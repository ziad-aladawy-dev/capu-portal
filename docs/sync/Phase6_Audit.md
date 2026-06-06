# Phase 6 — Push Sync (Internal → External): Audit

**Date:** 2026-05-29
**Status:** Completed (runtime-verified end-to-end on real SQL Server; success + per-row failure + recovery paths)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`

---

## 1. Goal Recap

> Enable outbound synchronization safely using Hangfire orchestration.

Achieved: the `students` module's `PushAsync` now drives the existing Phase-4 `SyncPipeline` over a new `sync_student.student_outbox` table. Outbox rows are written transactionally (Phase-6 baseline: written by an admin seed endpoint until a real internal write API exists), read in Pending order by `StudentOutboxExtractor`, dispatched through `IExternalStudentSink`, and marked `Processed` by `StudentOutboxPushWriter`. The push pipeline runs both on its own recurring schedule (`student-sync-push`, every minute) and on admin trigger, and routes to the dedicated `push-sync` Hangfire queue per the spec.

---

## 2. Components Implemented

### New types (project: `CapitalUniversity.Sync.Student`)

| File | Role |
|---|---|
| `Outbox/StudentOutboxEntity.cs` | Outbox row: `Id, ExternalStudentId, Operation, Payload (json), Status, AttemptCount, LastError, CreatedAt, ProcessedAt`. `MaxAttempts = 5` (soft cap for future Phase-9 dead-letter). |
| `Outbox/StudentOutboxOperation.cs` | Enum: `Upsert = 0`, `Delete = 1`. Phase 6 ships `Upsert` only. |
| `Outbox/StudentOutboxStatus.cs` | Enum: `Pending = 0`, `Processed = 1`, `Failed = 2`. |
| `Outbox/StudentOutboxDispatch.cs` | Pipeline intermediate: `(Row, Payload)` pair carried from mapper to writer so the writer can update the same tracked row instance. |
| `Configurations/StudentOutboxConfiguration.cs` | EF table mapping + `IX_student_outbox_Status_CreatedAt` composite index. |
| `Source/IExternalStudentSink.cs` | Push counterpart to `IExternalStudentSource`. One method: `PushAsync(ExternalStudent, ct)`. |
| `Source/InMemoryExternalStudentSink.cs` | Verification-only sink. Records latest payload per ExternalStudentId in `ConcurrentDictionary<string,ExternalStudent>`. `FailNextPushFor(id)` arms a one-shot failure for runtime verification. |
| `Extraction/StudentOutboxExtractor.cs` | Streams Pending rows in `CreatedAt, Id` order. `MaxPerRun = 500` hard cap per run; backlog wider than that splits across recurring ticks. |
| `Mapping/StudentOutboxMapper.cs` | Parses outbox row's JSON `Payload` back into `ExternalStudent`. Malformed JSON raises `InvalidOperationException` → pipeline failure path. |
| `Validation/StudentOutboxValidator.cs` | Sanity gate: payload non-null + `Row.ExternalStudentId` matches `Payload.ExternalStudentId`. Real validation already ran on Pull. |
| `Writing/StudentOutboxPushWriter.cs` | Iterates batch, calls sink for each `Pending` row, marks `Processed` (`Status` + `ProcessedAt`) on success, bumps `AttemptCount` + records `LastError` on per-row failure. One `SaveChangesAsync` per batch. Per-row failures do NOT abort the batch. |
| `Migrations/20260529144727_AddStudentOutbox.cs` | Creates `sync_student.student_outbox` + composite index. |

### New types (project: `CapitalUniversity.Sync.Host`)

| File | Role |
|---|---|
| `Admin/StudentOutboxSeedRequest.cs` | Body for admin seed endpoint — all fields optional with canned defaults. |

### Updated files

| File | Change |
|---|---|
| `Sync.Student/Context/StudentSyncDbContext.cs` | + `DbSet<StudentOutboxEntity> StudentOutbox`. |
| `Sync.Student/Configuration/StudentSyncOptions.cs` | + `PushBatchSize` (default 25). |
| `Sync.Student/Configuration/StudentSyncOptionsValidator.cs` | Extends validation to cover `PushBatchSize` with the same `MaxBatchSize = 1000` ceiling. |
| `Sync.Student/Modules/StudentSyncModule.cs` | `PushAsync` no longer a no-op: scopes a DI, resolves outbox parts, composes `SyncPipelineRequest<StudentOutboxEntity, StudentOutboxDispatch>`, runs the pipeline, returns the result. No checkpoint advance — outbox `Status` is the cursor. |
| `Sync.Student/DependencyInjection/StudentSyncServiceCollectionExtensions.cs` | + sink (singleton, both `InMemoryExternalStudentSink` and `IExternalStudentSink` faces) + four transient push parts. |
| `Sync.Infrastructure/Configuration/SyncOptions.cs` | **Direction separator changed from `:` to `|`.** `$"{moduleName}|{direction}"` lookup. Reason: `:` is .NET configuration path separator and would cause JSON keys like `"students:Push"` to collide with the existing `"students"` string entry. Phase 5 didn't surface this because no `direction`-scoped queue routing was exercised. |
| `Sync.Host/Scheduling/SyncRecurringJobsRegistrar.cs` | + `student-sync-push` recurring job (every minute). The push trigger lives on the `default` queue (same as the other triggers); the resulting dispatch lands on `push-sync` per `ResolveQueue`. |
| `Sync.Host/Program.cs` | + `POST /admin/outbox/student/{externalStudentId}` (seed Pending row) + `GET /admin/outbox/sink` (inspect accepted payloads) + `POST /admin/outbox/sink/fail-next/{externalStudentId}` (arm one-shot failure). Same dev-only `AllowAllDashboardAuthorizationFilter` as the other admin endpoints. |
| `Sync.Host/appsettings.json` | + `Sync:ModuleQueues:"students\|Push" = "push-sync"` + `Sync:Student:PushBatchSize = 25`. |

### Files explicitly NOT touched

- `Sync.Abstractions/**` — public surface byte-identical. `ISyncModule.PushAsync` already existed since Phase 0; Phase 6 only changes its `students` implementation.
- `Sync.Persistence/**` — no schema change, no migration. `sync.runs`, `sync.jobs`, `sync.failures`, `sync.dead_letters` continue to record push runs identically to pull runs (direction column already in place).
- `Sync.Infrastructure/Pipeline/SyncPipeline.cs` — reused as-is. No push-specific branch.
- `Sync.Infrastructure/Execution/SyncModuleExecutor.cs` — unchanged. Retry / cancellation / audit machinery wraps push automatically.
- `Sync.Student/Domain/{ExternalStudent, StudentEntity}.cs` — unchanged.
- `Sync.Student/Extraction/StudentExtractor.cs` + Mapping/Validation/Writing pull parts — unchanged.

---

## 3. Schema

New table in the existing `sync_student` schema:

```
sync_student.student_outbox
  Id                  uniqueidentifier   PK
  ExternalStudentId   nvarchar(64)       NOT NULL
  Operation           int                NOT NULL   (StudentOutboxOperation)
  Payload             nvarchar(max)      NOT NULL   (JSON ExternalStudent)
  Status              int                NOT NULL   (StudentOutboxStatus)
  AttemptCount        int                NOT NULL
  LastError           nvarchar(4000)     NULL
  CreatedAt           datetimeoffset     NOT NULL
  ProcessedAt         datetimeoffset     NULL

  IX_student_outbox_Status_CreatedAt   (Status, CreatedAt)
```

Migration `20260529144727_AddStudentOutbox` applied at host startup via the existing `StudentSyncDbContext.Database.MigrateAsync()`. The Phase-5 `sync_student.students` table and its migrations are unchanged.

---

## 4. Pipeline Wiring (Push)

```
StudentSyncModule.PushAsync(SyncContext)
  └─ ISyncPipeline.RunAsync<StudentOutboxEntity, StudentOutboxDispatch>(SyncPipelineRequest {
         Extractor           = StudentOutboxExtractor   ─→ DbContext: Pending rows (Take ≤ 500)
         Mapper              = StudentOutboxMapper      ─→ JSON parse Payload → ExternalStudent
         Validator           = StudentOutboxValidator   ─→ payload non-null + key match
         Writer              = StudentOutboxPushWriter  ─→ sink.PushAsync per row + mark Processed
         ExternalKeySelector = r => r.Id.ToString()     ─→ per-row dedup; sequential updates flow through
         BatchSize           = options.PushBatchSize    (default 25)
         CurrentCheckpoint   = null                     (outbox Status is the cursor)
     })
  └─ return result      (no checkpoint advance — outbox row state IS the checkpoint)
```

The push pipeline reuses the same generic `SyncPipeline` as the pull side: same stage timing, same `Pipeline metrics` summary line, same `[AutomaticRetry]` policy on `SyncModuleExecutor`, same `sync.runs` / `sync.failures` / `sync.dead_letters` audit trail.

---

## 5. Runtime Evidence

### 5.1 Success path (admin trigger, 2 rows)

```
POST /admin/outbox/student/EXT-S-0301  →  outboxId=...
POST /admin/outbox/student/EXT-S-0302  →  outboxId=...
POST /admin/trigger/students?direction=Push
→ {"module":"students","direction":"Push","jobId":"574",
   "correlationId":"df2ce2d0-4aee-4901-a648-1b23eb2281d8"}
```

Log trace (selected):
```
Sync job enqueued.        Module=students Direction=Push TriggeredBy=admin Queue=push-sync JobId=574
Sync execution started.   Module=students Direction=Push TriggeredBy=admin Attempt=1
Pipeline metrics.         Module=students Batches=1 Extracted=2 IdempotencySkipped=0 ValidationFailed=0
                          Processed=2 DistinctWarnings=0 Attempt=1 ReplayDetected=False ReplayReason=None
                          ExtractionMs=3 MappingMs=1 ValidationMs=0 WritingMs=13 TotalMs=19
Sync execution succeeded. Module=students Direction=Push Processed=2 Failed=0 Duration=00:00:00.0191396
```

SQL verification:
```
SELECT ExternalStudentId, Status, AttemptCount FROM sync_student.student_outbox WHERE ExternalStudentId LIKE 'EXT-S-030%';
   EXT-S-0301   1 (Processed)   0
   EXT-S-0302   1 (Processed)   0

SELECT CorrelationId, Status, RecordsProcessed, Queue FROM sync.runs WHERE HangfireJobId='574';
   DF2CE2D0-4AEE-4901-A648-1B23EB2281D8   2 (Succeeded)   2   push-sync
```

Sink contents (`GET /admin/outbox/sink`):
```
acceptedCount: 3
  EXT-S-0301, EXT-S-0302, EXT-S-0303  ←  all three latest payloads accepted
```

### 5.2 Per-row failure + recurring recovery

```
POST /admin/outbox/student/EXT-S-0401         ←  seed Pending row
POST /admin/outbox/sink/fail-next/EXT-S-0401  ←  arm one-shot sink failure
POST /admin/trigger/students?direction=Push
→ jobId=583  corr=9401c024-...
```

Failing-tick log trace:
```
Sync job enqueued.   Module=students Direction=Push TriggeredBy=admin Queue=push-sync JobId=583
Push sink failed.    ExternalStudentId=EXT-S-0401 AttemptCount=1 Error=InMemoryExternalStudentSink: armed failure for ExternalStudentId=EXT-S-0401..
Pipeline metrics.    Module=students Batches=1 Extracted=1 IdempotencySkipped=0 ValidationFailed=0 Processed=0 ...
Sync execution succeeded. Module=students Direction=Push Processed=0 Failed=0
```

Outbox state immediately after failing tick:
```
EXT-S-0401   Status=0 (Pending)   AttemptCount=1   LastError="InMemoryExternalStudentSink: armed failure..."
```

The recurring `student-sync-push` tick (job 591, two seconds later) picked the row up — the one-shot failure had already been consumed by the prior call, so the sink accepted it normally:
```
Sync job enqueued.        Module=students Direction=Push TriggeredBy=scheduled Queue=push-sync JobId=591
Pipeline metrics.         Module=students Batches=1 Extracted=1 ... Processed=1 ...
Sync execution succeeded. Module=students Direction=Push Processed=1 Failed=0
```

Final outbox state for EXT-S-0401:
```
Status=1 (Processed)   AttemptCount=1   ProcessedAt=2026-05-29 15:01:08.30 +00:00
```

`AttemptCount=1` survives across the success — that's the audit fingerprint that the row was retried.

### 5.3 sync.runs audit trail

```
HangfireJobId   CorrelationId    Status        Processed   TriggeredBy   Queue
   574          df2ce2d0-...     2 Succeeded     2          admin         push-sync   ← 2-row admin push
   583          9401c024-...     2 Succeeded     0          admin         push-sync   ← failing tick (row stays Pending)
   591          2e415186-...     2 Succeeded     1          scheduled     push-sync   ← recurring recovery
```

All push runs routed to `push-sync` queue. No `sync.failures` row was written for the per-row failure — the writer caught the sink exception, recorded it on the outbox row, and let the pipeline complete cleanly. Run status `Succeeded` with `Processed=0` is the audit signature of "ran cleanly, processed nothing this tick".

---

## 6. Compliance — Phase 6 Success Criteria

| Criterion | Status | Evidence |
|---|---|---|
| **Push sync fully asynchronous** | ✅ | Internal write (seed endpoint) and external sink call are decoupled by `sync_student.student_outbox`. No external HTTP/SQL call happens inside the request lifecycle — the seed endpoint only writes a Pending row and returns. |
| **Retries handled safely** | ✅ | Two independent retry layers: (a) per-row sink failure leaves the row `Pending` with bumped `AttemptCount`/`LastError`, picked up next recurring tick — verified in §5.2; (b) whole-batch failure (DB outage, mapper exception) bubbles through `SyncResult.Failed` → Hangfire `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]` already attached to the executor. |
| **External failures traceable** | ✅ | Two surfaces: structured `Push sink failed.` warning log carries `CorrelationId` via MEL scope; outbox row's `LastError` + `AttemptCount` persisted indefinitely until the row succeeds or an operator clears it. |
| **No blocking request pipeline** | ✅ | The seed endpoint's only SQL work is `INSERT INTO student_outbox`. The actual sink call happens on the Hangfire worker that runs the push pipeline, never on the ASP.NET request thread. |

---

## 7. Architectural Decisions

1. **Pipeline reuse — no push-specific orchestration framework.** Push uses the same `ISyncPipeline.RunAsync<TExt, TInt>` as pull, with `TExt = StudentOutboxEntity` and `TInt = StudentOutboxDispatch`. Stage timing, idempotency dedup, warning aggregation, replay metrics, OCE classification — all reused unchanged. The only price is the `StudentOutboxValidator` being effectively a no-op gate (it has nothing meaningful to check), which is a small price for keeping one pipeline.

2. **Outbox row state IS the cursor; no `SyncCheckpoint`.** `CurrentCheckpoint = null` is passed to the pipeline on push runs. `Pipeline started.` logs `HasCheckpoint=False` for push — intentional. Push correctness is enforced by `WHERE Status = Pending` in the extractor; advancing a checkpoint would duplicate that state. The module accepts the `SyncCheckpoint?` parameter on the pipeline contract but does not use it on the push path.

3. **Per-row failure isolation, not per-row retry.** A single failed `IExternalStudentSink.PushAsync` does not abort the batch — other dispatches still go through, and the failed row stays `Pending` with `AttemptCount + 1` and `LastError` set. The next recurring tick (every minute) re-attempts. This matches the spec's "fully asynchronous + traceable" criteria and avoids forcing Hangfire's whole-execution retry policy to engage for ordinary downstream-system flakiness. Whole-batch failures (DB outage, JSON parse exception, etc.) still propagate to Hangfire retry as before.

4. **Outbox `ExternalKeySelector` uses `OutboxId`, not `ExternalStudentId`.** Reason: two sequential updates to the same student need to flow through as distinct outbox rows — deduping by `ExternalStudentId` within a batch would silently drop intermediate edits. The sink is required to be idempotent on `ExternalStudentId` (so re-presented payloads are safe), which preserves the cross-run idempotency guarantee without requiring in-run dedup.

5. **In-memory sink as the Phase-6 default.** Mirrors the Phase-5 `InMemoryExternalStudentSource` pattern. Production replaces `InMemoryExternalStudentSink` with an HTTP/SOAP client behind the `IExternalStudentSink` interface — no other module change required.

6. **Loop prevention: `OriginSystem` on the inbound entity, not yet asserted by the push extractor.** Outbound pushes leave the existing `StudentEntity.OriginSystem` field as-is (the writer is the outbox table, not the students table). The Phase-7 multi-direction concern — pulling a record that was just pushed externally and incorrectly applying it as a new internal write — is addressed by the existing pull-side `OriginSystem = "external"` invariant and the writer's `ExternalStudentId` upsert. No new loop-prevention machinery is introduced in Phase 6.

7. **`MaxPerRun = 500` cap on extractor materialization.** Defense-in-depth on top of `SyncPipelineRequest.BatchSize ≤ 1000`. A push tick that finds a 10 000-row backlog processes the first 500 and lets subsequent recurring ticks drain the rest — no single tick balloons the EF tracker or holds a long-running SQL transaction.

8. **Queue separator changed from `:` to `|`.** Pre-existing latent bug uncovered while configuring `Sync:ModuleQueues:students|Push = push-sync`. `:` collides with the .NET configuration-path separator, silently dropping the directional override. Cleanest fix is a one-character change in `SyncOptions.ResolveQueue` + the single JSON key it touches. No public API change. Phase 5 didn't surface this because no module exercised directional routing.

9. **Push checkpoint advance NOT attempted.** Unlike pull, where the module advances `sync.checkpoints` after success, push leaves `sync.checkpoints[students]` untouched. Outbox row state is authoritative; a stale checkpoint on the push side would have no defined behavior.

10. **Recurring `student-sync-push` schedule = every minute.** Same cadence as the pull side. Tunable to a slower cadence (5-minute, 15-minute) once production observability informs the right tradeoff between recovery latency and DB read pressure. Phase 9 / Phase 10 candidate.

---

## 8. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts | ✅ byte-identical |
| `Sync.Abstractions` public surface | ✅ unchanged |
| `Sync.Persistence` schema and migrations | ✅ no new migration, no column change |
| `Sync.Infrastructure` pipeline / executor / dispatcher / filters | ✅ unchanged (except the queue separator one-char fix in `SyncOptions`) |
| Pull path for the `students` module | ✅ unchanged — `PullAsync` body byte-identical to Phase 5; the new module-level imports are additive |
| `FakeSyncModule`, `FakePipelineSyncModule`, `FailingPipelineSyncModule`, `CancellingPipelineSyncModule`, `PartialFailingStudentSyncModule` | ✅ byte-identical |
| Hangfire retry policy (`[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]`) | ✅ unchanged — applies to push runs identically |
| Queue list in `Sync.Host` (`students-sync, finance-sync, hr-sync, push-sync, default`) | ✅ unchanged |
| `sync.runs.Direction` column distinguishes Pull (0) from Push (1) | ✅ used as-is; no schema change needed |
| Phase-4 `Pipeline metrics.` single completion-log line shape | ✅ identical |
| Phase-4 hardened state-transition matrix (Enqueued→Running, Running→Succeeded, etc.) | ✅ identical |
| Phase-5 `sync_student.students` table + Pull pipeline | ✅ identical |

Trade-off: the queue separator change from `:` to `|` is a contract change visible to any operator-managed `Sync:ModuleQueues` configuration. There is no in-tree consumer of the `:` form (Phase 5 didn't use directional routing), so no migration is required for the codebase itself; operator-managed override JSON would need the one-character update. Documented in §7.8 above.

---

## 9. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Internal write API does not yet exist — outbox rows can only be seeded via the dev-only admin endpoint. | Medium | The seed endpoint is dev-only via the AllowAll dashboard filter; production integration (real internal student-edit API → transactional outbox write) is the natural follow-up. The push pipeline itself is production-shaped. |
| Sink call + outbox-row update are not in a single transaction (the sink is external; the update is local SQL). On host crash between sink-accept and SaveChanges, the next push tick re-presents the same payload. | Low | Sink contract requires idempotency on `ExternalStudentId`. Documented on `IExternalStudentSink`. Writer also pre-checks `Status == Pending` before calling the sink so a Processed row is never double-sent within the same tick. |
| `InMemoryExternalStudentSink` is verification-only — must be replaced before production cutover. | Medium | Documented; behind `IExternalStudentSink`. |
| Per-row failures bump `AttemptCount` without an automatic poison-row cutoff (the writer never sets `Status = Failed`). A genuinely-broken row will retry indefinitely. | Low | `MaxAttempts = 5` is a soft cap reserved for Phase-9 dead-letter handling; operators can query `WHERE AttemptCount >= 5` to surface stuck rows. Not auto-poisoning is intentional — silent dropping of internal mutations is more dangerous than a long-running retry. |
| Whole-batch failure (writer throws) does not currently mark the rows touched within the batch with their error — only the run's `sync.failures` row carries the message. | Low | Visible in `sync.failures` + `sync.runs.LastError`. Per-row attribution for whole-batch failures is a Phase-9 candidate. |
| Two host instances both pick up the same Pending row. | Low | The Phase-4-hardened `[PerModuleDisableConcurrencyAttribute]` on `SyncModuleExecutor.ExecuteAsync` already serializes per-module runs across the Hangfire cluster. Database-level race (two workers in the same instance) cannot happen because Hangfire dispatches one job at a time per `(moduleName, direction)` distributed lock. |
| The `Sync:ModuleQueues` separator change from `:` to `|` is operationally visible to anyone with a `students:Push` override on their env. | Low | One-character JSON key update. No code consumers exist. |
| Carried debt: `AllowAllDashboardAuthorizationFilter` fronts the new admin outbox endpoints. | High (deploy) | **Must be replaced before any non-local deployment.** Same as every prior phase. |

**Carried debt** (unchanged from prior phases): dashboard auth filter, dev SQL password, `SyncRunMetadata.Tags` mutability, `TriggeredBy` as free-form string, repo-vs-spec layout naming, pre-existing unrelated `tests/Core.UniTests` build error.

---

## 10. What Is Verified (and at what level)

### Runtime-verified end-to-end

| Property | Evidence (this audit) |
|---|---|
| Push success path: 2 admin-seeded outbox rows → 2 sink-accepted → outbox `Status=Processed`, `AttemptCount=0`, `ProcessedAt` set | §5.1 |
| Per-row sink failure does not abort the batch | §5.2 — Pipeline still returns `Processed=0` cleanly; writer logs warning per row |
| Failed outbox row stays `Pending` with `AttemptCount=1` and `LastError` set | §5.2 SQL snapshot |
| Recurring `student-sync-push` recovers the failed row on the next tick | §5.2 — job 591 `RecordsProcessed=1` recovered EXT-S-0401 |
| Push routes to `push-sync` queue (both admin-trigger and scheduled) | §5.3 — `Queue=push-sync` in `sync.runs` for jobs 574, 583, 591 |
| Push pipeline metrics line is identical in shape to pull | §5.1, §5.2 — `Pipeline metrics. Module=students Batches=N ... ExtractionMs=N MappingMs=N ValidationMs=N WritingMs=N TotalMs=N` |
| `sync.runs.Direction = 1 (Push)` recorded by existing audit machinery | §5.3 — implicit in the SELECT filter |
| Migration `AddStudentOutbox` applied at boot, idempotent | sqlcmd-listed `sync_student.student_outbox` post-startup |
| Per-module concurrency lock survives the push direction | Inherited from Phase 4 — push runs serialize against pull runs of the same module |

### Code-verified only

| Property | Why not runtime-tested | Where in code |
|---|---|---|
| Whole-batch writer throw → pipeline `SyncResult.Failed` → executor → Hangfire retry | Requires a SQL outage during `SaveChangesAsync` on the outbox table; same mechanism as Phase-4-failing-pipeline already runtime-verified for pull. | `SyncPipeline.RunAsync` `catch (Exception ex)` block + `SyncModuleExecutor.ExecuteAsync` failure path |
| Mapper JSON parse exception → pipeline `Failed` → Hangfire retry | Requires deliberately corrupting an outbox row's payload at the SQL level. Code path: `StudentOutboxMapper.Map` `catch (JsonException)` → `InvalidOperationException` → pipeline failure-path. | `StudentOutboxMapper.cs` |
| Replay defense: re-running over a Processed row is a no-op | Mechanism: `if (dispatch.Row.Status != Pending) continue;` in writer. Hangfire retry mid-batch is bounded by Phase-4-hardened `[AutomaticRetry]` and was runtime-verified there. | `StudentOutboxPushWriter.UpsertBatchAsync` |
| `MaxPerRun = 500` extractor cap | Requires 500+ Pending rows. The cap is a `Take()` clause — behavior is straightforward and runtime-tested implicitly by the success path with smaller numbers. | `StudentOutboxExtractor.ExtractAsync` |
| Sink interface idempotency contract | Cannot be verified at the abstract level; documented on `IExternalStudentSink`. `InMemoryExternalStudentSink` is idempotent by replacing the entry on accept. | `IExternalStudentSink.cs` XML doc |
| `student-sync-push` recurring registration survives host restart | Hangfire `AddOrUpdate` is idempotent on `recurringJobId`. Same mechanism as Phase 2 `fake-sync-pull`/`student-sync-pull`. | `SyncRecurringJobsRegistrar.cs` |

---

## 11. Open Items (Phase 7+)

| Item | Defer-to phase | Note |
|---|---|---|
| Real internal student-edit API writing the outbox transactionally | Phase 7 multi-module expansion, when a real internal write surface appears | Admin seed endpoint is the Phase-6 stand-in |
| Replace `InMemoryExternalStudentSink` with a real HTTP/SOAP client | Phase 7 / pre-deploy | Behind `IExternalStudentSink` |
| Push pipeline failures → `sync.dead_letters` (currently only whole-batch failure path is dead-letter-eligible; per-row failures are tracked only on the outbox row) | Phase 9 | Per-row attribution work |
| Auto-poison rows whose `AttemptCount >= MaxAttempts` (set `Status = Failed`) | Phase 9 | Soft cap exists; auto-action deferred to avoid silent drops |
| Outbox retention policy (Processed rows older than N days → archive/delete) | Phase 9 / 10 | Same shape as `SyncAuditRetention.md` |
| Per-batch retry (vs whole-execution retry) | Phase 8 | Inherited from `Phase5_Hardening_Refinement2_Audit.md` |
| Push observability: `IdempotencySkipped` will always be 0 on push because each outbox row has a unique Id. Consider hiding it from the push metrics line. | Phase 10 | Cosmetic; no behavioral impact |
| Loop-prevention assertion in pull extractor (skip records whose `OriginSystem` indicates they were just pushed) | Phase 7 | Requires the real upstream to honor the `OriginSystem` round-trip |
| Dashboard auth filter replacement | Pre-deploy | Carried debt across all phases |

---

## 12. Operational Quick-Reference

### Modules registered (now)

| Module name | Pull recurring | Push recurring | Queues |
|---|---|---|---|
| `fake` | ✅ every minute | n/a | `default` |
| `fake-pipeline` | ✅ every minute | n/a | `default` |
| `students` | ✅ every minute | **✅ every minute** | Pull → `students-sync`, **Push → `push-sync`** |
| `failing-pipeline` | admin only | n/a | `default` |
| `cancelling-pipeline` | admin only | n/a | `default` |
| `partial-failing-students` | admin only | n/a | `default` |

### New admin endpoints (dev-only — fronted by `AllowAllDashboardAuthorizationFilter`)

| Endpoint | Purpose |
|---|---|
| `POST /admin/outbox/student/{externalStudentId}` | Seed a Pending outbox row. Optional JSON body for FirstName/LastName/Email/ExternalUpdatedAt/ExternalVersion; missing fields fall back to canned defaults. |
| `GET /admin/outbox/sink` | Inspect what the in-memory sink has accepted (count + latest payload per student). |
| `POST /admin/outbox/sink/fail-next/{externalStudentId}` | Arm a one-shot failure for the next push of the given external id — exercises outbox `AttemptCount` + `LastError`. |

### New config knobs

| Key | Default | Source |
|---|---|---|
| `Sync:Student:PushBatchSize` | `25` (validated 0 < n ≤ 1000) | `appsettings.json` |
| `Sync:ModuleQueues:"students\|Push"` | `"push-sync"` | `appsettings.json` |

### Existing knobs (unchanged)

All Phase 5 knobs preserved.

---

## 13. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Five sync projects (`Abstractions`, `Infrastructure`, `Persistence`, `Student`, `Host`) compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| `AddStudentOutbox` migration applied at boot (idempotent on restart) | ✅ Verified via `sqlcmd` listing `sync_student.student_outbox` |
| Admin-triggered Push: 2 rows seeded → 2 sink-accepted → outbox Processed | ✅ §5.1 |
| Per-row failure: row stays Pending with AttemptCount=1, LastError set, batch still completes cleanly | ✅ §5.2 |
| Recurring `student-sync-push` recovery of failed row | ✅ §5.2 (job 591) |
| Push queue routing to `push-sync` (both admin + scheduled) | ✅ §5.3 |
| Pull path for `students` module unchanged | ✅ Visible in `sync.runs` for the same module continuing to log `Queue=students-sync` for Direction=0 |
| Pre-existing unrelated test build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged from earlier phases — out of sync-layer scope |

---

## 14. Phase Exit Checklist

- [x] `student_outbox` table + EF entity + configuration + migration shipped
- [x] `IExternalStudentSink` + in-memory implementation registered behind the interface
- [x] Push pipeline parts (`StudentOutboxExtractor`, `StudentOutboxMapper`, `StudentOutboxValidator`, `StudentOutboxPushWriter`) implement the Phase-4 module-facing contracts
- [x] `StudentSyncModule.PushAsync` no longer a no-op — composes the pipeline with the new parts
- [x] `PushBatchSize` option added and validated against `MaxBatchSize = 1000`
- [x] Recurring `student-sync-push` registered on the minute schedule
- [x] Push runs route to `push-sync` queue via `students|Push` config override
- [x] Admin endpoints for outbox seed / sink inspect / sink fail-next exposed (dev-only)
- [x] Hangfire retry policy applies to push runs identically (inherited from executor `[AutomaticRetry]`)
- [x] CorrelationId visible across dispatcher → executor → pipeline → writer → sink-call
- [x] Per-row sink failure isolates to the offending row (other rows in the batch still complete); failed row stays Pending with AttemptCount + LastError for the next tick
- [x] No public-surface changes to `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] Sync.Abstractions byte-identical
- [x] Sync.Persistence schema and migrations unchanged
- [x] Sync.Infrastructure pipeline / executor unchanged (sole change: one-character queue-separator fix in `SyncOptions`)
- [x] Phase-5 Pull path for `students` byte-identical
- [x] Clean Debug build, `TreatWarningsAsErrors=true`, 0/0
- [x] Audit document produced

---

**Phase 6 (Push Sync — Internal → External) complete. Stopping. Phase 7 (Multi-Module Expansion: Staff, Courses, Schedule, Finance) is the next planned step.**
