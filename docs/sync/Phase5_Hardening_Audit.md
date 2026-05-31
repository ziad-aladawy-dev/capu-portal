# Phase 5 — Hardening Audit (Concurrency, Replay Safety, Operational Guards)

**Date:** 2026-05-29
**Status:** Completed (runtime-verified end-to-end)
**Spec source:** Phase 5 hardening brief
**Scope:** Stabilization only. No new project, no new infrastructure, no broker/outbox/CQRS/MediatR/distributed transaction, no schema redesign. `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts preserved.

---

## 1. Improvements Summary

| # | Concern | Outcome |
|---|---|---|
| 1 | Concurrent module execution | `PerModuleDisableConcurrencyAttribute` filter; runtime-verified two triggers ran strictly sequentially |
| 2 | Batch-size safety | `StudentSyncOptionsValidator` + `ValidateOnStart`; host refuses to start with `BatchSize > 1000` |
| 3 | Replay-safe writer enforcement | Documented in `IRecordWriter<>` XML; `StudentWriter` doc-comment formalizes guarantees |
| 4 | Read-then-write race | `StudentWriter` catches `DbUpdateException` on unique violation → clear tracker → retry once |
| 5 | Warning-aggregation explosion | Pipeline caps `MaxDistinctWarnings = 100` with overflow bucket; `RecordWarning` helper |
| 6 | Duplicate-skip observability | Metrics line gains `Extracted`, renames `Skipped → IdempotencySkipped`; `Extracted = IdempotencySkipped + ValidationFailed + Processed` |
| 7 | Audit retention readiness | `docs/sync/SyncAuditRetention.md` — strategy/windows/triggers; no scheduler implemented |
| 8 | Partial-success replay verification | `PartialFailingStudentSyncModule` + `OneShotFailingStudentWriter` — runtime-verified replay safety on the real `StudentWriter` |

---

## 2. Code Changes

### Sync.Abstractions
- `Pipeline/IRecordWriter.cs` — XML doc-comment formalizes:
  - Writers MUST be idempotent on the external merge key.
  - The runtime MAY replay already-committed batches (retries, post-commit checkpoint failure, host crash).
  - Writers MUST catch unique-constraint races and converge via re-read + retry.

### Sync.Infrastructure
- `Filters/PerModuleDisableConcurrencyAttribute.cs` **new** — `JobFilterAttribute` + `IServerFilter`; acquires Hangfire's `IStorageConnection.AcquireDistributedLock(resource = "sync-module:{moduleName}:{direction}", timeoutSeconds)` on `OnPerforming`; disposes on `OnPerformed`.
- `Execution/SyncModuleExecutor.cs` — `[PerModuleDisableConcurrency(timeoutSeconds: 3600)]` added above `[AutomaticRetry]`. No change to method body. Different modules and different directions of the same module still run in parallel.
- `Pipeline/SyncPipeline.cs`:
  - Renamed `skipped` → `idempotencySkipped`; added `extracted` counter at batch-start.
  - Metrics line shape changed to include both:
    ```
    Pipeline metrics. Module=… Batches=… Extracted=… IdempotencySkipped=… ValidationFailed=… Processed=… DistinctWarnings=…
                       ExtractionMs=… MappingMs=… ValidationMs=… WritingMs=… TotalMs=…
    ```
  - New `RecordWarning(...)` private helper; `MaxDistinctWarnings = 100`; overflow bucket `"(further warning categories suppressed)"`.
  - Cancellation + failure logs also include `Extracted` for parity.

### Sync.Student
- `Configuration/StudentSyncOptionsValidator.cs` **new** — `IValidateOptions<StudentSyncOptions>`; rejects empty connection string, `BatchSize <= 0`, `BatchSize > 1000`.
- `DependencyInjection/StudentSyncServiceCollectionExtensions.cs` — registers the validator + `services.AddOptions<>().Bind().ValidateOnStart()`.
- `Writing/StudentWriter.cs`:
  - Constructor now also takes `ILogger<StudentWriter>`.
  - Body refactored into `UpsertOnceAsync` + a 2-attempt retry loop.
  - On `DbUpdateException` whose inner `SqlException` is **2627** or **2601** (unique-constraint / unique-index violation), the writer clears the change tracker and retries the upsert once. The retry's `ToDictionaryAsync` re-reads existing rows, so the now-existing row is reached via the update path.
  - XML doc-comment documents replay-safety, race-safety, and external-wins.
- `csproj` — added `Microsoft.Data.SqlClient 5.2.2` (needed to inspect `SqlException.Number`).

### Sync.Host
- `Modules/PartialFailingStudents/OneShotFailingStudentWriter.cs` **new** — wraps `StudentWriter`; uses `static int _callCount` + `static int _hasFailedOnce`; throws on call #2 once per process; `Reset()` + `Snapshot()` static APIs for the admin endpoint.
- `Modules/PartialFailingStudents/PartialFailingStudentSyncModule.cs` **new** — DI-resolves real `StudentExtractor`/`StudentMapper`/`StudentValidator` + real `StudentWriter`, wraps the writer in `OneShotFailingStudentWriter`; pipeline always replays full extract (`CurrentCheckpoint = null`).
- `Program.cs` — registers `PartialFailingStudentSyncModule` as `ISyncModule`; adds `POST /admin/reset-partial-failing` admin endpoint.

### Documentation
- `docs/sync/SyncAuditRetention.md` **new** — operational retention strategy: tables, growth model, recommended windows, future cleanup mechanism (Hangfire recurring job, batched deletes, config-gated, kill-switched), implementation triggers. **No scheduler code.**

### NOT changed
- `Sync.Persistence/{Migrations, Entities, Configurations, Context}` — no schema change.
- `Sync.Abstractions/Models/*`, `Pipeline/{ISyncPipeline, SyncPipelineRequest, IDataExtractor, IRecordMapper, IRecordValidator}` — public surface unchanged.
- `Sync.Infrastructure/{Dispatching, Logging, Modules, DependencyInjection, Configuration}` — untouched.
- `Sync.Student/{Domain, Context, Source, Extraction, Mapping, Validation, Modules}` — untouched (writer changes only).
- `FakeSyncModule`, `FakePipelineSyncModule`, `FailingPipelineSyncModule`, `CancellingPipelineSyncModule` — all untouched. Picked up the new metric shape automatically.

---

## 3. Runtime Verification — Success Path (regression)

After Phase 5 baseline state was reset (checkpoint deleted, `sync_student.students` truncated):

```
POST /admin/trigger/students?direction=Pull
→ {"module":"students","jobId":"350","correlationId":"54ea3f0b-..."}
```

```
Pipeline metrics. Module=students Batches=2 Extracted=50 IdempotencySkipped=0 ValidationFailed=2 Processed=48 DistinctWarnings=1 ExtractionMs=2 MappingMs=0 ValidationMs=1 WritingMs=140 TotalMs=147
Sync execution succeeded. Module=students Direction=Pull Processed=48 Failed=0 Duration=00:00:00.1478866
```

- New metric `Extracted=50` present.
- `IdempotencySkipped=0` (renamed from `Skipped`).
- Equation holds: `50 = 0 + 2 + 48`.
- `DistinctWarnings=1` (the two invalid emails collapse to a single normalized message).
- Total runtime unchanged within noise.

✅ **Success path regression-clean. No behavior change to baseline modules.**

---

## 4. Runtime Verification — Concurrency Lock

Two consecutive admin triggers of `cancelling-pipeline` (which takes ~12 s):

```
T+0.0s   POST /admin/trigger/cancelling-pipeline    → jobId=357 correlationId=a4b0c320-...
T+1.0s   POST /admin/trigger/cancelling-pipeline    → jobId=358 correlationId=072e3cb4-...
```

Log lines (ordered by line number):

```
4611  Sync execution started.   Module=cancelling-pipeline ... JobId=357
… (12 s) …
5055  Sync execution succeeded. Module=cancelling-pipeline … Processed=40 Duration=00:00:12.2048498   (JobId=357)
5057  Sync execution started.   Module=cancelling-pipeline ... JobId=358
… (12 s) …
6535  Sync execution succeeded. Module=cancelling-pipeline … Processed=40 Duration=00:00:12.2562016   (JobId=358)
```

SQL:

```
SELECT HangfireJobId, Status, StartedAt, CompletedAt FROM sync.runs WHERE HangfireJobId IN ('357','358');

357   2 (Succeeded)   2026-05-29 13:14:57.374 +00:00   2026-05-29 13:15:09.595 +00:00
358   2 (Succeeded)   2026-05-29 13:15:09.605 +00:00   2026-05-29 13:15:21.874 +00:00
```

- Job 358 starts **10 ms** after job 357 completes — the Hangfire distributed lock blocked it for ~11 s.
- No overlap.
- Both jobs succeed.

✅ **Same (module, direction) cannot execute concurrently.** Different modules (e.g., `fake-pipeline` ticked freely during this window) and different directions of the same module are unaffected.

---

## 5. Runtime Verification — Batch-Size Guard

```
$ Sync__Student__BatchSize=2000  dotnet run --no-build ...
```

Startup output (relevant tail):

```
Microsoft.Extensions.Options.OptionsValidationException:
  Sync:Student:BatchSize (2000) must be <= 1000 to stay within SQL Server's
  ~2100 parameter limit per command.
Unhandled exception. (host does NOT start)
```

✅ **Invalid config rejected fail-fast.** Valid config (the default `BatchSize=25`) starts normally — exercised by every other run in this audit.

Additional zero/negative case verified at the same boundary (`BatchSize=0` → `must be > 0`).

---

## 6. Runtime Verification — Partial-Success Replay

### Setup
- `sync.checkpoints` for `partial-failing-students` and `students` deleted.
- `sync_student.students` truncated (48 rows → 0).
- `OneShotFailingStudentWriter` counters reset via `POST /admin/reset-partial-failing`.

### Attempt 1 — partial commit then injected throw

```
POST /admin/trigger/partial-failing-students?direction=Pull
→ jobId=377  correlationId=22e9eca6-...

Batches:
  1) records 1-20 (incl. invalid #10 #20 dropped by validator) → 18 written (call #1)
  2) records 21-40                                              → throws (call #2, one-shot)
```

```
SELECT COUNT(*) FROM sync_student.students;                  → 18
SELECT COUNT(*) FROM sync.failures WHERE CorrelationId=...;  →  1
```

Log evidence:

```
System.InvalidOperationException: OneShotFailingStudentWriter: deterministic partial-failure injection on batch 2.
   at OneShotFailingStudentWriter.UpsertBatchAsync(...) in OneShotFailingStudentWriter.cs:line 36
SyncExecutionException: OneShotFailingStudentWriter: deterministic partial-failure injection on batch 2.
  ---> SyncExecutionException: ...
```

### Retry — full replay converges

```
POST /admin/requeue/377  → {"jobId":"377","requeued":true}
```

Attempt 2 (replay) — same correlation id, fresh writer instance:
- Batch 1: records 1-20 — writer call #3 → no fail; existing rows updated in place (external-wins; no duplicates).
- Batch 2: records 21-40 — call #4 → 20 written.
- Batch 3: records 41-50 — call #5 → 10 written.

```
Pipeline metrics. Module=partial-failing-students Batches=3 Extracted=50 IdempotencySkipped=0 ValidationFailed=2 Processed=48 DistinctWarnings=1 ExtractionMs=0 MappingMs=0 ValidationMs=0 WritingMs=57 TotalMs=59
```

### Final SQL

```
SELECT COUNT(*)             FROM sync_student.students;                                → 48
SELECT COUNT(DISTINCT ExternalStudentId) FROM sync_student.students;                   → 48
SELECT Status, AttemptCount, LastError FROM sync.runs WHERE CorrelationId='22e9eca6-...';
  2 (Succeeded)   2 (attempts)   NULL
SELECT COUNT(*) FROM sync.dead_letters WHERE CorrelationId='22e9eca6-...';            → 0
```

✅ **Replay safe.**
- Final logical state correct.
- No duplicate students.
- No dead-letter.
- `AttemptCount=2` proves the retry path executed.
- `LastError=NULL` because the success path clears it on `MarkSucceeded`.

This exercises the real `StudentWriter` end-to-end against the real `sync_student.students` schema — the unique index on `ExternalStudentId` is what makes the replay safe.

---

## 7. Runtime Verification — Race-Safe Writer

The unique-constraint-race code path was **not** exercised at runtime — producing two concurrent partial writes against the same `ExternalStudentId` requires either:
- two independent processes against the same DB, or
- intentionally racing two Hangfire workers (the Phase 5-hardening concurrency lock now prevents this for the same module).

**Code-verified only.** The path is:

```csharp
catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxAttempts)
{
    _db.ChangeTracker.Clear();
    _logger.LogInformation("StudentWriter unique-constraint race detected; converging via retry. ...");
}
// loop body runs once more; second pass observes the now-existing row → update path
```

`IsUniqueConstraintViolation` matches `SqlException.Number` 2627 / 2601 — the standard SQL Server unique-constraint / unique-index violation codes. The path is reachable from any production scenario where two writers race (e.g., the same student arrives from two different external feeds concurrently). The fix converges deterministically because:
1. First write committed → unique index now contains the row.
2. Our `SaveChangesAsync` fails with 2627/2601.
3. `ChangeTracker.Clear()` discards our pending entity additions.
4. Loop body re-executes; `ToDictionaryAsync` now finds the row; the upsert takes the update branch.
5. Second `SaveChangesAsync` succeeds (or, if a true persistent conflict exists, propagates).

The replay verification in §6 demonstrates the same convergence property along the **idempotent re-read** path, which is the dominant production scenario.

---

## 8. Runtime Verification — Warning Aggregation Bound

Defense-in-depth path. Phase 4 final refinement already aggregated identical messages. Phase 5 hardening adds the hard cap (`MaxDistinctWarnings = 100`).

The students-sync tick produced exactly **one** distinct warning category (`"Email is required."`) across two invalid records — matching the normalized-category contract documented in `IRecordValidator<>`. The aggregation behavior:

- Two validation rejections × same message → 1 distinct key.
- `DistinctWarnings=1` in metrics line.
- `SyncResult.Warnings` returned: `["Email is required. (x2)"]`.

The cap itself is not runtime-triggerable without an intentionally misbehaving validator. **Code-verified** that `RecordWarning(...)` enforces the cap and routes overflow into the bucket key `"(further warning categories suppressed)"`.

---

## 9. Replay-Safe Contract — Explicit Documentation

`IRecordWriter<>` XML now states (excerpt):

> **Implementations MUST be idempotent on the external merge key.** The sync runtime reserves the right to replay an already-committed batch when:
> - Hangfire retries an attempt after a transient failure in a later batch.
> - The checkpoint save fails after a successful pipeline (run is replayed next tick).
> - The host crashes between commit and checkpoint advance.
> Writers must treat a re-presented record as an update of the existing internal row, keyed by its stable external identifier. Duplicate logical rows are never acceptable.
>
> Concurrency: writers should be prepared for the unique-constraint race where two workers both observed "row does not exist" and both attempted an insert. The recommended pattern is to catch the resulting `DbUpdateException` and converge via re-read + retry once, NOT to silently swallow it.

`StudentWriter` XML documents external-wins + the specific retry behavior.

This is the **public, source-controlled** contract for Phase 6+ module authors. A future module reusing the pipeline now has clear guidance on what their writer must guarantee.

---

## 10. Audit-Retention Strategy Summary

See `docs/sync/SyncAuditRetention.md` for the full strategy. Highlights:

| Table | Hot window | Cold archive | Hard delete |
|---|---|---|---|
| `sync.runs` (Succeeded) | 30 days | optional | 90 days |
| `sync.runs` (Failed/DeadLettered/Cancelled) | 90 days | mandatory | 365 days |
| `sync.failures` | 90 days | tied to parent | tied to parent |
| `sync.dead_letters` | 365 days | mandatory | 730 days |
| `sync.checkpoints` | never deleted | — | — |

Mechanism plan (NOT implemented in Phase 5):
- Hangfire recurring job, daily off-peak.
- `DELETE TOP (N)` in batches of 5000 to avoid lock escalation.
- Children before parents: `failures` → `dead_letters` → `jobs` → `runs`.
- Config-gated + kill-switched.
- Implementation triggers: > 5M rows in `sync.runs`, dashboard p99 > 500 ms, or compliance requirement.

---

## 11. Verification Classification

### VERIFIED at runtime
- New metric shape (`Extracted`, `IdempotencySkipped`) on a fresh students-sync tick.
- Concurrency lock: 2 ticks of `cancelling-pipeline` ran strictly sequentially (10 ms gap).
- Batch-size guard: `OptionsValidationException` at startup with `BatchSize=2000`.
- Partial-success replay: attempt 1 partially commits 18 rows → retry converges to 48 rows / 48 distinct external ids / 0 dead-letters / `AttemptCount=2`.

### CODE-VERIFIED ONLY
- `StudentWriter` unique-constraint-race handler (would require two concurrent processes against the same DB; the new concurrency lock already prevents the in-process case).
- `MaxDistinctWarnings = 100` overflow bucket (would require an intentionally misbehaving validator).
- Audit-retention cleanup mechanism (documented; no scheduler shipped per spec).

### CARRIED DEBT (unchanged)
- `AllowAllDashboardAuthorizationFilter` fronts both `/hangfire/*` and the admin trigger/requeue/cancel/reset endpoints. **Dev-only. Must be replaced before non-local deploy.**
- Dev SQL password in `appsettings.Development.json` — move to user-secrets.
- `SyncRunMetadata.Tags` exposed as `IReadOnlyDictionary<,>` but backed by mutable `Dictionary<,>`.
- `SyncRunMetadata.TriggeredBy` as free-form string.
- Repo-vs-spec layout naming (`5.Application` exists; spec recommends `6.Application`).
- Pre-existing unrelated test build error in `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`.
- `OneShotFailingStudentWriter` resets only via admin endpoint within process lifetime — verification artifact, not production code.

### DEVIATIONS
- **None vs. Phase 5 hardening brief.** All eight required outcomes met.
- Spec listed `[DisableConcurrentExecution(timeoutInSeconds: 3600)]` as a *preferred* implementation. Hangfire OSS's built-in attribute uses a method-level mutex which would prevent ALL `SyncModuleExecutor.ExecuteAsync` calls from running concurrently — too coarse. The custom `PerModuleDisableConcurrencyAttribute` uses the same Hangfire `IStorageConnection.AcquireDistributedLock` primitive with a `(module, direction)`-scoped resource key, satisfying the spec's "preserve parallelism between DIFFERENT modules" requirement.

---

## 12. Boundary & Regression Verification

| Check | Result |
|---|---|
| `ISyncModule` / `ISyncDispatcher` / `SyncContext` / `SyncResult` byte-identical | ✅ |
| `IRecordWriter<>` interface signature byte-identical (XML-doc change only) | ✅ |
| `ISyncPipeline` / `SyncPipelineRequest<,>` signature byte-identical | ✅ |
| No new project | ✅ |
| No new schema, no new migration | ✅ |
| No new infrastructure system (event bus, broker, outbox, CQRS, MediatR, distributed TX) | ✅ |
| Hangfire connection-string + queue configuration unchanged | ✅ |
| `[AutomaticRetry]` policy unchanged | ✅ |
| `BackgroundJobServerOptions.CancellationCheckInterval` unchanged | ✅ |
| Pre-existing modules (`FakeSyncModule`, `FakePipelineSyncModule`, `FailingPipelineSyncModule`, `CancellingPipelineSyncModule`, `StudentSyncModule`) unchanged | ✅ |
| Clean Debug build, `TreatWarningsAsErrors=true`, 0/0 | ✅ |

---

## 13. Production-Readiness Recheck

| Dimension | Status | Notes |
|---|---|---|
| Same module cannot execute concurrently | ✅ | Distributed-lock-based, per-(module,direction) |
| Batch-size overflow prevented | ✅ | Fail-fast at startup |
| Replay-safe behavior documented + verified | ✅ | XML contract + partial-success runtime test |
| Duplicate insert races converge safely | ✅ (code) | Retry-once on unique-constraint violation |
| Warning aggregation bounded | ✅ | Per-message dedup + 100-category cap + overflow bucket |
| Duplicate-skip metrics observable | ✅ | New `Extracted` + `IdempotencySkipped` fields |
| Audit retention strategy documented | ✅ | Strategy doc + implementation triggers |
| Partial-success retry verified | ✅ | 18 → 48 with no duplicates |
| No public API breakage | ✅ | All public signatures byte-identical |
| No new infrastructure introduced | ✅ | One new attribute + one new validator + new docs |

**Verdict:** Phase 5 baseline is now hardened against the seven operational risk classes called out in the brief. The system remains Hangfire-driven and small. Production-deployable for the Students.Sync module pending the carried debt items (dashboard auth + secrets handling).

---

## 14. Phase Exit Checklist

- [x] `PerModuleDisableConcurrencyAttribute` implemented, applied, runtime-verified
- [x] `StudentSyncOptionsValidator` enforces `0 < BatchSize <= 1000`, runtime-verified fail-fast
- [x] `StudentWriter` handles unique-constraint race (`DbUpdateException` → clear tracker → retry once)
- [x] `IRecordWriter<>` XML formalizes replay-safe + race-safe + idempotent contract
- [x] `SyncPipeline` metrics line includes `Extracted`, renames `Skipped → IdempotencySkipped`
- [x] `SyncPipeline.MaxDistinctWarnings = 100` cap with overflow bucket
- [x] `PartialFailingStudentSyncModule` + `OneShotFailingStudentWriter` + `POST /admin/reset-partial-failing`
- [x] Partial-success replay runtime-verified to converge with no duplicates, no dead-letter, `AttemptCount=2`
- [x] `docs/sync/SyncAuditRetention.md` strategy + windows + future-cleanup design
- [x] No public API breakage
- [x] No new infrastructure
- [x] Audit document produced

---

**Phase 5 hardening complete. Stopping per instruction. Phase 6 (Push Sync — Internal → External) is next when invoked.**
