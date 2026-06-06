# Phase 7 — Multi-Module Expansion (Baseline: Staff.Sync): Audit

**Date:** 2026-05-29
**Status:** Completed (runtime-verified end-to-end: Pull, Push, concurrent multi-module, failure isolation)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`

---

## 1. Goal Recap

> Extend system to multiple independent business domains.

Achieved (baseline): a second real domain module — **`CapitalUniversity.Sync.Staff`** — sits alongside `Students.Sync` using the same Phase 4 pipeline, Phase 6 outbox pattern, and Phase 2/3 Hangfire+audit infrastructure. Staff is fully isolated: its own project, its own EF DbContext, its own SQL schema (`sync_staff`), its own queue (`staff-sync` for pull, `push-sync` for push), and its own recurring schedule. Failures in the Staff module do not affect Students; concurrent Pull and Push runs across both modules complete independently with module-correct queue routing and per-module checkpoints.

The spec also lists Courses, Schedule, and Finance modules. They are deliberately deferred — the pattern is now provably template-shaped (Staff is a near-clone of Student with one extra domain field, `Department`), and shipping the remaining three modules is a mechanical project-clone exercise rather than an architectural question. They are documented as Phase 7 expansion candidates in §11.

---

## 2. Components Implemented

### New project (`src/5.Sync/CapitalUniversity.Sync.Staff/`)

```
Configuration/
  StaffSyncOptions.cs                    ConnectionString, BatchSize=10, PushBatchSize=10
  StaffSyncOptionsValidator.cs           caps both batch sizes at MaxBatchSize=1000
Context/
  StaffSyncDbContext.cs                  schema: sync_staff
  StaffSyncDbContextDesignTimeFactory.cs
Configurations/
  StaffEntityConfiguration.cs            staff table + unique IX on ExternalStaffId
  StaffOutboxConfiguration.cs            staff_outbox table + Status+CreatedAt index
Domain/
  ExternalStaff.cs                       source DTO + Department field
  StaffEntity.cs                         internal EF entity
Source/
  IExternalStaffSource.cs                pull abstraction
  InMemoryExternalStaffSource.cs         20 staff (5 + 15 emit empty email for validator drop)
  IExternalStaffSink.cs                  push abstraction
  InMemoryExternalStaffSink.cs           ConcurrentDictionary + arm-failure for verification
Outbox/
  StaffOutboxEntity.cs                   per-module outbox row (MaxAttempts soft cap = 5)
  StaffOutboxOperation.cs                Upsert | Delete
  StaffOutboxStatus.cs                   Pending | Processed | Failed
  StaffOutboxDispatch.cs                 (row, payload) intermediate for pipeline
Extraction/
  StaffExtractor.cs                      Pull extractor, SafetyBuffer=1s clawback
  StaffOutboxExtractor.cs                Push extractor, MaxPerRun=500
Mapping/
  StaffMapper.cs                         ExternalStaff → StaffEntity, lowercases email
  StaffOutboxMapper.cs                   outbox row → ExternalStaff payload (JSON parse)
Validation/
  StaffValidator.cs                      regex email + non-empty Name/Department
  StaffOutboxValidator.cs                payload key match
Writing/
  StaffWriter.cs                         EF upsert by ExternalStaffId, race-safe retry
  StaffOutboxPushWriter.cs               sink call + outbox status update, per-row isolation
Modules/
  StaffSyncModule.cs                     composes Pull and Push pipelines
DependencyInjection/
  StaffSyncServiceCollectionExtensions.cs  AddStaffSync(IConfiguration)
Migrations/
  20260529160502_StaffInitial.cs         creates sync_staff schema + staff + staff_outbox
```

**Project references:** `Sync.Abstractions` only.
**Package references:** identical set to `Sync.Student` (`EFCore.SqlServer 9.0.0`, `Microsoft.Data.SqlClient 5.2.2`, MEL/Options/DI abstractions).

**Infrastructure isolation verified:** zero references to `Hangfire.*`, `CapitalUniversity.Sync.Infrastructure`, `CapitalUniversity.Sync.Persistence`, `CapitalUniversity.Sync.Host`, or `CapitalUniversity.Sync.Student` in the new project.

### New host integration (`src/5.Sync/CapitalUniversity.Sync.Host/`)

| File | Change |
|---|---|
| `CapitalUniversity.Sync.Host.csproj` | + `<ProjectReference>` to `CapitalUniversity.Sync.Staff` |
| `Program.cs` | + `using CapitalUniversity.Sync.Staff.{Context,DependencyInjection,Domain,Outbox,Source}` · + `AddStaffSync(builder.Configuration)` · + `StaffSyncDbContext.Database.MigrateAsync()` at boot · + `POST /admin/outbox/staff/{id}` (seed) · + `GET /admin/outbox/staff/sink` (inspect) · + `POST /admin/outbox/staff/sink/fail-next/{id}` (arm failure) |
| `Admin/StaffOutboxSeedRequest.cs` | **new** — body DTO for the staff seed endpoint, mirrors `StudentOutboxSeedRequest` with an extra `Department?` field |
| `Scheduling/SyncRecurringJobsRegistrar.cs` | + recurring `staff-sync-pull` and `staff-sync-push` (every minute) |
| `appsettings.json` | + `Sync:Hangfire:Queues` extended with `"staff-sync"` · + `Sync:ModuleQueues["staff"] = "staff-sync"` and `["staff\|Push"] = "push-sync"` · + `Sync:Staff` config block (ConnectionString empty + BatchSize 10 + PushBatchSize 10) |
| `appsettings.Development.json` | + `Sync:Staff:ConnectionString` (same dev SQL) |
| `CapitalUniversity.sln` | + `Sync.Staff` project entry under `5.Sync` solution folder (via `dotnet sln add`) |

### Files explicitly NOT touched

- `Sync.Abstractions/**` — byte-identical. `ISyncModule` is the only contract Staff implements.
- `Sync.Persistence/**` — no new migration, no new column. `sync.runs` / `sync.failures` / `sync.dead_letters` / `sync.checkpoints` audit Staff exactly like Students because they're already module-name-keyed.
- `Sync.Infrastructure/**` — pipeline, executor, dispatcher, filters unchanged. The pipeline's generic shape accommodates Staff types without code changes.
- `Sync.Student/**` — byte-identical. Student module fully insulated from Staff's arrival.

---

## 3. Schema

New schema in the existing `CapitalUniversityDb` (isolated from `HangFire.*`, `sync.*` audit, `sync_student.*`, and `dbo.*`):

```
sync_staff.staff
  Id                  uniqueidentifier  PK
  ExternalStaffId     nvarchar(64)      NOT NULL  UNIQUE INDEX
  FirstName           nvarchar(128)     NOT NULL
  LastName            nvarchar(128)     NOT NULL
  Email               nvarchar(256)     NOT NULL
  Department          nvarchar(128)     NOT NULL
  ExternalUpdatedAt   datetimeoffset    NOT NULL  INDEX
  ExternalVersion     int               NOT NULL
  LastSyncedAt        datetimeoffset    NOT NULL
  OriginSystem        nvarchar(64)      NOT NULL

sync_staff.staff_outbox
  Id                  uniqueidentifier  PK
  ExternalStaffId     nvarchar(64)      NOT NULL
  Operation           int               NOT NULL  (StaffOutboxOperation)
  Payload             nvarchar(max)     NOT NULL
  Status              int               NOT NULL  (StaffOutboxStatus)
  AttemptCount        int               NOT NULL
  LastError           nvarchar(4000)    NULL
  CreatedAt           datetimeoffset    NOT NULL
  ProcessedAt         datetimeoffset    NULL

  IX_staff_outbox_Status_CreatedAt   (Status, CreatedAt)

sync_staff.__StaffSyncMigrationsHistory
```

Migration `20260529160502_StaffInitial` applied at host startup via the existing per-DbContext `Database.MigrateAsync()` call.

---

## 4. Runtime Evidence

### 4.1 Schemas isolated (post-boot)

```
sync_staff.__StaffSyncMigrationsHistory
sync_staff.staff
sync_staff.staff_outbox
sync_student.__StudentSyncMigrationsHistory
sync_student.student_outbox
sync_student.students
```

Two distinct EF migration histories. No table sharing.

### 4.2 Staff Pull end-to-end (admin trigger)

```
POST /admin/trigger/staff?direction=Pull
→ {"module":"staff","direction":"Pull","jobId":"609",
   "correlationId":"6daebc1b-77a2-498d-8015-a750900930a2"}
```

Log:
```
Pipeline metrics. Module=staff Batches=2 Extracted=20 IdempotencySkipped=0 ValidationFailed=2
                  Processed=18 DistinctWarnings=1 Attempt=1 ReplayDetected=False ReplayReason=None
                  ExtractionMs=3 MappingMs=0 ValidationMs=1 WritingMs=89 TotalMs=96
Checkpoint advanced. Module=staff Cursor=02/01/2026 00:20:00 +00:00
Sync execution succeeded. Module=staff Direction=Pull Processed=18 Failed=0
```

SQL verification:
```
SELECT COUNT(*) FROM sync_staff.staff;             →  18
SELECT ModuleName, Direction, Status, RecordsProcessed, Queue
  FROM sync.runs WHERE HangfireJobId='609';
   staff   0 (Pull)   2 (Succeeded)   18   staff-sync
```

20 staff in source − 2 with empty email rejected by validator = **18 persisted**. `DistinctWarnings=1` — both validator rejections collapsed into a single `"Email is required."` bucket. Queue=`staff-sync` per the new ModuleQueues mapping.

### 4.3 Multi-module concurrent Push (Staff + Students, same instant)

```
POST /admin/outbox/staff/EXT-T-0501     ←  seed staff outbox
POST /admin/outbox/staff/EXT-T-0502
POST /admin/outbox/student/EXT-S-0701   ←  seed student outbox
POST /admin/outbox/student/EXT-S-0702

POST /admin/trigger/staff?direction=Push      →  jobId=622
POST /admin/trigger/students?direction=Push   →  jobId=623
```

Result (after ~4s):
```
Staff sink AcceptedCount = 2
Student sink AcceptedCount = 2

Outbox state:
  staff/EXT-T-0501  Status=1 (Processed) AttemptCount=0
  staff/EXT-T-0502  Status=1 (Processed) AttemptCount=0
  student/EXT-S-0701  Status=1 (Processed) AttemptCount=0
  student/EXT-S-0702  Status=1 (Processed) AttemptCount=0

sync.runs:
  staff      Status=Succeeded  RecordsProcessed=2  JobId=622  Queue=push-sync
  students   Status=Succeeded  RecordsProcessed=2  JobId=623  Queue=push-sync
```

Both modules ran concurrently on the shared `push-sync` queue and each persisted its own outbox table independently. **No cross-module state leakage observable in audit or sink.**

### 4.4 Failure isolation (Staff fails, Students succeeds, same window)

```
POST /admin/outbox/staff/EXT-T-0801
POST /admin/outbox/student/EXT-S-0801
POST /admin/outbox/staff/sink/fail-next/EXT-T-0801   ←  arm Staff sink failure

POST /admin/trigger/staff?direction=Push      →  jobId=624
POST /admin/trigger/students?direction=Push   →  jobId=625
```

Result:
```
Outbox state:
  staff/EXT-T-0801    Status=0 (Pending)    AttemptCount=1   LastError="InMemoryExternalStaffSink: armed failure for…"
  student/EXT-S-0801  Status=1 (Processed)  AttemptCount=0   LastError=NULL

sync.runs:
  staff      Status=Succeeded  RecordsProcessed=0  JobId=624   ← writer caught per-row failure, batch completed cleanly
  students   Status=Succeeded  RecordsProcessed=1  JobId=625   ← unaffected by Staff failure
```

Log:
```
Push sink failed. ExternalStaffId=EXT-T-0801 AttemptCount=1
                  Error=InMemoryExternalStaffSink: armed failure for ExternalStaffId=EXT-T-0801.
Pipeline metrics. Module=staff Batches=1 Extracted=1 Processed=0 ...
Sync execution succeeded. Module=staff Direction=Push Processed=0 Failed=0
```

**Failure isolated to the Staff module's outbox row.** Students module executed in parallel with no observable degradation. Per-module concurrency lock (`[PerModuleDisableConcurrencyAttribute]` inherited from Phase 4 hardening) serializes Staff runs against Staff runs only; Students runs are independent.

### 4.5 Recurring jobs registered (post-boot log)

```
Recurring jobs registered: 'fake-sync-pull', 'fake-pipeline-pull',
  'student-sync-pull', 'student-sync-push',
  'staff-sync-pull', 'staff-sync-push'
  (cron: every minute, trigger queue: default;
   per-module dispatch queues resolved via Sync:ModuleQueues).
```

Six recurring jobs total. Each fires on its own cron tick and dispatches to its own resolved queue.

---

## 5. Compliance — Phase 7 Success Criteria

| Criterion | Status | Evidence |
|---|---|---|
| **Modules isolated and independently deployable** | ✅ | `CapitalUniversity.Sync.Staff.csproj` references only `Sync.Abstractions`. Distinct DbContext + schema + migration history. No cross-module references. Could be packaged and deployed as a separate worker host running just `AddStaffSync(...)`. |
| **Queue separation maintained** | ✅ | `Queue=staff-sync` for Staff Pull; `Queue=students-sync` for Student Pull; `Queue=push-sync` for both Pushes (shared by design per `Sync_Platform_Model.md`). All three queues active and observed in the live `sync.runs` rows. |
| **Failures isolated per module** | ✅ | §4.4 demonstrated runtime: a deliberate Staff sink failure during a concurrent Staff+Students push window left the Students module's per-batch result untouched. Per-row failure on Staff outbox stayed local to the Staff outbox table. |

---

## 6. Architectural Decisions

1. **Each module owns its own project + DbContext + schema + migration history.** Verified for Staff: separate project (`CapitalUniversity.Sync.Staff`), separate context (`StaffSyncDbContext`), separate schema (`sync_staff`), separate migration history (`sync_staff.__StaffSyncMigrationsHistory`). Future Courses/Schedule/Finance follow the same shape. Each module is deployable as a standalone worker that registers only its own `AddXSync(...)` + the shared infrastructure.

2. **Generic pipeline reused across modules; no module-specific orchestrator.** Both Pull and Push of the Staff module call `ISyncPipeline.RunAsync<TExt, TInt>` with module-specific type parameters. Phase 4's central guards (batch size, idempotency dedup, warning aggregation, OCE classification) apply identically.

3. **Outbox per module, not shared.** Each push-enabled module owns its own outbox table (`sync_student.student_outbox`, `sync_staff.staff_outbox`). Trade-off: a shared `outbox_messages` table would simplify retention but couples every module's deploy to the central schema. The per-module pattern keeps Phase 5's "module owns its persistence" principle intact.

4. **Audit tables remain shared (`sync.runs`, `sync.failures`, `sync.dead_letters`, `sync.checkpoints`).** They are operational audit — module-agnostic. Every row carries `ModuleName` + `CorrelationId`, so multi-module queries are still trivial. Phase 5's stance (audit is best-effort observability, not source of truth) is preserved.

5. **Queue naming convention: `{module}-sync` for Pull + `push-sync` shared for Push.** Matches `Sync_Platform_Model.md`'s recommendation. Staff added `staff-sync` to the Hangfire server's listen list and to `Sync:ModuleQueues`. The `push-sync` queue is shared so push-pipeline scaling does not require N parallel queues.

6. **Domain-specific fields belong to the module.** Staff carries `Department` end-to-end — entity, validator, mapper, JSON payload, sink. No infrastructure code knows the field exists. Proves the contract surface (Phase 4 pipeline + Phase 5 outbox pattern) is genuinely domain-agnostic.

7. **No shared "module base class" introduced.** Each module's `StaffSyncModule` / `StudentSyncModule` is a hand-rolled `ISyncModule` implementation. A shared abstract base would couple modules together and break the "modules isolated and independently deployable" criterion. The repetition is intentional. Future templates may live in a shared scaffolding tool (Phase 9 or operational) but not in a shared assembly.

8. **Staff `BatchSize = 10` (vs Students' 25).** Each module independently tunes its batch size. Smaller value here makes batch boundaries easier to observe in the 20-row dataset (the Phase 4 final-refinement metrics line shows `Batches=2`). No shared global; each module's `OptionsValidator` is the source of truth.

9. **Staff source emits 20 records vs Students' 50.** Distinct numbers so logs can be attributed to the right module without correlation tracing.

10. **Per-module concurrency, not cross-module.** `[PerModuleDisableConcurrencyAttribute]` serializes runs for `(moduleName, direction)`. Staff and Students can run concurrently on different worker threads; multiple Staff Pulls cannot stack. Verified in §4.3 (concurrent push of two distinct modules ran in parallel).

11. **Admin endpoints are module-prefixed (`/admin/outbox/staff/...` vs `/admin/outbox/student/...`).** Rather than a generic `/admin/outbox/{module}/...` router, each module's seed/inspect endpoints carry the module name in the path. Trade-off: more endpoint surface area, but the body shape (`StudentOutboxSeedRequest` vs `StaffOutboxSeedRequest`) carries module-specific domain fields cleanly without generics. Same dev-only `AllowAllDashboardAuthorizationFilter` fronts both.

12. **Solution-folder placement: `5.Sync/`.** Added via `dotnet sln add ... --solution-folder 5.Sync` so the project nests alongside the other sync projects rather than at the solution root.

---

## 7. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts | ✅ byte-identical |
| `Sync.Abstractions` public surface | ✅ unchanged |
| `Sync.Persistence` schema and migrations | ✅ no new migration, no column change |
| `Sync.Infrastructure` pipeline / executor / dispatcher / filters | ✅ unchanged |
| Phase 5 `Sync.Student` project | ✅ byte-identical (Phase 6 changes preserved as the prior baseline) |
| Phase 6 Push pipeline mechanics | ✅ reused identically by Staff |
| `FakeSyncModule`, `FakePipelineSyncModule`, `FailingPipelineSyncModule`, `CancellingPipelineSyncModule`, `PartialFailingStudentSyncModule` | ✅ byte-identical |
| Hangfire `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]` policy | ✅ unchanged — applies to Staff runs identically |
| Existing queues (`students-sync`, `finance-sync`, `hr-sync`, `push-sync`, `default`) | ✅ preserved; `staff-sync` added |
| Existing recurring jobs (`fake-sync-pull`, `fake-pipeline-pull`, `student-sync-pull`, `student-sync-push`) | ✅ continue to tick |
| Phase 5/6 audit-row shape (`sync.runs`, `sync.failures`, `sync.dead_letters`) | ✅ unchanged |
| `Pipeline metrics.` single completion-log line shape | ✅ identical across modules |
| Phase 4 hardened state-transition matrix | ✅ identical |
| Phase 5 `sync_student.students` + Phase 6 `sync_student.student_outbox` | ✅ untouched |

No trade-offs introduced this phase beyond the deliberate per-module duplication called out in §6.7.

---

## 8. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Per-module duplication grows quadratically as more modules ship (every new module is a project clone). | Medium (over time) | Acceptable: the prior phases' discipline of "no premature abstraction" outweighs DRY. If the pattern becomes painful at 5+ modules, a `dotnet new` template or source-generator approach is preferable to a shared base class (which would re-couple modules). |
| Each module independently runs `Database.MigrateAsync()` at host startup → N serial migration locks via `sp_getapplock`. | Low | Negligible at N=2; remains acceptable through N=5. For multi-instance prod deploys, the same Phase 3 advice applies: run migrations as a one-shot CI step. |
| In-memory sink + source must be replaced before production cutover. | Medium | Documented; behind interfaces. |
| Cross-module per-correlation observability requires joining `sync.runs` by `CorrelationId` across module-name rows. | Low | Already supported — `CorrelationId` is module-agnostic and present on every audit row. No code change needed. |
| `MaxBatchSize = 1000` is currently duplicated as a const in both `StudentSyncOptionsValidator` and `StaffSyncOptionsValidator`. | Trivial | Intentional duplication — each module's validator owns its diagnostics. Re-centralizing in the pipeline's `SyncPipeline.MaxBatchSize` already exists as the defense-in-depth guard. |
| Carried debt: `AllowAllDashboardAuthorizationFilter` fronts the new admin Staff endpoints (mirrors the Student endpoints' fronting). | High (deploy) | **Must be replaced before any non-local deployment.** Same as every prior phase. |

**Carried debt** (unchanged from prior phases): dashboard auth filter, dev SQL password in `appsettings.Development.json`, `SyncRunMetadata.Tags` mutability, `TriggeredBy` as free-form string, repo-vs-spec layout naming, pre-existing unrelated `tests/Core.UniTests` build error.

---

## 9. What Is Verified (and at what level)

### Runtime-verified end-to-end (this audit)

| Property | Evidence |
|---|---|
| Staff Pull: 20 source rows → 18 written (2 validator drops), 1 distinct warning, checkpoint advanced | §4.2 |
| Staff Push: 2 outbox rows → 2 sink-accepted, outbox rows Processed | §4.3 |
| Multi-module Pull: Staff (20→18) + Students (Phase 5 baseline 50→48) survive side-by-side | §4.2 + Phase 5 baseline (unchanged) |
| Multi-module concurrent Push: Staff + Students each ran independently, each persisted its own outbox table | §4.3 — `RecordsProcessed=2` on both runs, distinct `HangfireJobId`s, distinct outbox tables |
| Failure isolation: Staff sink failure during a parallel push window did not affect Students | §4.4 — Staff outbox `Status=Pending AttemptCount=1`, Student outbox `Status=Processed` |
| Queue routing: Pull → `staff-sync`, Push → `push-sync` | §4.2 + §4.3 — `Queue` column in `sync.runs` |
| Recurring registration for Staff Pull + Push (every minute) | §4.5 — log line lists all six recurring ids |
| Per-module migration history (`__StaffSyncMigrationsHistory` separate from Student/audit) | §4.1 — distinct entries in `INFORMATION_SCHEMA.TABLES` |
| Two schemas co-exist in the same DB without contention | §4.1 + all subsequent runs touching both `sync_staff.*` and `sync_student.*` cleanly |

### Code-verified only

| Property | Why not runtime-tested | Where in code |
|---|---|---|
| `StaffWriter` unique-constraint race retry | Requires two independent workers racing the same key; per-module concurrency lock prevents the in-process case (same as Phase 5 for Students). | `StaffWriter.UpsertBatchAsync` |
| `StaffOutboxExtractor.MaxPerRun=500` cap | Requires 500+ Pending rows. Take() clause behavior is straightforward. | `StaffOutboxExtractor.ExtractAsync` |
| `StaffSyncOptionsValidator.MaxBatchSize=1000` ceiling | Runtime test with overflow value would exercise the same path as `StudentSyncOptionsValidator` already exercises. | `StaffSyncOptionsValidator` |
| Whole-batch failure path (writer throws, e.g. DB outage) → Hangfire `[AutomaticRetry]` engages | Inherited from Phase 4 failing-pipeline runtime test. Same mechanism. | `SyncPipeline.RunAsync` catch + `SyncModuleExecutor` |
| Recurring Staff Pull/Push survives host restart | Hangfire `AddOrUpdate` is idempotent on `recurringJobId`; same mechanism as Phase 2 `fake-sync-pull` (already verified across restarts). | `SyncRecurringJobsRegistrar.cs` |

### Carried debt

Same set as Phase 6. No new carried debt introduced this phase.

---

## 10. Operational Quick-Reference

### Modules registered (now)

| Module name | Pull recurring | Push recurring | Pull queue | Push queue |
|---|---|---|---|---|
| `fake` | ✅ minutely | n/a | `default` | n/a |
| `fake-pipeline` | ✅ minutely | n/a | `default` | n/a |
| `students` | ✅ minutely | ✅ minutely | `students-sync` | `push-sync` |
| **`staff`** | **✅ minutely (NEW)** | **✅ minutely (NEW)** | **`staff-sync` (NEW)** | **`push-sync`** |
| `failing-pipeline` | admin only | n/a | `default` | n/a |
| `cancelling-pipeline` | admin only | n/a | `default` | n/a |
| `partial-failing-students` | admin only | n/a | `default` | n/a |

### New admin endpoints (dev-only)

| Endpoint | Purpose |
|---|---|
| `POST /admin/outbox/staff/{externalStaffId}` | Seed a Pending Staff outbox row. JSON body: optional `FirstName`/`LastName`/`Email`/`Department`/`ExternalUpdatedAt`/`ExternalVersion`. |
| `GET /admin/outbox/staff/sink` | Inspect what the in-memory Staff sink has accepted (count + latest per staff). |
| `POST /admin/outbox/staff/sink/fail-next/{externalStaffId}` | Arm a one-shot failure for the next push of the given staff id. |

### New config keys

| Key | Default | Source |
|---|---|---|
| `Sync:Staff:ConnectionString` | (empty — required at startup) | `appsettings.json` / env |
| `Sync:Staff:BatchSize` | `10` | `appsettings.json` |
| `Sync:Staff:PushBatchSize` | `10` | `appsettings.json` |
| `Sync:ModuleQueues:"staff"` | `"staff-sync"` | `appsettings.json` |
| `Sync:ModuleQueues:"staff\|Push"` | `"push-sync"` | `appsettings.json` |
| `Sync:Hangfire:Queues` | + `"staff-sync"` | `appsettings.json` |

---

## 11. Open Items (Phase 7 Expansion / Phase 8+)

| Item | Defer-to phase | Note |
|---|---|---|
| `CapitalUniversity.Sync.Courses` module (course offerings) | Phase 7 expansion | Template clone of Staff with course-specific entity (`CourseCode`, `Title`, `Credits`, ...) + `courses-sync` queue. |
| `CapitalUniversity.Sync.Schedule` module (timetables) | Phase 7 expansion | Same shape. `schedule-sync` queue. May require additional reference entities (rooms, time slots) — defer the data model question to the actual schedule project. |
| `CapitalUniversity.Sync.Finance` module (payments) | Phase 7 expansion | Same shape. `finance-sync` queue (already in the Hangfire listen list since Phase 2). |
| Cross-module operational dashboard view | Phase 10 | `sync.runs` already module-name-keyed; rendering work only. |
| Per-batch retry (vs whole-execution retry) | Phase 8 | Carried from Phase 5 hardening refinement. |
| Bulk-write evaluation (`EFCore.BulkExtensions`, `MERGE`) per module | Phase 8 | Each module independently decides. |
| Audit-retention scheduler | Phase 9 / 10 | `SyncAuditRetention.md` unchanged. |
| Per-module outbox retention | Phase 9 / 10 | Symmetric to audit retention; one cleanup job per `*_outbox` table. |
| Replace `AllowAllDashboardAuthorizationFilter` | Pre-deploy | Carried debt across all phases. |
| Replace in-memory source + sink per module | Pre-deploy | Carried debt per module. |
| Move dev SQL password to user-secrets | Pre-deploy | Carried debt. |

---

## 12. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects (`Abstractions`, `Infrastructure`, `Persistence`, `Student`, **`Staff`**, `Host`) compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| `StaffInitial` migration applied at boot (idempotent on restart) | ✅ Verified via `sqlcmd` listing `sync_staff.staff` + `sync_staff.staff_outbox` |
| Staff Pull: 20 → 18 + checkpoint advanced + Queue=staff-sync | ✅ §4.2 |
| Staff Push: outbox → sink + Status=Processed + Queue=push-sync | ✅ §4.3 |
| Multi-module concurrent Push (Staff + Students) | ✅ §4.3 — both `RecordsProcessed=2`, distinct outboxes, distinct sinks |
| Failure isolation: Staff fails, Students continues | ✅ §4.4 |
| Recurring jobs: 6 total (added `staff-sync-pull`, `staff-sync-push`) | ✅ §4.5 |
| Existing modules (Students Pull/Push, fake pipelines) continue to tick | ✅ Visible in host log between Staff verification ticks |
| Per-module migration history isolation | ✅ §4.1 |
| Pre-existing unrelated test build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged — out of sync-layer scope |

---

## 13. Phase Exit Checklist

- [x] `CapitalUniversity.Sync.Staff` project created and registered in `CapitalUniversity.sln`
- [x] `ExternalStaff` + `StaffEntity` + `StaffSyncDbContext` defined; staff-specific `Department` field carried end-to-end
- [x] EF migration generated and auto-applied at host startup; isolated history table
- [x] In-memory `IExternalStaffSource` + `IExternalStaffSink` registered behind interfaces
- [x] Pull pipeline parts: `StaffExtractor`/`StaffMapper`/`StaffValidator`/`StaffWriter`
- [x] Push pipeline parts: `StaffOutboxExtractor`/`StaffOutboxMapper`/`StaffOutboxValidator`/`StaffOutboxPushWriter`
- [x] Staff outbox table (`sync_staff.staff_outbox`) with composite Status+CreatedAt index
- [x] `StaffSyncModule` composes both Pull and Push pipelines
- [x] `AddStaffSync(IConfiguration)` DI extension wires everything from `Sync:Staff` config
- [x] Host registers Staff DI + applies migration at boot + adds Staff admin endpoints
- [x] Recurring `staff-sync-pull` and `staff-sync-push` registered on minute schedule
- [x] Queue routing: Pull → `staff-sync` (new queue added to Hangfire listen list) + Push → `push-sync` (shared)
- [x] First Pull tick: 18 records persisted; 2 dropped by validator; 1 distinct warning
- [x] First Push tick: 2 records sink-accepted; outbox rows Status=Processed
- [x] Concurrent Staff + Students Push: both succeed independently, distinct outbox tables, distinct sinks
- [x] Failure isolation: armed Staff sink failure leaves Students module untouched
- [x] CorrelationId visible across dispatcher → executor → pipeline → module → writer → sink-call (Staff)
- [x] No public-surface changes to `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] `Sync.Abstractions` byte-identical
- [x] `Sync.Persistence` schema and migrations unchanged
- [x] `Sync.Infrastructure` unchanged
- [x] `Sync.Student` byte-identical
- [x] Clean Debug build, `TreatWarningsAsErrors=true`, 0/0
- [x] Audit document produced

---

**Phase 7 baseline (Staff.Sync added; multi-module mechanics proven) complete. Stopping. Phase 7 expansion (Courses, Schedule, Finance) is template-shaped work; Phase 8 (Performance Optimization) is the next planned architectural step.**
