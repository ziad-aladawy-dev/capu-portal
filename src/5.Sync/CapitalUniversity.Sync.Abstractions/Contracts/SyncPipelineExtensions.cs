using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Reusable scaffolds for the two standard module flows (Pull + Push). Modules
/// supply only the bits that genuinely vary per domain — module name, batch
/// size, key selector, and per-stage component factories — and the helper
/// owns the boilerplate that used to be copy-pasted across every module:
/// scope creation, service resolution, pipeline invocation, and (for Pull)
/// checkpoint advance via <see cref="ICursorObserver"/>.
///
/// <para>
/// SOLID: each helper has one responsibility (orchestrate one flow shape).
/// Modules stay concrete sealed classes — no inheritance, no abstract members,
/// no hidden control flow — they just delegate one method call.
/// </para>
/// </summary>
public static class SyncPipelineExtensions
{
    /// <summary>
    /// Pull-flow scaffold. Opens a DI scope, resolves <c>ISyncCheckpointStore</c>
    /// and the four pipeline components, runs the pipeline, then — on success
    /// and when the extractor implements <see cref="ICursorObserver"/> — saves
    /// the new cursor to <c>SyncCheckpoint.Cursor</c>. A failed checkpoint save
    /// is logged at Error (the next run will replay, which is safe because
    /// writers are idempotent on the external merge key).
    /// </summary>
    public static async Task<SyncResult> RunStandardPullAsync<TExternal, TInternal>(
        this ISyncPipeline pipeline,
        IServiceScopeFactory scopeFactory,
        ISyncLogger logger,
        SyncContext context,
        string moduleName,
        int batchSize,
        Func<TExternal, string> externalKeySelector,
        Func<IServiceProvider, IDataExtractor<TExternal>> extractorFactory,
        Func<IServiceProvider, IRecordMapper<TExternal, TInternal>> mapperFactory,
        Func<IServiceProvider, IRecordValidator<TInternal>?> validatorFactory,
        Func<IServiceProvider, IRecordWriter<TInternal>> writerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(externalKeySelector);
        ArgumentNullException.ThrowIfNull(extractorFactory);
        ArgumentNullException.ThrowIfNull(mapperFactory);
        ArgumentNullException.ThrowIfNull(validatorFactory);
        ArgumentNullException.ThrowIfNull(writerFactory);

        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var checkpointStore = sp.GetRequiredService<ISyncCheckpointStore>();
        var extractor = extractorFactory(sp);
        var current = await checkpointStore.GetAsync(moduleName, cancellationToken).ConfigureAwait(false);

        var result = await pipeline.RunAsync(new SyncPipelineRequest<TExternal, TInternal>
        {
            Context = context,
            Extractor = extractor,
            Mapper = mapperFactory(sp),
            Validator = validatorFactory(sp),
            Writer = writerFactory(sp),
            ExternalKeySelector = externalKeySelector,
            BatchSize = batchSize,
            CurrentCheckpoint = current
        }, cancellationToken).ConfigureAwait(false);

        // Read the cursor through the contractual ICursorObserver interface so
        // a mocked IDataExtractor that doesn't implement ICursorObserver simply
        // skips checkpoint advance (instead of silently breaking it).
        if (result.Success && extractor is ICursorObserver observer &&
            observer.CurrentCursor is { } cursor)
        {
            try
            {
                await checkpointStore.SaveAsync(moduleName, new SyncCheckpoint
                {
                    ModuleName = moduleName,
                    LastSyncedAt = DateTimeOffset.UtcNow,
                    Cursor = cursor
                }, cancellationToken).ConfigureAwait(false);

                logger.LogInformation(context.CorrelationId,
                    "Checkpoint advanced. Module={Module} Cursor={Cursor}",
                    moduleName, cursor);
            }
            catch (Exception ex)
            {
                // Pipeline succeeded; checkpoint save failed. Next run will reprocess
                // the range — safe because writers are idempotent on the external merge
                // key. Escalated to Error so monitoring/alerting flags it.
                logger.LogError(context.CorrelationId, ex,
                    "Checkpoint save failed — REPLAY EXPECTED ON NEXT RUN. Module={Module} Cursor={Cursor} Processed={Processed} ElapsedMs={ElapsedMs} Error={Error}.",
                    moduleName, cursor, result.RecordsProcessed,
                    (long)result.Duration.TotalMilliseconds, ex.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Push-flow scaffold. Opens a DI scope, resolves the four outbox-pipeline
    /// components, and runs the pipeline. No checkpoint — the outbox row's
    /// <c>Status</c> column is the cursor.
    /// </summary>
    public static async Task<SyncResult> RunStandardPushAsync<TExternal, TInternal>(
        this ISyncPipeline pipeline,
        IServiceScopeFactory scopeFactory,
        SyncContext context,
        int batchSize,
        Func<TExternal, string> externalKeySelector,
        Func<IServiceProvider, IDataExtractor<TExternal>> extractorFactory,
        Func<IServiceProvider, IRecordMapper<TExternal, TInternal>> mapperFactory,
        Func<IServiceProvider, IRecordValidator<TInternal>?> validatorFactory,
        Func<IServiceProvider, IRecordWriter<TInternal>> writerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(externalKeySelector);
        ArgumentNullException.ThrowIfNull(extractorFactory);
        ArgumentNullException.ThrowIfNull(mapperFactory);
        ArgumentNullException.ThrowIfNull(validatorFactory);
        ArgumentNullException.ThrowIfNull(writerFactory);

        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        return await pipeline.RunAsync(new SyncPipelineRequest<TExternal, TInternal>
        {
            Context = context,
            Extractor = extractorFactory(sp),
            Mapper = mapperFactory(sp),
            Validator = validatorFactory(sp),
            Writer = writerFactory(sp),
            ExternalKeySelector = externalKeySelector,
            BatchSize = batchSize,
            CurrentCheckpoint = null
        }, cancellationToken).ConfigureAwait(false);
    }
}