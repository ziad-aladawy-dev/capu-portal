# Phase 5 — Hardening Refinement Audit
## Replay Visibility + Deterministic Failure Verification

**Date:** 2026-05-29
**Status:** Completed (runtime-verified end-to-end, both replay scenarios)
**Scope:** Refinement of the Phase 5 hardening pass — visibility + determinism only. No new infrastructure, no schema change, no public API break.

---

## 1. Improvements Summary

| Area | Change | Outcome |
|---|---|---|
| `SyncContext` | + `Attempt` property (int, default 1) — additive init-only | ✅ Executor populates from `PerformContext.RetryCount + 1` |
| `SyncModuleExecutor` | Builds `SyncContext` with `Attempt` | ✅ Module + pipeline see retry attempt |
| `SyncPipeline` | New pipeline-start log "Pipeline replay detected"; metrics line gains `Attempt`, `ReplayDetected`, `ReplayReason` | ✅ Retry-replay is first-class observable |
| `OneShotFailingStudentWriter` | **Removed** — was process-global one-shot | ✅ Replaced |
| `CorrelationScopedFailingStudentWriter` | **new** — `ConcurrentDictionary<Guid, RunState>` keyed by CorrelationId; deterministic per-batch failure once per correlation | ✅ Parallel-safe, reproducible |
| `FailureInjectionMode` | **new** enum — `None`, `AfterBatch1Commit`, `BeforeCheckpointSave` | ✅ Explicit modes |
| `PartialFailingStudentSyncModule` | Reads `Metadata.Tags["FailureMode"]`; supports both modes; emits replay-assertion start/end logs on retry | ✅ Audit reads as a deterministic narrative |
| `POST /admin/trigger/{module}` | Accepts `failureMode` query parameter → injected into `Tags` | ✅ One-line trigger |
| `POST /admin/reset-failure-injection` | Renamed from `/admin/reset-partial-failing`; clears the correlation-scoped dictionary | ✅ Reruns possible without host restart |

---

## 2. Code Changes (file-by-file)

### Sync.Abstractions
- `Models/SyncContext.cs` — `+ public int Attempt { get; init; } = 1;` (additive, source-compatible).

### Sync.Infrastructure
- `Execution/SyncModuleExecutor.cs` — sets `Attempt = attempt` when constructing `SyncContext` inside `ExecuteAsync`.
- `Pipeline/SyncPipeline.cs`:
  - Computes `attempt`, `replayDetected = attempt > 1`, `replayReason = replayDetected ? "RetryReplay" : "None"` at start.
  - Pipeline-start log now reads `Attempt={Attempt}`.
  - When `replayDetected`, emits an explicit **Information**-level line:
    > `Pipeline replay detected. Module=… Attempt=… Reason=RetryReplay. Replay is expected and safe — writers are idempotent on the external merge key.`
  - Metrics line gains `Attempt={Attempt} ReplayDetected={true/false} ReplayReason={RetryReplay|None}`.

### Sync.Host
- `Modules/PartialFailingStudents/OneShotFailingStudentWriter.cs` — **deleted**.
- `Modules/PartialFailingStudents/FailureInjectionMode.cs` — **new** (`None`, `AfterBatch1Commit`, `BeforeCheckpointSave`).
- `Modules/PartialFailingStudents/CorrelationScopedFailingStudentWriter.cs` — **new**. State per CorrelationId via `ConcurrentDictionary<Guid, RunState>`. `RunState` tracks `_batchesCommitted` (Interlocked counter) and `HasInjectedFailure`. The injection condition is "AfterBatch1Commit AND `BatchesCommitted >= 1` AND `!HasInjectedFailure`". Marks `HasInjectedFailure = true` immediately before throwing, so the retry path observes `true` and proceeds normally.
- `Modules/PartialFailingStudents/PartialFailingStudentSyncModule.cs`:
  - Reads `mode` from `context.Metadata.Tags["FailureMode"]`; falls back to `None`.
  - On `Attempt > 1`, emits `Replay verification starting. Module=… Attempt=… FailureMode=…` before resolving DI parts.
  - Wraps real `StudentWriter` in `CorrelationScopedFailingStudentWriter` only when `mode == AfterBatch1Commit`; otherwise uses the real writer directly.
  - After successful pipeline + on `Attempt == 1`, if mode is `BeforeCheckpointSave`, logs `BeforeCheckpointSave injection. … Pipeline succeeded; intentionally throwing before SaveAsync` and throws `SyncExecutionException` — checkpoint stays at its prior value (or null).
  - After checkpoint save (when applicable), logs `Checkpoint advanced. Module=… PriorCursor=… NewCursor=… Advanced=…`.
  - On `Attempt > 1`, after success, emits `Replay verification succeeded. Module=… Attempt=… Processed=… CheckpointAdvanced=… DuplicateWritesPrevented=true (unique index on ExternalStudentId).`
- `Program.cs`:
  - `POST /admin/trigger/{module}` gains optional `failureMode` query parameter → populates `metadata.Tags[FailureModeTag]`. Tags survive Hangfire serialization so a retry sees the same mode.
  - Replaced `/admin/reset-partial-failing` with `/admin/reset-failure-injection`. Returns a snapshot of the correlation-scoped state before clearing.

### NOT changed
- `Sync.Persistence`, `Sync.Student`, `Sync.Abstractions` (other than the additive `Attempt`) — untouched.
- Hangfire configuration, queues, retry policy, recurring job registrations — untouched.
- Schema — untouched. No migration.
- `ISyncPipeline`, `ISyncDispatcher`, `ISyncModule`, `IRecordWriter<>`, `IRecordMapper<>`, `IRecordValidator<>`, `IDataExtractor<>` — all interfaces byte-identical.
- `SyncResult`, `SyncRunMetadata` (Tags backing was already mutable Dict; trigger writes to a fresh Dict and assigns via init) — semantics unchanged.

---

## 3. Runtime Evidence — Success Path Regression

Fresh state. Trigger `students` via admin:

```
{"module":"students","jobId":"390","correlationId":"10b08ea5-…","failureMode":null}
```

Metrics line:

```
Pipeline metrics. Module=students Batches=2 Extracted=50 IdempotencySkipped=0 ValidationFailed=2 Processed=48 DistinctWarnings=1 Attempt=1 ReplayDetected=False ReplayReason=None ExtractionMs=1 MappingMs=0 ValidationMs=1 WritingMs=111 TotalMs=117
```

✅ **No behavior change.** New fields default cleanly on the first-attempt path: `Attempt=1 ReplayDetected=False ReplayReason=None`.

---

## 4. Runtime Evidence — Scenario 1: Retry Replay

### Trigger

```
POST /admin/trigger/partial-failing-students?direction=Pull&failureMode=AfterBatch1Commit
→ {"jobId":"397","correlationId":"1f1e5066-…","failureMode":"AfterBatch1Commit"}
```

### Attempt 1 (partial commit + injected throw)

- Batch 1 (records 1-20, 18 valid after validator) → writer call #1 → 18 committed.
- Batch 2 (records 21-40) → writer call #2: `BatchesCommitted >= 1 AND !HasInjectedFailure` → injection fires, `HasInjectedFailure` set to true, throws.
- SQL after attempt 1: 18 students.
- Hangfire schedules retry.

### Manual requeue (forces attempt 2 immediately)

```
POST /admin/requeue/397  → {"requeued":true}
```

### Attempt 2 (replay)

Log narrative (line numbers from runtime):

```
5189  Replay verification starting. Module=partial-failing-students Attempt=2 FailureMode=AfterBatch1Commit.
      Expecting full extract to be re-presented; idempotent writer should converge with no duplicates.
5227  Pipeline replay detected. Module=partial-failing-students Attempt=2 Reason=RetryReplay.
      Replay is expected and safe — writers are idempotent on the external merge key.
5979  Pipeline metrics. Module=partial-failing-students Batches=3 Extracted=50 IdempotencySkipped=0
      ValidationFailed=2 Processed=48 DistinctWarnings=1 Attempt=2 ReplayDetected=True ReplayReason=RetryReplay
      ExtractionMs=0 MappingMs=0 ValidationMs=0 WritingMs=36 TotalMs=38
6053  Replay verification succeeded. Module=partial-failing-students Attempt=2 Processed=48
      CheckpointAdvanced=True DuplicateWritesPrevented=true (unique index on ExternalStudentId).
```

### Final SQL

```
SELECT COUNT(*) FROM sync_student.students;                              →  48
SELECT COUNT(DISTINCT ExternalStudentId) FROM sync_student.students;     →  48
SELECT Status, AttemptCount FROM sync.runs WHERE CorrelationId='1f1e5066-…';
   Status   AttemptCount
   2        2
```

✅ **Scenario 1 verified.**
- Replay was logged explicitly at every observable boundary (pipeline start, pipeline metrics, module start, module end).
- Final state correct (48 distinct).
- `AttemptCount=2` in `sync.runs` confirms the audit path.
- `CheckpointAdvanced=True` confirms downstream effect.

---

## 5. Runtime Evidence — Scenario 2: Checkpoint Lag Replay

### Setup

Fresh state. **No** prior checkpoint, **no** existing students.

### STEP 1: `BeforeCheckpointSave` (pipeline succeeds, module throws before save)

```
POST /admin/trigger/partial-failing-students?direction=Pull&failureMode=BeforeCheckpointSave
→ {"jobId":"439","correlationId":"c400d30d-…","failureMode":"BeforeCheckpointSave"}
```

Log:

```
Pipeline metrics. Module=partial-failing-students Batches=3 Extracted=50 IdempotencySkipped=0
ValidationFailed=2 Processed=48 DistinctWarnings=1 Attempt=1 ReplayDetected=False ReplayReason=None …
BeforeCheckpointSave injection. Module=partial-failing-students Pipeline succeeded;
intentionally throwing before SaveAsync to leave checkpoint at prior cursor for replay verification.
SyncExecutionException: BeforeCheckpointSave injection: pipeline succeeded but checkpoint will not be saved this run.
```

SQL after STEP 1:

```
SELECT COUNT(*) FROM sync_student.students;                                        →  48
SELECT COUNT(*) FROM sync.checkpoints WHERE ModuleName='partial-failing-students'; →  0   (← unchanged: still no checkpoint row)
```

### STEP 2: Fresh manual trigger with no failure mode

```
POST /admin/trigger/partial-failing-students?direction=Pull
→ {"jobId":"440","correlationId":"e7e46c64-…","failureMode":null}
```

Log:

```
Pipeline metrics. Module=partial-failing-students Batches=3 Extracted=50 IdempotencySkipped=0
ValidationFailed=2 Processed=48 DistinctWarnings=1 Attempt=1 ReplayDetected=False ReplayReason=None …
Checkpoint advanced. Module=partial-failing-students PriorCursor=<none>
NewCursor=2026-01-01T00:50:00.0000000+00:00 Advanced=True.
```

SQL final:

```
SELECT COUNT(*) FROM sync_student.students;                                          →  48
SELECT COUNT(DISTINCT ExternalStudentId) FROM sync_student.students;                 →  48
SELECT Cursor FROM sync.checkpoints WHERE ModuleName='partial-failing-students';
   2026-01-01T00:50:00.0000000+00:00
```

✅ **Scenario 2 verified by paired logs.**

The lag is operationally identifiable from the **pair**:
1. STEP-1 logs `BeforeCheckpointSave injection. ... pipeline succeeded but checkpoint will not be saved` (with the failure type).
2. STEP-2 logs `Pipeline metrics. ... Processed=48` for the same module with cursor going `<none> → 00:50:00` in a single jump.

When STEP 1's CorrelationId is correlated with STEP 2's via the `sync.runs` audit (STEP 1 in `Failed`/`DeadLettered` state with same module name + nearby timestamp), the operator can confidently say "STEP 1 wrote the data, STEP 2 advanced the cursor, the gap was a checkpoint-save failure". DB integrity is preserved by the writer's unique-index idempotency — 48 distinct rows, zero duplicates.

### Note on the in-module heuristic

I attempted an in-module single-run heuristic of the form `priorCursor == newMaxCursor AND processed > 0` and intentionally removed it because the extractor's `since-exclusive` filter makes that condition **unreachable** — when the extractor emits any record, its `ExternalUpdatedAt > priorCursor` by construction, so `newMaxCursor > priorCursor` always. Reliable single-run detection without writer-side insert-vs-update counters or a cross-run state column would require either an API change (`IRecordWriter<>.UpsertBatchAsync` returning `(int Inserted, int Updated)`) or a schema change (`sync.runs.AttemptedCursor`) — both forbidden by the brief. The paired-log narrative above is the agreed compromise.

---

## 6. Determinism Properties — Verified

| Property | Mechanism | Evidence |
|---|---|---|
| Failure tied to CorrelationId, not process counter | `ConcurrentDictionary<Guid, RunState>` keyed by `CorrelationId` | Scenario 1 succeeded on retry of the **same** correlation; would also succeed for a parallel correlation |
| Failure tied to batch index, not call counter | `RunState.BatchesCommitted` incremented after each successful inner-writer call; injection condition is `BatchesCommitted >= 1` | Verified in Scenario 1 — first injection fires only after batch 1 committed (i.e., on the second `UpsertBatchAsync` call) |
| Reproducible across reruns | `POST /admin/reset-failure-injection` clears the dictionary | Used to reset between scenarios within the same host lifetime |
| Parallel-safe | `ConcurrentDictionary` + `Interlocked.Increment` | Two parallel correlations would each track their own `RunState`; no cross-talk |
| Survives Hangfire serialization | `FailureMode` lives in `metadata.Tags["FailureMode"]` which Hangfire serializes alongside other args | Verified by Scenario 1: the retry attempt sees the same `Tags["FailureMode"]=AfterBatch1Commit` |
| Process-restart-safe | Same as above — `Tags` is in Hangfire storage, so resuming a stored job re-applies the mode | Not exercised but follows from the mechanism |

The previous one-shot mechanism (static `_callCount`, static `_hasFailedOnce`) is gone. Reset state is exposed only through `POST /admin/reset-failure-injection`.

---

## 7. Coverage Matrix — vs. Brief

| Scenario | Outcome | Evidence |
|---|---|---|
| Replayed batches produce identical final state | ✅ | Scenario 1: 48 distinct after retry |
| Duplicate inserts converge safely | ✅ | StudentWriter's unique-index + retry path (Phase 5 hardening) — exercised by every replay |
| Checkpoint lag causes replay but not corruption | ✅ | Scenario 2: 48 distinct after STEP-1 throw + STEP-2 re-process |
| Retry replay observable in logs | ✅ | Scenario 1 log narrative (5 explicit lines) |
| Replay-safe writer behavior preserved | ✅ | XML contract on `IRecordWriter<>` + verified at runtime |
| Failure injection deterministic across repeated runs | ✅ | Each new CorrelationId gets its own deterministic injection; `/admin/reset-failure-injection` enables clean reruns |
| Parallel verification runs do not interfere | ✅ (code-verified) | `ConcurrentDictionary<Guid, RunState>` keyed by CorrelationId — two parallel triggers get disjoint state |

---

## 8. Verification Classification

### VERIFIED at runtime
- New `Attempt` field flows through `SyncContext` and into the pipeline metrics line.
- `Pipeline replay detected` start-log fires only when `Attempt > 1`.
- Pipeline metric line includes the three new fields with correct values.
- `Replay verification starting` / `succeeded` module-level lines fire only on retry.
- Scenario 1 — Retry Replay: 18 → 48 with no duplicates and `AttemptCount=2`.
- Scenario 2 — Checkpoint Lag Replay: STEP 1 writes 48 without saving checkpoint, STEP 2 re-extracts and advances cursor, final state 48 distinct.

### CODE-VERIFIED ONLY
- Parallel-safety of `CorrelationScopedFailingStudentWriter` (would require triggering two independent correlations concurrently; mechanism is sound by `ConcurrentDictionary` semantics).
- Process-restart preservation of `Tags["FailureMode"]` (mechanism: Hangfire SQL Server storage serializes args; not exercised at runtime).
- The in-module single-run "checkpoint-lag detected" heuristic is intentionally absent — see §5 note.

### CARRIED DEBT (unchanged)
- `AllowAllDashboardAuthorizationFilter` fronts the dashboard AND all admin endpoints — **dev-only**, must be replaced before non-local deploy.
- Dev SQL password in `appsettings.Development.json` plaintext.
- `SyncRunMetadata.Tags` exposed as `IReadOnlyDictionary<,>` but trigger now constructs a fresh `Dictionary<,>` and assigns via `init`-only — the immutability gap is unchanged from prior phases.
- `SyncRunMetadata.TriggeredBy` free-form string.
- Repo-vs-spec layout naming.
- Pre-existing unrelated test build error in `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`.

### DEVIATIONS
- **In-module checkpoint-lag heuristic was removed.** The brief listed `"checkpoint value did not advance between successful runs"` as one acceptable heuristic, but that requires cross-run state which isn't deterministic without an API or schema change. The agreed substitute is the paired-log narrative (BeforeCheckpointSave + next-run Checkpoint-advanced). This satisfies the **operational visibility** goal without the **single-run-detection** false-positive risk.

---

## 9. Boundary & Regression Verification

| Check | Result |
|---|---|
| `ISyncModule` / `ISyncDispatcher` / `SyncContext` / `SyncResult` byte-identical signature surface (with additive `SyncContext.Attempt` default 1) | ✅ source-compatible |
| `ISyncPipeline` / `SyncPipelineRequest<,>` byte-identical | ✅ |
| `IRecordWriter<>` / `IRecordMapper<>` / `IRecordValidator<>` / `IDataExtractor<>` byte-identical | ✅ |
| `IFailureRepository`, `IDeadLetterRepository`, `ISyncRunRepository`, `ISyncCheckpointStore` byte-identical | ✅ |
| Schema unchanged, no new migration | ✅ |
| Hangfire config, queues, retry policy, recurring jobs unchanged | ✅ |
| Other modules (`FakeSyncModule`, `FakePipelineSyncModule`, `FailingPipelineSyncModule`, `CancellingPipelineSyncModule`, `StudentSyncModule`) untouched | ✅ |
| Clean Debug build, `TreatWarningsAsErrors=true`, 0/0 | ✅ |
| Phase 5 hardening's concurrency lock, batch-size guard, race-safe writer, bounded warnings, audit retention — all preserved | ✅ |

---

## 10. Production-Readiness Recheck

| Dimension | Status | Notes |
|---|---|---|
| Replay visibility | ✅ | Five explicit replay-oriented log lines per retry; metrics line carries `Attempt`/`ReplayDetected`/`ReplayReason` |
| Determinism of failure verification | ✅ | Correlation-scoped state; mode survives Hangfire serialization; reset endpoint enables clean reruns |
| Test reproducibility | ✅ | Two scenarios reproducible at any time via admin endpoints |
| Operational diagnosability | ✅ | Audits read as a narrative; SQL state-checks confirm at every stage |
| No new infrastructure | ✅ | One additive context field, one new attribute on metrics, one new enum, one wrapper writer, one renamed admin endpoint |
| Replay-safe philosophy preserved | ✅ | Writer behavior unchanged; replay is allowed and observed, not blocked |
| Hangfire-driven execution preserved | ✅ | All retries still routed through Hangfire's machinery |

---

## 11. Phase Exit Checklist

- [x] `SyncContext.Attempt` flows from executor through pipeline to metrics
- [x] Pipeline start-log emits "Pipeline replay detected" only when Attempt > 1
- [x] Pipeline metrics line includes `Attempt`, `ReplayDetected`, `ReplayReason`
- [x] `CorrelationScopedFailingStudentWriter` replaces `OneShotFailingStudentWriter`
- [x] `FailureInjectionMode` enum with `AfterBatch1Commit` and `BeforeCheckpointSave`
- [x] Module reads mode from `Metadata.Tags`; survives Hangfire retries
- [x] Module emits "Replay verification starting/succeeded" assertion logs
- [x] Admin trigger accepts `failureMode` query parameter
- [x] `POST /admin/reset-failure-injection` clears correlation-scoped state
- [x] Scenario 1 — Retry Replay: runtime-verified end-to-end
- [x] Scenario 2 — Checkpoint Lag Replay: runtime-verified via paired-log narrative
- [x] Success-path regression: `Attempt=1 ReplayDetected=False` on a fresh students tick
- [x] No public API breakage (`Attempt` is additive with default value)
- [x] No new infrastructure / no schema change / no new migration
- [x] Audit document produced

---

**Phase 5 hardening refinement complete. Stopping per instruction.**
