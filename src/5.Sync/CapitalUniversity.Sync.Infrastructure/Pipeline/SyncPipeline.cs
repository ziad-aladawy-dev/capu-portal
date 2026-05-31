using System.Diagnostics;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Pipeline;

/// <summary>
/// Orchestrates the spec's pipeline:
///   Extractor → BatchProcessor → IdempotencyHandler → Mapper
///     → optional Validator → Writer.
///
/// Pipeline does NOT advance the checkpoint — the module owns that decision after
/// inspecting the returned <see cref="SyncResult"/>.
/// </summary>
public sealed class SyncPipeline : ISyncPipeline
{
    private readonly ISyncLogger _logger;
    private readonly IOptionsMonitor<SyncOptions> _options;
    private readonly ISyncAlertingHook? _alertingHook;

    /// <summary>
    /// <see cref="ISyncAlertingHook"/> is optional — wired by the host when the
    /// operator opts into alerting (defaults to a no-op log hook in the standard
    /// <c>AddSyncInfrastructure</c> registration).
    /// </summary>
    public SyncPipeline(
        ISyncLogger logger,
        IOptionsMonitor<SyncOptions> options,
        ISyncAlertingHook? alertingHook = null)
    {
        _logger = logger;
        _options = options;
        _alertingHook = alertingHook;
    }

    public async Task<SyncResult> RunAsync<TExternal, TInternal>(
        SyncPipelineRequest<TExternal, TInternal> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Central batch-size guard. Modules' own option validators (e.g. StudentSyncOptionsValidator)
        // are the first line; this is defense-in-depth so the pipeline never accepts a request
        // that would crash the writer with a SQL Server parameter-limit overflow.
        if (request.BatchSize <= 0 || request.BatchSize > MaxBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.BatchSize,
                $"SyncPipelineRequest.BatchSize must be in (0, {MaxBatchSize}]. Module={request.Context.ModuleName}.");
        }

        var correlationId = request.Context.CorrelationId;
        var moduleName = request.Context.ModuleName;
        var idempotency = new IdempotencyHandler<string>(StringComparer.Ordinal);
        var idempotencyMemoryWarningEmitted = false;

        // Bounded validation-warning aggregation: identical messages roll up to a single
        // entry with a count. Hard-capped at MaxDistinctWarnings categories so a misbehaving
        // validator emitting record-specific messages cannot grow this dictionary without
        // bound. Overflow records are folded into a single "(further warning categories
        // suppressed)" bucket.
        var warningCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        var stopwatch = Stopwatch.StartNew();
        var extracted = 0;
        var processed = 0;
        var failed = 0;
        var idempotencySkipped = 0;
        var batchesProcessed = 0;
        // Phase X.3 fix #1: tracks rows that reached the writer but did NOT
        // transition to "processed" (e.g. Phase 6 outbox push writers catch per-row
        // sink failures and return a lower count). Surfaces silently-dropped rows
        // to RecordsFailed in the SyncResult + the audit table.
        var writerSkipped = 0;

        // Aggregate per-stage durations across all batches → single summary log line.
        long extractionMsTotal = 0;
        long mappingMsTotal = 0;
        long validationMsTotal = 0;
        long writingMsTotal = 0;

        var attempt = request.Context.Attempt;
        var replayDetected = attempt > 1;
        var replayReason = replayDetected ? "RetryReplay" : "None";

        // ActivitySource is the BCL-standard seam for OpenTelemetry / APM
        // backends to attach without any sync-layer code change.
        using var pipelineActivity = SyncDiagnostics.Source.StartActivity(
            SyncDiagnostics.PipelineRunActivity,
            ActivityKind.Internal);
        SetInitialActivityTags(pipelineActivity, correlationId, request.Context, request.BatchSize, attempt);

        _logger.LogInformation(correlationId,
            "Pipeline started. Module={Module} BatchSize={BatchSize} HasCheckpoint={HasCheckpoint} Attempt={Attempt}",
            moduleName, request.BatchSize, request.CurrentCheckpoint is not null, attempt);

        if (replayDetected)
        {
            _logger.LogInformation(correlationId,
                "Pipeline replay detected. Module={Module} Attempt={Attempt} Reason={Reason}. " +
                "Replay is expected and safe — writers are idempotent on the external merge key.",
                moduleName, attempt, replayReason);
        }

        var source = request.Extractor.ExtractAsync(
            request.Context, request.CurrentCheckpoint, cancellationToken);

        // Inter-yield Stopwatch measures the wall-clock time between batch arrivals
        // from the extractor (effectively "Extraction" time per batch).
        var extractionTimer = Stopwatch.StartNew();

        try
        {
            await foreach (var batch in BatchProcessor.ChunkAsync(source, request.BatchSize, cancellationToken)
                .ConfigureAwait(false))
            {
                batchesProcessed++;
                var extractionMs = extractionTimer.ElapsedMilliseconds;
                extractionMsTotal += extractionMs;
                LogStage(correlationId, moduleName, "Extraction", extractionMs, batchesProcessed);

                // 1. Idempotency dedup
                extracted += batch.Count;
                var unique = new List<TExternal>(batch.Count);
                foreach (var external in batch)
                {
                    var key = request.ExternalKeySelector(external);
                    if (idempotency.TryAdd(key))
                    {
                        unique.Add(external);
                    }
                    else
                    {
                        idempotencySkipped++;
                        _logger.LogDebug(correlationId,
                            "Idempotency skip. Module={Module} Key={Key}", moduleName, key);
                    }
                }

                // Single-shot memory warning — for multi-million-record runs the in-run
                // dedup HashSet can dominate worker memory. Emit once when the threshold
                // is crossed so operators can see it in the audit before the next OOM.
                if (!idempotencyMemoryWarningEmitted &&
                    idempotency.SeenCount > IdempotencyMemoryWarningThreshold)
                {
                    _logger.LogWarning(correlationId,
                        "Pipeline idempotency-dedup memory threshold crossed. Module={Module} SeenKeys={SeenKeys} Threshold={Threshold}. " +
                        "Consider streaming dedup or smaller per-run cursor windows for very large syncs.",
                        moduleName, idempotency.SeenCount, IdempotencyMemoryWarningThreshold);
                    idempotencyMemoryWarningEmitted = true;
                }

                // Phase X.5 hard cap on idempotency-set size. Prevents silent OOM
                // under multi-million-record runs where the Warning above was the
                // only signal (and that signal was easy to miss in busy logs).
                var idempotencyHardCap = _options.CurrentValue.Pipeline.MaxIdempotencyKeysPerRun;
                if (idempotencyHardCap > 0 && idempotency.SeenCount >= idempotencyHardCap)
                {
                    stopwatch.Stop();
                    var capError =
                        $"Pipeline aborted: idempotency dedup set reached the hard cap of {idempotencyHardCap:N0} keys. " +
                        $"Module={moduleName} SeenKeys={idempotency.SeenCount:N0} Processed={processed} BatchesProcessed={batchesProcessed}. " +
                        "Lower the cursor window for this module's extractor, raise " +
                        "Sync:Pipeline:MaxIdempotencyKeysPerRun (operator-tuned against worker memory), " +
                        "or move to a streaming-dedup strategy (Phase 8 expansion).";
                    _logger.LogError(correlationId, null, "{Error}", capError);
                    return SyncResult.Failed(capError, stopwatch.Elapsed, processed, failed + writerSkipped);
                }

                // 2. Map
                var mappingTimer = Stopwatch.StartNew();
                var mapped = new List<TInternal>(unique.Count);
                foreach (var external in unique)
                {
                    mapped.Add(request.Mapper.Map(external));
                }
                mappingTimer.Stop();
                mappingMsTotal += mappingTimer.ElapsedMilliseconds;
                LogStage(correlationId, moduleName, "Mapping", mappingTimer.ElapsedMilliseconds, batchesProcessed);

                // 3. Validate (optional)
                if (request.Validator is not null)
                {
                    var validationTimer = Stopwatch.StartNew();
                    var valid = new List<TInternal>(mapped.Count);
                    foreach (var record in mapped)
                    {
                        if (request.Validator.IsValid(record, out var error))
                        {
                            valid.Add(record);
                        }
                        else
                        {
                            failed++;
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                RecordWarning(warningCounts, error!);
                            }
                        }
                    }
                    mapped = valid;
                    validationTimer.Stop();
                    validationMsTotal += validationTimer.ElapsedMilliseconds;
                    LogStage(correlationId, moduleName, "Validation", validationTimer.ElapsedMilliseconds, batchesProcessed);
                }

                // 4. Merge (with optional per-batch writer retry — Phase 8 hardening).
                //    Default is one attempt (PerBatchWriterRetryAttempts=0). When the
                //    operator enables it, a writer exception is retried up to
                //    Attempts times with PerBatchWriterRetryBackoff between tries —
                //    avoiding the whole-pipeline replay path on transient DB hiccups.
                //    Mapper/validator failures are NOT retried (re-running them on
                //    identical input would produce identical results).
                var writingTimer = Stopwatch.StartNew();
                var pipelineOpts = _options.CurrentValue.Pipeline;
                var writerRetryBudget = Math.Max(0, pipelineOpts.PerBatchWriterRetryAttempts);
                var writerBackoff = pipelineOpts.PerBatchWriterRetryBackoff;
                var written = await MergeWithRetryAsync(
                    mapped,
                    request.Writer,
                    moduleName,
                    correlationId,
                    batchesProcessed,
                    writerRetryBudget,
                    writerBackoff,
                    cancellationToken).ConfigureAwait(false);
                writingTimer.Stop();
                writingMsTotal += writingTimer.ElapsedMilliseconds;
                LogStage(correlationId, moduleName, "Writing", writingTimer.ElapsedMilliseconds, batchesProcessed);

                processed += written;
                // Phase X.3 fix #1: rows the writer reached but did not persist are
                // "writer-skipped" — surfaces silent-drop scenarios (sink down with
                // per-row catch returning lower count). Counted distinctly from
                // validator drops (which are tracked in `failed`) so the metrics log
                // separates "bad input rejected" from "good input not persisted".
                var perBatchWriterSkipped = mapped.Count - written;
                if (perBatchWriterSkipped > 0)
                {
                    writerSkipped += perBatchWriterSkipped;
                    _logger.LogWarning(correlationId,
                        "Pipeline writer-skipped rows. Module={Module} BatchIndex={Index} Mapped={Mapped} Written={Written} Skipped={Skipped}. " +
                        "The writer returned a lower count than it received — likely per-row sink failure that the writer absorbed (e.g. outbox push with sink down). These are reported as RecordsFailed in the SyncResult so the audit row no longer reads as fully green.",
                        moduleName, batchesProcessed, mapped.Count, written, perBatchWriterSkipped);
                }

                _logger.LogDebug(correlationId,
                    "Batch processed. Module={Module} BatchIndex={Index} BatchSize={Size} Mapped={Mapped} Written={Written} Skipped={Skipped}",
                    moduleName, batchesProcessed, batch.Count, mapped.Count, written, perBatchWriterSkipped);

                extractionTimer.Restart();
            }
        }
        catch (OperationCanceledException oce)
            when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            // Legitimate cancellation: either the executor's token is signaled (host
            // shutdown) or the OCE's own token is signaled (module-supplied source token,
            // typically via CancellationCoordinator).
            stopwatch.Stop();
            _logger.LogInformation(correlationId,
                "Pipeline cancelled. Module={Module} Extracted={Extracted} Processed={Processed} Batches={Batches} Elapsed={Elapsed}",
                moduleName, extracted, processed, batchesProcessed, stopwatch.Elapsed);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Spurious OCE — no cancellation token is signaled. Treat as a module bug:
            // log Warning and fall through to the regular failure path (SyncResult.Failed).
            stopwatch.Stop();
            _logger.LogWarning(correlationId,
                "Pipeline spurious OperationCanceledException. Module={Module} Extracted={Extracted} Processed={Processed} Elapsed={Elapsed}. " +
                "Neither the executor token nor the exception's token is signaled — likely a module-internal OCE used for non-cancellation logic. Reporting as Failed.",
                moduleName, extracted, processed, stopwatch.Elapsed);
            return SyncResult.Failed(
                "Spurious OperationCanceledException without cancellation signal: " + ex.Message,
                stopwatch.Elapsed, processed, failed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(correlationId, ex,
                "Pipeline failure. Module={Module} BatchIndex={Batch} Extracted={Extracted} Processed={Processed} Elapsed={Elapsed}",
                moduleName, batchesProcessed, extracted, processed, stopwatch.Elapsed);

            // Fire-and-forget pipeline-failure alert. Operator gets early warning
            // before Hangfire's retry policy exhausts and the dead-letter alert fires.
            await SendAlertSafelyAsync(new SyncAlert
            {
                CorrelationId = correlationId,
                ModuleName = moduleName,
                Direction = request.Context.Direction.ToString(),
                Title = $"Sync pipeline failure: {moduleName} {request.Context.Direction}",
                Severity = "Warning",
                Detail = ex.Message,
                AttemptCount = attempt,
                Tags = request.Context.Metadata.Tags
            }, "Pipeline-failure", cancellationToken).ConfigureAwait(false);

            return SyncResult.Failed(ex.Message, stopwatch.Elapsed, processed, failed);
        }

        stopwatch.Stop();

        var summarizedWarnings = BuildSummarizedWarnings(warningCounts);

        // Single normalized completion-metrics log line. Counts:
        //   Extracted          = total records emitted by the extractor
        //   IdempotencySkipped = duplicates dropped by the in-run dedup
        //   ValidationFailed   = records dropped by the optional validator
        //   Processed          = records persisted by the writer
        //   Extracted = IdempotencySkipped + ValidationFailed + Processed   (in steady state)
        //   ReplayDetected     = Attempt > 1. Pipeline can only see retry-replay; module-level
        //                        checkpoint-lag replay is logged separately by the module.
        // Phase 8: throughput metrics. Records/sec = persisted-records-per-wall-clock-second;
        // Batches/sec = how quickly the pipeline drained its batch queue. Both rounded
        // to one decimal because operators read these in dashboards, not for accounting.
        // Computed defensively when elapsed = 0 (e.g. checkpoint-fast-path with zero
        // records) — division by zero would NaN-poison the log.
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var elapsedSec = elapsedMs > 0 ? elapsedMs / 1000.0 : 0.0;
        var recordsPerSec = elapsedSec > 0 ? Math.Round(processed / elapsedSec, 1) : 0.0;
        var batchesPerSec = elapsedSec > 0 ? Math.Round(batchesProcessed / elapsedSec, 1) : 0.0;

        WriteMetricsLine(correlationId, moduleName, batchesProcessed, extracted, idempotencySkipped,
            failed, writerSkipped, processed, summarizedWarnings.Count, attempt, replayDetected,
            replayReason, extractionMsTotal, mappingMsTotal, validationMsTotal, writingMsTotal,
            elapsedMs, recordsPerSec, batchesPerSec);

        SetCompletionActivityTags(pipelineActivity, extracted, processed, failed, writerSkipped, idempotencySkipped);

        // Phase X.3 fix #1 + #6: report writer-skipped rows in RecordsFailed.
        // SyncResult.Ok(...) factory hardcodes RecordsFailed = 0 (lossy), so we
        // construct the result directly here to surface the full failure picture.
        // RecordsFailed = validation drops + writer drops. Auditors / dashboards
        // that filter on RecordsFailed > 0 will now see ALL non-success counts,
        // not just validator rejections.
        var totalFailed = failed + writerSkipped;
        var pipelineOptionsForResult = _options.CurrentValue.Pipeline;

        // Fire pipeline-failure alert whenever writer-skipped > 0, regardless of
        // pass/fail policy. Operator's early-warning surface for "upstream is
        // misbehaving" before the dashboard even sees the count.
        if (writerSkipped > 0)
        {
            await SendAlertSafelyAsync(new SyncAlert
            {
                CorrelationId = correlationId,
                ModuleName = moduleName,
                Direction = request.Context.Direction.ToString(),
                Title = $"Sync writer-skipped rows: {moduleName} {request.Context.Direction}",
                Severity = processed == 0 ? "Critical" : "Warning",
                Detail = $"Writer skipped {writerSkipped} of {writerSkipped + processed} rows that reached it. " +
                         (processed == 0
                            ? "Zero rows persisted — upstream sink is likely down."
                            : "Partial progress made; some rows will retry on the next tick."),
                AttemptCount = attempt,
                Tags = request.Context.Metadata.Tags
            }, "Writer-skipped", cancellationToken).ConfigureAwait(false);
        }

        // Phase X.4 fix #2: adaptive failure policy. Two scenarios force a Failed result:
        //
        // (a) Strict-visibility mode (operator opted in): any writer-skipped row fails
        //     the run. Aggressive — every per-row hiccup triggers Hangfire's full
        //     [AutomaticRetry(Attempts=4)] backoff.
        //
        // (b) ZERO-PROGRESS scenario (default behavior): the writer was offered records
        //     to persist AND persisted none. This is the "upstream is dead" signature —
        //     a green job in this state would silently hide a production outage. Forcing
        //     Failed surfaces it in the Hangfire dashboard + engages retry. Mixed-batch
        //     runs (some succeeded, some failed) still return Ok to preserve Phase 6
        //     per-row isolation semantics.
        //
        // The previous Phase X.3 default ("always Ok when writer-skipped > 0") covered
        // the partial case correctly but missed the zero-progress case — the exact
        // failure mode that prompted the reviewer's "Audit Fraud" finding.
        var zeroProgressFailure = writerSkipped > 0 && processed == 0;
        var strictFailure = writerSkipped > 0 && pipelineOptionsForResult.FailRunOnAnyWriterSkip;

        if (zeroProgressFailure || strictFailure)
        {
            var reason = zeroProgressFailure
                ? $"Zero-progress sync: writer received {writerSkipped} rows and persisted 0. " +
                  "Default policy fails the run so Hangfire retries engage and the failure surfaces in the dashboard. " +
                  "Outbox rows remain Pending — the per-row failure isolation from Phase 6 still applies."
                : $"Writer skipped {writerSkipped} of {extracted - idempotencySkipped - failed} mapped rows. " +
                  "Strict-visibility mode (Sync:Pipeline:FailRunOnAnyWriterSkip) is on — failing the run so the failure surfaces in audit and Hangfire retries.";

            return SyncResult.Failed(reason, stopwatch.Elapsed, processed, totalFailed);
        }

        return new SyncResult
        {
            Success = true,
            RecordsProcessed = processed,
            RecordsFailed = totalFailed,
            Duration = stopwatch.Elapsed,
            Warnings = summarizedWarnings
        };
    }

    /// <summary>
    /// Hard upper bound on <see cref="SyncPipelineRequest{TExt,TInt}.BatchSize"/>. Sourced
    /// from the shared <see cref="SyncLimits.MaxBatchSize"/> constant so the pipeline,
    /// each module's options validator, and operator tooling all agree on one number.
    /// </summary>
    public const int MaxBatchSize = SyncLimits.MaxBatchSize;

    /// <summary>
    /// Cap on distinct validation-message categories tracked per pipeline run.
    /// Validators are REQUIRED to emit normalized category strings (e.g., "Email invalid")
    /// not record-specific messages. This cap is defense-in-depth against a misbehaving
    /// validator — once the cap is reached, all further unique messages are folded into
    /// a single overflow bucket. Lowered to 50 to tighten the memory envelope.
    /// </summary>
    public const int MaxDistinctWarnings = 50;

    /// <summary>
    /// Threshold at which a single Warning log is emitted noting that the in-run
    /// idempotency-dedup HashSet has grown beyond what is comfortable for worker memory.
    /// One warning per run; not a hard cap because dropping keys would silently allow
    /// duplicates through.
    /// </summary>
    public const int IdempotencyMemoryWarningThreshold = 1_000_000;

    private const string OverflowBucket = "(further warning categories suppressed)";

    private static void RecordWarning(Dictionary<string, int> warningCounts, string error)
    {
        if (warningCounts.ContainsKey(error))
        {
            warningCounts[error]++;
            return;
        }

        if (warningCounts.Count >= MaxDistinctWarnings)
        {
            warningCounts[OverflowBucket] = warningCounts.GetValueOrDefault(OverflowBucket, 0) + 1;
            return;
        }

        warningCounts[error] = 1;
    }

    private void LogStage(Guid correlationId, string moduleName, string stage, long durationMs, int batch)
    {
        _logger.LogDebug(correlationId,
            "Pipeline stage completed. Module={Module} Stage={Stage} DurationMs={DurationMs} Batch={Batch}",
            moduleName, stage, durationMs, batch);
    }

    private async Task<int> MergeWithRetryAsync<TInternal>(
        IReadOnlyList<TInternal> mapped,
        IRecordWriter<TInternal> writer,
        string moduleName,
        Guid correlationId,
        int batchIndex,
        int retryBudget,
        TimeSpan retryBackoff,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return mapped.Count == 0
                    ? 0
                    : await writer.UpsertBatchAsync(mapped, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is honored immediately — never retried.
                throw;
            }
            catch (Exception ex) when (attempt < retryBudget)
            {
                attempt++;
                _logger.LogWarning(correlationId,
                    "Pipeline per-batch writer failure — retrying. Module={Module} Batch={Batch} Attempt={Attempt}/{Budget} BackoffMs={BackoffMs} Error={Error}",
                    moduleName, batchIndex, attempt, retryBudget,
                    (long)retryBackoff.TotalMilliseconds, ex.Message);

                if (retryBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(retryBackoff, cancellationToken).ConfigureAwait(false);
                }
                // Loop and re-attempt the merge.
            }
        }
    }

    private static IReadOnlyList<string> BuildSummarizedWarnings(IReadOnlyDictionary<string, int> warningCounts)
    {
        if (warningCounts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>(warningCounts.Count);
        foreach (var kvp in warningCounts)
        {
            list.Add(kvp.Value == 1 ? kvp.Key : $"{kvp.Key} (x{kvp.Value})");
        }
        return list;
    }

    // ── Telemetry helpers ──────────────────────────────────────────────────────
    // Kept out of RunAsync so the orchestration flow reads top-to-bottom without
    // 30-line tag-setting / log-line blocks in the middle.

    private static void SetInitialActivityTags(
        Activity? activity, Guid correlationId, SyncContext context, int batchSize, int attempt)
    {
        if (activity is null) return;
        activity.SetTag(SyncDiagnostics.TagCorrelationId, correlationId.ToString());
        activity.SetTag(SyncDiagnostics.TagModule, context.ModuleName);
        activity.SetTag(SyncDiagnostics.TagDirection, context.Direction.ToString());
        activity.SetTag(SyncDiagnostics.TagBatchSize, batchSize);
        activity.SetTag(SyncDiagnostics.TagAttempt, attempt);
    }

    private static void SetCompletionActivityTags(
        Activity? activity, int extracted, int processed, int validationFailed,
        int writerSkipped, int idempotencySkipped)
    {
        if (activity is null) return;
        activity.SetTag(SyncDiagnostics.TagExtracted, extracted);
        activity.SetTag(SyncDiagnostics.TagProcessed, processed);
        activity.SetTag(SyncDiagnostics.TagValidationFailed, validationFailed);
        activity.SetTag(SyncDiagnostics.TagWriterSkipped, writerSkipped);
        activity.SetTag(SyncDiagnostics.TagIdempotencySkipped, idempotencySkipped);
    }

    private void WriteMetricsLine(
        Guid correlationId, string moduleName, int batches, int extracted,
        int idempotencySkipped, int validationFailed, int writerSkipped, int processed,
        int distinctWarnings, int attempt, bool replayDetected, string replayReason,
        long extractionMs, long mappingMs, long validationMs, long writingMs,
        long totalMs, double recordsPerSec, double batchesPerSec)
    {
        _logger.LogInformation(correlationId,
            "Pipeline metrics. Module={Module} Batches={Batches} Extracted={Extracted} IdempotencySkipped={IdempotencySkipped} ValidationFailed={ValidationFailed} WriterSkipped={WriterSkipped} Processed={Processed} DistinctWarnings={DistinctWarnings} Attempt={Attempt} ReplayDetected={ReplayDetected} ReplayReason={ReplayReason} ExtractionMs={ExtractionMs} MappingMs={MappingMs} ValidationMs={ValidationMs} WritingMs={WritingMs} TotalMs={TotalMs} RecordsPerSec={RecordsPerSec} BatchesPerSec={BatchesPerSec}",
            moduleName, batches, extracted, idempotencySkipped, validationFailed, writerSkipped,
            processed, distinctWarnings, attempt, replayDetected, replayReason,
            extractionMs, mappingMs, validationMs, writingMs, totalMs, recordsPerSec, batchesPerSec);
    }

    /// <summary>
    /// Fire-and-forget alert dispatch. Swallows hook exceptions so a flaky
    /// destination can never replace the genuine pipeline error path.
    /// </summary>
    private async Task SendAlertSafelyAsync(SyncAlert alert, string contextLabel, CancellationToken cancellationToken)
    {
        if (_alertingHook is null) return;
        try
        {
            await _alertingHook.PipelineFailureAsync(alert, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception alertEx)
        {
            _logger.LogWarning(alert.CorrelationId,
                "{Context} alerting hook threw — ignored. Module={Module} Error={Error}",
                contextLabel, alert.ModuleName, alertEx.Message);
        }
    }
}