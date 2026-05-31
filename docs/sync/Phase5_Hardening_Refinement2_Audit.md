# Phase 5 — Hardening Refinement #2 Audit
## Operational Hazards Closeout

**Date:** 2026-05-29
**Status:** Completed (8 hazards addressed; 2 intentionally deferred with rationale)
**Scope:** Surgical fixes to specific hazards surfaced in the post-Refinement review. No new infrastructure, no schema change, no API break.

---

## 1. Closeout Matrix

| # | Hazard (review item) | Resolution | Status |
|---|---|---|---|
| 1 | SQL parameter limit via `.Contains()` for non-Student modules | `SyncPipeline.MaxBatchSize = 1000` central guard + fail-fast at `RunAsync` | ✅ Verified by code path (StudentSyncOptionsValidator already proves the same pattern runtime) |
| 2 | Checkpoint save silent "Success" with Warning | **Error**-level log with `REPLAY EXPECTED ON NEXT RUN`, includes Processed + ElapsedMs + Error | ✅ Code review |
| 3 | Validation message bloat from unique data | `MaxDistinctWarnings: 100 → 50`; `IRecordValidator<>` XML now REQUIRES normalized category strings with GOOD/BAD examples | ✅ |
| 4 | Non-transactional read-then-upsert | **Existing race-handler retained**: `DbUpdateException(2627\|2601)` → `ChangeTracker.Clear()` → retry once. Adding a serializable transaction would serialize all upserts and tank throughput; CAS-style optimistic concurrency is the chosen trade-off. | ✅ (Documented) |
| 5 | Concurrent module execution | **Already addressed in Hardening 1**: `PerModuleDisableConcurrencyAttribute` scoped per `(module, direction)` using `IStorageConnection.AcquireDistributedLock`. Review may have missed this. | ✅ (No change needed; cross-referenced) |
| 6 | Timestamp monotonicity assumption | `StudentExtractor.SafetyBuffer = 1 second` clawback on `since-exclusive`. XML documents the back-dating / clock-drift rationale. | ✅ Runtime-visible on the first tick after the change (record #50 re-emitted by the recurring schedule) |
| 7 | Aggressive OCE catching | **Tightened**: legitimate cancellation now requires `cancellationToken.IsCancellationRequested OR oce.CancellationToken.IsCancellationRequested`. Spurious OCEs (no signaled token) → log Warning + classify as Failed, write `sync.failures` row. | ✅ Runtime-verified — see §3 |
| 8 | Cooperative-cancellation cleanup hang | `SyncModuleExecutor.CleanupTimeout = 15s` bounded CTS around `MarkCancelledAsync` write so a slow DB cannot hold up shutdown | ✅ Code review |
| 9 | Idempotency / warning memory ceiling at multi-million-record scale | Single-emit Warning when `IdempotencyHandler.SeenCount > 1,000,000`; document streaming-dedup as Phase-8 follow-up | ✅ |
| 10 | Push placeholder silent success | `StudentSyncModule.PushAsync` emits Warning explicitly stating Phase-6 implementation needed | ✅ Runtime-verified — see §3 |
| 11 | Audit table bloat | Documented in `docs/sync/SyncAuditRetention.md` (Hardening 1) — strategy + triggers + cleanup plan. No scheduler ships here. | ✅ (Cross-reference) |
| 12 | Per-batch retry strategy | **Intentionally deferred to Phase 8 (Performance)**. Pipeline-level retry restructuring requires architecture changes (sub-job dispatch, intermediate checkpoint format) that exceed a refinement pass. Replay-safe writer absorbs the cost. | 🟡 Deferred |

---

## 2. Code Changes (file-by-file)

### Sync.Abstractions
- `Pipeline/IRecordValidator.cs` — XML doc-comment now **requires** normalized category strings with explicit GOOD / BAD examples. Mentions `SyncPipeline.MaxDistinctWarnings` as defense-in-depth.

### Sync.Infrastructure
- `Pipeline/SyncPipeline.cs`:
  - `public const int MaxBatchSize = 1000;` (new) — central guard. `RunAsync` throws `ArgumentOutOfRangeException` if `request.BatchSize <= 0 || > MaxBatchSize`.
  - `public const int MaxDistinctWarnings = 50;` (lowered from 100).
  - `public const int IdempotencyMemoryWarningThreshold = 1_000_000;` (new). Single Warning per run when crossed.
  - **OCE filter tightened** into two `catch` blocks:
    - Block A (legitimate): `when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)` → rethrow.
    - Block B (spurious): logs `Pipeline spurious OperationCanceledException. ... Reporting as Failed.` and returns `SyncResult.Failed(...)`.
- `Execution/SyncModuleExecutor.cs`:
  - `public static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(15);` (new).
  - **OCE filter tightened** with the same legitimate/spurious split. Legitimate branch uses `using var cleanupCts = new CancellationTokenSource(CleanupTimeout);` for the `MarkCancelledAsync` write so cancellation cleanup cannot block host shutdown.
  - Spurious branch: log Warning, call `RecordFailureAsync(...)`, throw `SyncExecutionException("Sync module '...' threw a spurious OperationCanceledException without any cancellation signal.")`.

### Sync.Student
- `Extraction/StudentExtractor.cs`:
  - `public static readonly TimeSpan SafetyBuffer = TimeSpan.FromSeconds(1);` (new).
  - `since = parsed - SafetyBuffer;` — clawback applied to the cursor before the source filter.
  - XML explains the back-dating / clock-drift rationale.
- `Modules/StudentSyncModule.cs`:
  - Checkpoint-save failure → `LogError` instead of `LogWarning`, message reads `Checkpoint save failed — REPLAY EXPECTED ON NEXT RUN.` Includes `ex` as the structured exception.
  - `PushAsync` no longer silently returns success. Emits Warning: `Push not implemented for module {Module}. Returning success with Processed=0 to satisfy the dispatcher contract. Wire a real push pipeline in Phase 6 — do not treat this as a successful sync.`

### Sync.Host (verification-only)
- `Modules/FailingPipeline/FailingPipelineStage.cs`: + `SpuriousOce = 3` enum member.
- `Modules/FailingPipeline/FailingExtractor.cs`: throws `new OperationCanceledException(SpuriousOceMessage)` with no token when stage is `SpuriousOce`.

### NOT changed
- `Sync.Persistence` — no schema change, no migration.
- `ISyncModule`, `ISyncDispatcher`, `ISyncPipeline`, `IDataExtractor<>`, `IRecordMapper<>`, `IRecordWriter<>` — interface signatures byte-identical (`IRecordValidator<>` XML strengthened, not signature).
- `SyncContext`, `SyncResult`, `SyncRunMetadata` — unchanged.
- Hangfire configuration, queues, retry policy, recurring jobs — untouched.
- `FakeSyncModule`, `FakePipelineSyncModule`, `CancellingPipelineSyncModule`, `PartialFailingStudentSyncModule` — untouched.
- `PerModuleDisableConcurrencyAttribute` — preserved (review's "DisableConcurrentExecution still missing" appears to have missed this).
- `StudentWriter` race-handler — preserved (CAS-style optimistic concurrency).

---

## 3. Runtime Verification

### 3.1 Success regression — students tick

After truncating the students table and resetting the checkpoint:

```
Pipeline metrics. Module=students Batches=2 Extracted=50 IdempotencySkipped=0
ValidationFailed=2 Processed=48 DistinctWarnings=1 Attempt=1 ReplayDetected=False
ReplayReason=None ExtractionMs=0 MappingMs=0 ValidationMs=0 WritingMs=39 TotalMs=41
Checkpoint advanced. Module=students Cursor=01/01/2026 00:50:00 +00:00
```

✅ Metric shape unchanged; success path is regression-clean.

### 3.2 Safety-buffer visible in subsequent recurring tick

Immediately after, the next recurring `student-sync-pull` tick (with prior cursor at `00:50:00`) extracted **1 record**:

```
Pipeline metrics. Module=students Batches=1 Extracted=1 IdempotencySkipped=0
ValidationFailed=0 Processed=1 DistinctWarnings=0 Attempt=1 ReplayDetected=False
ReplayReason=None ExtractionMs=1 MappingMs=0 ValidationMs=0 WritingMs=8 TotalMs=9
```

The extractor's safety buffer (1 s) shifted the filter from `> 00:50:00` to `> 00:49:59`, causing record #50 (which has `ExternalUpdatedAt = 00:50:00`) to re-emit. The writer's `ExternalStudentId`-keyed upsert absorbed the re-presentation as a no-op update — final row count stayed at 48.

✅ **Safety buffer working** — back-dated records can now be observed at the cost of a tiny, idempotent replay.

### 3.3 Push no-op visibility

Direct trigger of `students` with `direction=Push`:

```
{"module":"students","direction":"Push","jobId":"460","correlationId":"deb35e3a-…","failureMode":null}
```

Log:

```
Push not implemented for module students. Returning success with Processed=0 to
satisfy the dispatcher contract. Wire a real push pipeline in Phase 6 — do not treat
this as a successful sync.
```

✅ Push is no longer silently successful — operators now see the unimplemented state.

### 3.4 Spurious-OCE classification

With `Sync:FailingPipeline:FailureStage=SpuriousOce`:

```
POST /admin/trigger/failing-pipeline?direction=Pull
→ {"jobId":"479","correlationId":"34d58cf3-…"}
```

Pipeline log:

```
Pipeline spurious OperationCanceledException. Module=failing-pipeline Extracted=0
Processed=0 Elapsed=00:00:00.0033147. Neither the executor token nor the exception's
token is signaled — likely a module-internal OCE used for non-cancellation logic.
Reporting as Failed.
```

Executor log:

```
Sync execution threw. Module=failing-pipeline Direction=Pull Attempt=1 …
SyncExecutionException: FailingPipeline (SpuriousOce): Spurious OperationCanceledException
   without cancellation signal: FailingExtractor: spurious OperationCanceledException
   without cancellation signal.
```

SQL state:

```
SELECT Status, HangfireJobId FROM sync.runs WHERE CorrelationId='34d58cf3-…';
Status  HangfireJobId
1       479                   ← Running (waiting for next retry attempt), not Cancelled

SELECT COUNT(*) FROM sync.failures WHERE CorrelationId='34d58cf3-…';
1                              ← failure row appended per the executor's failure path
```

✅ **Spurious OCE no longer misclassified as Cancelled.**
- Status remains `Running` (Hangfire-retry territory), eventually `DeadLettered` after exhaustion — the failure-path machinery owns the outcome.
- `sync.failures` row appended.
- The cancellation-cleanup path (`MarkCancelledAsync`) was **not** invoked.

---

## 4. Items Out of Scope (Justified)

### Per-batch retry (review item 7)
- Current model retries the whole module execution. Per-batch retry would require:
  - Sub-job dispatch (one Hangfire job per batch) → multiplies Hangfire load by N.
  - Intermediate checkpoint format (per-batch high-water-mark) → schema change.
  - Pipeline restructuring (extractor reset to per-batch range) → public-API change.
- Replay-safe writers absorb whole-execution retry cost. Performance impact is acceptable until module count or batch count grows much further.
- **Belongs to Phase 8 (Performance)** per the implementation plan.

### Audit retention scheduler (review item 5 — bloat)
- Strategy lives in `docs/sync/SyncAuditRetention.md`.
- Implementation triggers (rows > 5 M, dashboard p99 > 500 ms, compliance) are defined and not yet hit.
- Implementing now would add a Hangfire recurring job + config + kill switch — within scope of a future Phase-9 (Resilience) or Phase-10 (Observability) pass.

### Streaming idempotency dedup (review item 5 — memory)
- Hash-set based dedup is O(N) memory per run. For multi-million-record syncs, a Bloom filter or chunked dedup would cut this dramatically.
- Refinement adds an early-warning log instead (single emit at 1 M keys). Operators can then split the cursor window or escalate to Phase-8 work.

### Transactional checkpoint + write coordination
- Explicitly forbidden by the brief ("no distributed transactions / no transactional checkpoint coordination"). The replay-safe writer is the chosen alternative.

### `StudentWriter` race transaction
- The current optimistic-concurrency pattern (catch unique-constraint exception, clear tracker, retry) converges deterministically and preserves throughput.
- Wrapping in a serializable transaction would serialize all writers for the same key range → throughput cliff.
- The race-handler is what makes the retry safe; it's the spec-aligned answer to the brief's "duplicate insert races converge safely" requirement.

---

## 5. Boundary & Regression Verification

| Check | Result |
|---|---|
| `ISyncModule` / `ISyncDispatcher` / `ISyncPipeline` / `SyncContext` / `SyncResult` byte-identical signature surface | ✅ |
| `IRecordWriter<>` / `IRecordMapper<>` / `IRecordValidator<>` / `IDataExtractor<>` signatures byte-identical (`IRecordValidator<>` XML strengthened) | ✅ |
| `IFailureRepository` / `IDeadLetterRepository` / `ISyncRunRepository` / `ISyncCheckpointStore` byte-identical | ✅ |
| No new project | ✅ |
| No new schema; no new migration | ✅ |
| No new infrastructure (broker, outbox, CQRS, MediatR, distributed-TX) | ✅ |
| Hangfire connection-string, queues, retry policy, recurring jobs unchanged | ✅ |
| `PerModuleDisableConcurrencyAttribute` (Hardening 1) retained | ✅ |
| `StudentSyncOptionsValidator` (Hardening 1) retained — module-level batch guard | ✅ |
| `StudentWriter` unique-constraint race-handler (Hardening 1) retained | ✅ |
| `CorrelationScopedFailingStudentWriter` (Refinement 1) retained | ✅ |
| Replay logging (Refinement 1) retained | ✅ |
| Audit-retention doc retained | ✅ |
| Clean Debug build, `TreatWarningsAsErrors=true`, 0/0 | ✅ |

---

## 6. Production-Readiness Recheck

| Dimension | Status | Notes |
|---|---|---|
| Universal batch-size guard | ✅ | Pipeline-level constant; defense-in-depth above module options |
| Checkpoint-save failure surfaced loudly | ✅ | Error level + structured replay tags |
| Validation warning growth bounded | ✅ | 50-distinct cap + contract-level validator XML |
| Concurrent execution prevention | ✅ | Per-(module, direction) Hangfire distributed lock |
| Read-then-write race convergence | ✅ | CAS retry-once on unique violation |
| Timestamp back-dating tolerated | ✅ | 1-second clawback; small idempotent replay absorbed |
| Cancellation cleanup bounded | ✅ | 15-second CTS on `MarkCancelledAsync` |
| Spurious OCE not silently classified as cancellation | ✅ | Verified at runtime |
| Push placeholder visible | ✅ | Warning log per call |
| Idempotency memory ceiling warned | ✅ | Single Warning at 1M keys |
| Audit retention strategy documented | ✅ | `docs/sync/SyncAuditRetention.md` |
| Per-batch retry | 🟡 Phase 8 | Replay-safe writer absorbs current cost |

---

## 7. Phase Exit Checklist

- [x] `SyncPipeline.MaxBatchSize` central guard + `RunAsync` validation
- [x] `SyncPipeline.MaxDistinctWarnings` lowered to 50
- [x] `SyncPipeline.IdempotencyMemoryWarningThreshold` + single-emit warning
- [x] Pipeline OCE catch split into legitimate / spurious branches
- [x] Executor OCE catch split into legitimate / spurious branches
- [x] Executor `CleanupTimeout = 15s` on cancellation cleanup write
- [x] `StudentSyncModule` checkpoint-save → Error log with `REPLAY EXPECTED ON NEXT RUN`
- [x] `StudentSyncModule.PushAsync` emits explicit Warning
- [x] `StudentExtractor.SafetyBuffer = 1s` clawback on cursor
- [x] `IRecordValidator<>` XML strengthened with GOOD / BAD examples
- [x] `FailingPipelineStage.SpuriousOce` test path
- [x] Runtime-verified: success regression, safety-buffer visible, push warning, spurious-OCE → Failed
- [x] No public API breakage
- [x] No new infrastructure / schema / migration
- [x] Audit document produced

---

**Phase 5 Hardening Refinement #2 complete. Stopping per instruction.**
