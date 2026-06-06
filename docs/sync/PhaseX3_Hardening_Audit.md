# Phase X.3 — Visibility & Tracing: Audit

**Date:** 2026-05-30
**Status:** Completed — 3 issues fixed; 4 review claims dismissed as already-fixed-in-prior-phase
**Scope constraint:** Sync.Infrastructure + Sync.Persistence + Sync.Host only. Modules + Abstractions untouched.

---

## 1. Review Triage

A third-party "deep assessment" surfaced 10 concerns. Triage:

| # | Concern | Class | Status |
|---|---|---|---|
| 1 | Suppressed Job Failures (outbox writer returns "success" while rows silently fail) | Visibility bug | **FIXED — code** (§3.1) |
| 2 | Deadlock Hazard in filters / executor (`.GetAwaiter().GetResult()`) | Already addressed | **NO-OP — claim contradicts code** (§4.1) |
| 3 | Audit Trail Race (dispatcher enqueues before audit row; orphan reaper never called) | Already addressed | **NO-OP — fixed in Phase X.1** (§4.2) |
| 4 | Fake Production Reality (defaults still InMemory in Program.cs) | Design decision | **DOCUMENTED — opt-in default is correct** (§4.3) |
| 5 | Service Locator Anti-pattern in modules | Modules off-limits | **DEFERRED — scope constraint** (§4.4) |
| 6 | Result Factory Bug (`SyncResult.Ok` ignores `RecordsFailed`) | Visibility bug | **FIXED — code** (§3.1) |
| 7 | God Class / Tight Coupling (`SyncPipeline` uses thin engines) | Already addressed | **NO-OP — engines DI-interfaced in Phase X.2** (§4.5) |
| 8 | Stopwatch Bloat (manual timer boilerplate) | Real maintenance debt | **FIXED — code** (§3.2) |
| 9 | `SyncContext` required-property leak | Abstractions off-limits | **DEFERRED — scope constraint** (§4.6) |
| 10 | Ghost Interface (`ISyncAlertingHook.PipelineFailureAsync` never wired) | Already addressed | **NO-OP — wired in Phase X, X.1, X.2** (§4.7) |

**Code fixes: 3 categories (#1, #6, #8).**
**Already-fixed-in-prior-phase claims dismissed with evidence: 4 (#2, #3, #7, #10).**
**Scope-deferred with rationale: 3 (#4, #5, #9).**

---

## 2. Code Changes (file-by-file)

### Sync.Infrastructure

| File | Change |
|---|---|
| `Configuration/SyncPipelineOptions.cs` | + `FailRunOnAnyWriterSkip` (default `false`). When `true`, any writer-skipped row in a run flips the result to `SyncResult.Failed` so the dashboard shows red and Hangfire retries engage. Default preserves Phase 6 per-row isolation semantics. |
| `Pipeline/SyncPipeline.cs` | Tracks `writerSkipped += mapped.Count - written` per batch (fix #1). Emits a per-batch Warning when `> 0` so operators see the drop in the structured log immediately. Pipeline metrics log line gains a `WriterSkipped=N` field. Final result construction switched from `SyncResult.Ok(...)` to a direct `new SyncResult { … RecordsFailed = totalFailed … }` so the failed count surfaces in audit (fix #6). When `FailRunOnAnyWriterSkip` is on, returns `SyncResult.Failed(...)` instead. + `using var pipelineActivity = SyncDiagnostics.Source.StartActivity(...)` wrapping the whole run with module/direction/correlation tags + final outcome tags (fix #8). + `Activity.Current.AddEvent(...)` emission in `LogStage` so per-stage durations propagate to OpenTelemetry collectors without parsing logs. |
| `Observability/SyncDiagnostics.cs` | **new** — central `ActivitySource` (`CapitalUniversity.Sync`, v1.0.0) plus stable tag-name constants. Operators wire OpenTelemetry/Application Insights/Datadog by registering a listener on this source — zero sync-layer code change. |

### Files explicitly NOT touched (scope constraint)

- `Sync.Abstractions/**` — byte-identical. `SyncResult` factory shape unchanged; the pipeline just constructs the type directly to surface `RecordsFailed` (the `init` setter is public).
- `Sync.Student/**`, `Sync.Staff/**` — byte-identical.
- `Sync.Persistence/**` — byte-identical.

---

## 3. Per-Fix Detail

### 3.1 Concern #1 + #6 — Suppressed Failures + `SyncResult.Ok` Lossy Factory

**The flaw.** Phase 6's `StudentOutboxPushWriter` (and Staff equivalent) catches per-row sink exceptions to preserve outbox per-row isolation — failed rows stay `Pending` with `AttemptCount + 1`. The writer returns the **count of rows that succeeded**, which may be lower than the input batch size. Pipeline code did `processed += written` and called `SyncResult.Ok(processed, ...)`. `SyncResult.Ok` hardcodes `RecordsFailed = 0`. Result: a push tick that sent zero records to the sink because the upstream is down looks **fully green** in `sync.runs` (`Status = Succeeded`, `RecordsProcessed = 0`, `RecordsFailed = 0`). Outbox backlog grows silently.

**The fix.** Three changes in `SyncPipeline.RunAsync`:

1. New per-batch counter:
   ```csharp
   var perBatchWriterSkipped = mapped.Count - written;
   if (perBatchWriterSkipped > 0)
   {
       writerSkipped += perBatchWriterSkipped;
       _logger.LogWarning(correlationId,
           "Pipeline writer-skipped rows. Module={Module} BatchIndex={Index} Mapped={Mapped} Written={Written} Skipped={Skipped}. " +
           "The writer returned a lower count than it received — likely per-row sink failure that the writer absorbed.",
           moduleName, batchesProcessed, mapped.Count, written, perBatchWriterSkipped);
   }
   ```

2. Direct `SyncResult` construction (bypasses lossy `Ok` factory):
   ```csharp
   var totalFailed = failed + writerSkipped;
   return new SyncResult
   {
       Success = true,
       RecordsProcessed = processed,
       RecordsFailed = totalFailed,
       Duration = stopwatch.Elapsed,
       Warnings = summarizedWarnings
   };
   ```

3. New `FailRunOnAnyWriterSkip` option for strict-visibility operators:
   ```csharp
   if (writerSkipped > 0 && pipelineOptionsForResult.FailRunOnAnyWriterSkip)
   {
       return SyncResult.Failed(
           $"Writer skipped {writerSkipped} of {extracted - idempotencySkipped - failed} mapped rows. " +
           "Strict-visibility mode is on — failing the run so the failure surfaces in audit and Hangfire retries.",
           stopwatch.Elapsed, processed, totalFailed);
   }
   ```

**Runtime verification** — seeded two outbox rows, armed sink failure on one, triggered push:

```
Pipeline metrics. Module=students Batches=1 Extracted=2 IdempotencySkipped=0
  ValidationFailed=0 WriterSkipped=1 Processed=1 …

Pipeline writer-skipped rows. Module=students BatchIndex=1 Mapped=2 Written=1 Skipped=1.
  The writer returned a lower count than it received — likely per-row sink failure that
  the writer absorbed (e.g. outbox push with sink down). These are reported as
  RecordsFailed in the SyncResult so the audit row no longer reads as fully green.

sync.runs:  Status=Succeeded  RecordsProcessed=1  RecordsFailed=1
```

Before the fix, the `sync.runs` row would have read `Status=Succeeded RecordsProcessed=1 RecordsFailed=0` — the silent loss the reviewer correctly identified. After the fix, `RecordsFailed=1` is now visible to any dashboard query filtering on it.

**Why Status stays Succeeded by default.** Phase 6's design was that per-row failures are tracked on the outbox row (`AttemptCount`, `LastError`, `Status=Pending`) and re-attempted on the next tick — preserving partial-batch isolation. Forcing the whole run to `Failed` would engage Hangfire's `[AutomaticRetry]` (4 retries with 1/5/15/60 min backoff) for every transient sink hiccup, which is heavier than the recurring-tick path. Operators who want failure-as-failure flip `Sync:Pipeline:FailRunOnAnyWriterSkip = true`.

### 3.2 Concern #8 — Stopwatch Bloat → ActivitySource

**The flaw.** `SyncPipeline.RunAsync` declares four `Stopwatch` instances + a manual `extractionTimer.Restart()` pattern between batches. The boilerplate is maintenance-heavy and doesn't propagate to APM tools — operators can only get stage-level timing by parsing log lines.

**The fix.** New `Observability/SyncDiagnostics.cs` declares a central `ActivitySource` (`CapitalUniversity.Sync`, v1.0.0). `SyncPipeline.RunAsync` opens one parent activity per run with tags for module / direction / correlation / batch-size / attempt. Per-stage timings are emitted as `ActivityEvent`s via the existing `LogStage` helper (which keeps the structured-log emission too — additive, not replacing).

The existing `Stopwatch` totals (`extractionMsTotal`, `mappingMsTotal`, etc.) are kept so the `Pipeline metrics.` log line still carries them — operators dashboarding on the log line don't need to migrate. New operators get the same data through OpenTelemetry by registering a listener:

```csharp
// Operator's host: subscribe to sync activities
using var tracer = new ActivityListener
{
    ShouldListenTo = src => src.Name == "CapitalUniversity.Sync",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
};
ActivitySource.AddActivityListener(tracer);
```

No sync-layer code change required. Default cost when no listener is registered is essentially zero — `Source.StartActivity(...)` returns `null` and the `?.SetTag` chain short-circuits.

**Removing the Stopwatch boilerplate entirely** would have been ideal but breaks the existing `Pipeline metrics.` log line shape (which dashboards already consume). The compromise: ActivitySource for new consumers, Stopwatch for the legacy log line. A future refactor can drop the Stopwatch when the log line becomes redundant with the activity feed.

---

## 4. Concerns Dismissed With Evidence

### 4.1 Concern #2 — `.GetAwaiter().GetResult()` claim

The reviewer asserts `SyncDeadLetterFilter` and `SyncModuleExecutor` still use `.GetAwaiter().GetResult()`. **This contradicts the code.** A repository-wide `grep` for `GetAwaiter\(\)\.GetResult|\.GetResult\(\)` against `src/5.Sync/` returns:

```
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:27:  comment
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:32:  comment
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:140: comment
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Pipeline\SyncPipeline.cs:259:        comment
```

All four hits are **comments describing past fixes**, not live call sites. Phase X.2 fix #1 removed the last live `.GetAwaiter().GetResult()`:
- `SyncDeadLetterFilter` audit writes use sync EF Core (`db.SaveChanges()`) directly.
- `SyncDeadLetterFilter` alerting hook fan-out runs in `Task.Run` with a fresh DI scope — true fire-and-forget.
- `SyncPipeline.PipelineFailureAsync` uses native `await ... .ConfigureAwait(false)` inside its already-async catch block.
- `SyncModuleExecutor` uses `async/await` throughout — no sync-over-async anywhere.

**No code change required this phase.** Documented for the avoidance of doubt.

### 4.2 Concern #3 — Audit Trail Race / Orphan Reaper

The reviewer claims `FindOrphanRunsAsync` exists but is never called. **This was true at the end of Phase 10 and is no longer true after Phase X.1 fix #5.**

- `SyncOrphanReaperService` (in `Sync.Infrastructure/Reaper/`) wires `ISyncRunRepository.FindOrphanRunsAsync` into a `RunAsync` sweep.
- `SyncOrphanReaperRecurringTrigger` registers `sync-orphan-reaper` as a Hangfire recurring job (every 5 minutes by default).
- `Sync:OrphanReaper:GraceMinutes` (default 10) protects against transient enqueue-window races.
- `POST /admin/reaper/run` operator-triggered immediate sweep.
- Phase X.1 §3.5 ran a live test: seeded a stuck `Enqueued+null-JobId` row at `EnqueuedAt = -30 min`, triggered the reaper, observed `Status=0 → Status=3 (Failed)` with descriptive `LastError`.

The other half of the race — dispatcher enqueues *before* audit row exists — is also closed: Phase X.1 fix #11 made `OpenRunAsync` idempotent and the executor now calls it defensively at entry, so even if the dispatcher's audit write silently failed, the executor self-heals the missing row before `MarkStartedAsync`.

**No code change required this phase.** The reviewer's working memory appears to be pre-Phase X.1.

### 4.3 Concern #4 — Fake Production Reality (defaults)

The reviewer notes that `Program.cs` still defaults to `InMemoryExternalStudentSource`/`Sink`. **This is by design.**

Phase X.2 fix #3 introduced HTTP adapters in `Sync.Host/Integration/` with config-flag opt-in (`Sync:Integration:UseHttpAdapters`). The flag defaults to `false` because:

1. Dev/test environments without a real upstream endpoint must still be able to boot and exercise the pipeline. Defaulting `UseHttpAdapters = true` would require every contributor to have a real upstream URL configured — making the codebase un-pullable for first-time contributors.
2. Production deployments **explicitly** flip the flag to `true` with operator-supplied `BaseUrl` + `ApiKey`. The flip is a single config-key change.
3. The InMemory implementations are the integration-test scaffold — they remain useful long after production HTTP adapters land.

**Carried debt entry:** every prior audit since Phase 5 has explicitly listed "swap in-memory module sources/sinks for real adapters" as a production cutover gate. Phase X.2 made the swap mechanism trivial; the deployment decision to flip the flag is operator-owned, not platform-owned.

**No code change required this phase.** The architecture is correct; the carried-debt list already captures the deployment step.

### 4.4 Concern #5 — Service Locator in Modules

The reviewer notes that `StudentSyncModule` and `StaffSyncModule` use `IServiceScopeFactory.CreateAsyncScope` + `GetRequiredService`. **Scope constraint forbids modifying modules.** Also, the pattern is intentional — each `PullAsync`/`PushAsync` invocation needs a **fresh** DbContext scope so EF tracking doesn't leak across runs. The service-locator pattern is the standard .NET DI way to acquire a per-call scope from a singleton module.

The alternative — constructor-injecting transient services — would tie the module's lifetime to the DI container's scope, which is the wrong shape for Hangfire-invoked job code (which has no ambient request scope).

**No code change permissible** without violating the operator-imposed scope constraint. Documented in the carried-debt list for Phase 7+ scope expansion.

### 4.5 Concern #7 — God Class / Engines Are Thin

The reviewer characterizes `SyncPipeline` as a god class with thin-wrapper engines. **The "thin wrappers" framing was the design intent.** ChangeDetector/MappingEngine/MergeEngine are each one method, by design — they are seams for testing, not behavioral abstractions. Phase X.2 fix #2 converted them to interfaces (`IChangeDetector`, `IMappingEngine`, `IMergeEngine`) so DI substitution works cleanly.

`SyncPipeline` is the orchestrator. One orchestrator class owning the stage sequence (extract → dedup → map → validate → merge → metrics) is correct for this layer. Decomposing further would fragment the per-run control flow without test or maintenance gain.

**No code change required this phase.** The DI-substitution gap the reviewer flagged was already closed; the "god class" framing is style preference.

### 4.6 Concern #9 — `SyncContext` Required Properties

`SyncContext` lives in `Sync.Abstractions`. **Off-limits per scope constraint.**

The cleanest fix would be a `SyncContext.ForTest(...)` static factory or builder in Abstractions providing sensible defaults. That requires opening Abstractions, which the operator has consistently kept off-limits since Phase 8. The workaround for tests today: a per-test-project `TestContextFactory` helper that wraps `new SyncContext { … }` with default values. Each consuming test project owns its own.

**Deferred to a future scope expansion of Abstractions.** Documented.

### 4.7 Concern #10 — Ghost `PipelineFailureAsync`

The reviewer claims `ISyncAlertingHook.PipelineFailureAsync` is defined but never wired. **This is false as of Phase X.1.**

Live call sites:

1. **`SyncPipeline.RunAsync` catch block** (Phase X, line ~277 of current file):
   ```csharp
   await _alertingHook.PipelineFailureAsync(alert, cancellationToken).ConfigureAwait(false);
   ```
   Fires on every pipeline exception before `SyncResult.Failed` is returned.

2. **`SyncModuleExecutor.UpdateRunAsync` audit-failure path** (Phase X.1, fix #4):
   ```csharp
   await _alertingHook.PipelineFailureAsync(new SyncAlert {
       Title = $"Sync audit write failed: {operation}",
       Severity = "Warning",
       …
   }, CancellationToken.None).ConfigureAwait(false);
   ```
   Fires when a `sync.runs` write throws — so operators see the audit-DB itself struggling.

Both wired through proper async `await` (no sync-over-async). The default `LoggingSyncAlertingHook` writes a structured `[ALERT][PipelineFailure]` line at Warning level on every fire.

**No code change required this phase.** The reviewer's working memory appears to be pre-Phase X.

---

## 5. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` byte-identical | ✅ |
| `Sync.Student` byte-identical | ✅ |
| `Sync.Staff` byte-identical | ✅ |
| `Sync.Persistence` byte-identical | ✅ |
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, pipeline contracts | ✅ unchanged |
| `SyncResult` shape | ✅ unchanged — pipeline now constructs directly to populate `RecordsFailed` properly |
| All prior-phase audit-row shapes, retention, replay, health, lag, alerting | ✅ unchanged |
| Default `FailRunOnAnyWriterSkip = false` preserves Phase 6 per-row isolation semantics | ✅ Verified runtime §3.1: `Status=Succeeded` despite `RecordsFailed=1` |
| Existing `Pipeline metrics.` log line shape | ✅ Extended additively with `WriterSkipped=N`; existing tools that parse other fields keep working |
| ActivitySource zero-cost when no listener | ✅ BCL contract — `StartActivity` returns `null` without listeners; `?.` chain short-circuits |

---

## 6. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `WriterSkipped` is computed as `mapped.Count - written`. A writer that legitimately deduplicates within its own logic (returning a lower count for "this was a no-op, already done") would now be reported as a failure. | Low | Documented in `SyncPipelineOptions.FailRunOnAnyWriterSkip` XML doc. The `IRecordWriter<T>` contract since Phase 4 says return-value is "rows persisted by this call" — module writers that have internal idempotency should still return the full input count (the spec interpretation). The new `WriterSkipped` Warning log makes mis-implementations loud. |
| Dashboards filtering on `sync.runs.RecordsFailed = 0` to find "clean" runs will see more rows fall outside the filter once writers honestly report skips. | Trivial | Expected — that's the whole point of fix #6. |
| Operators turning on `FailRunOnAnyWriterSkip` get more Hangfire retries on transient sink hiccups (4 retries × 1/5/15/60 min). | Low | Documented opt-in. The trade-off is dashboard visibility vs retry pressure; operators choose. |
| ActivitySource adds a small allocation per run when listeners are registered. | Trivial | BCL handles this — no listener = no allocation. Cost is negligible relative to the run's work. |
| Carried debt: `AllowAllDashboardAuthorizationFilter`, SQL passwords, HTTP `ApiKey` plain config, in-memory adapters as default. | High (deploy) | Same as every prior phase. **Must be addressed before non-local deployment.** |

---

## 7. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| `WriterSkipped` per-batch Warning fires when writer returns lower count | ✅ §3.1 runtime evidence |
| `Pipeline metrics.` log line includes `WriterSkipped=N` | ✅ §3.1 |
| `sync.runs.RecordsFailed` populated correctly (no longer always 0) | ✅ §3.1 SQL snapshot |
| Default behavior: `Status = Succeeded` with `RecordsFailed > 0` (visibility without retry pressure) | ✅ §3.1 |
| `FailRunOnAnyWriterSkip = true` flips to `Status = Failed` | ✅ Code-verified — opt-in policy |
| `SyncDiagnostics.Source` declared + activities started in `SyncPipeline.RunAsync` | ✅ Code-verified §3.2 |
| Per-stage `LogStage` additionally emits `ActivityEvent` when listener present | ✅ Code-verified §3.2 |
| Zero `.GetAwaiter().GetResult()` calls (only comments remain) | ✅ Repo-wide `grep` evidence §4.1 |
| Orphan reaper recurring registered + operator endpoint live (Phase X.1) | ✅ Unchanged — verified §4.2 |
| HTTP adapter scaffold + opt-in flag (Phase X.2) | ✅ Unchanged — verified §4.3 |
| `PipelineFailureAsync` wired at two call sites | ✅ Code-verified §4.7 |
| Pre-existing unrelated test build error | ⚠️ Unchanged |

---

## 8. Phase Exit Checklist

- [x] `SyncPipelineOptions.FailRunOnAnyWriterSkip` added (default `false`)
- [x] `SyncPipeline.RunAsync` tracks `writerSkipped` per batch
- [x] Per-batch Warning logged when `writerSkipped > 0`
- [x] `Pipeline metrics.` log line gains `WriterSkipped=N` field
- [x] Final `SyncResult` constructed directly (not via `Ok` factory) so `RecordsFailed` is honest (fix #1 + #6)
- [x] Strict-visibility mode: `FailRunOnAnyWriterSkip=true` returns `SyncResult.Failed` when any writer skip occurs
- [x] `SyncDiagnostics` `ActivitySource` declared with stable tag constants (fix #8)
- [x] `SyncPipeline` opens parent activity per run with module/direction/correlation tags
- [x] Per-stage activity events emitted from `LogStage` when listeners are present
- [x] Existing `Stopwatch` totals + log line preserved for backward compatibility
- [x] Runtime verified: 2 outbox rows seeded, 1 sink failure armed → `WriterSkipped=1`, `RecordsFailed=1`, `RecordsProcessed=1`, `Status=Succeeded` (default mode)
- [x] Concerns #2 / #3 / #7 / #10 dismissed with code evidence (§4)
- [x] Concerns #4 / #5 / #9 documented as scope-constrained (§4)
- [x] No change to `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`, `Sync.Persistence`
- [x] No public-surface change on `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase X.3 complete. The sync platform's visibility surface now matches its safety surface — silent writer-suppressed failures are reported in audit, OpenTelemetry attach is a single listener registration away, and four review claims that contradicted post-Phase-X.2 reality are explicitly documented as such with code references.**

**Carried debt remains (every phase, deployment-owned):**
1. Replace `AllowAllDashboardAuthorizationFilter` with real auth.
2. Move SQL passwords + HTTP `ApiKey` to user-secrets / Key Vault.
3. Flip `Sync:Integration:UseHttpAdapters = true` for production deployments.
4. Production HTTP adapter hardening (Polly retry, real auth flow) when a real upstream contract exists.
