# Phase 5 — First Real Module (Students.Sync): Audit

**Date:** 2026-05-29
**Status:** Completed (runtime-verified end-to-end on real SQL Server)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`

---

## 1. Goal Recap

> Introduce the first real domain module using the full architecture stack.

Achieved: a dedicated `CapitalUniversity.Sync.Student` project consumes the Phase 4 pipeline with a real domain (`ExternalStudent` → `StudentEntity`), owns its own EF context + schema + migration, advances per-module checkpoints after success, and runs both manually (admin endpoint) and on the `student-sync-pull` recurring schedule against the `students-sync` queue.

---

## 2. Project Layout

```
src/5.Sync/CapitalUniversity.Sync.Student/
├── Configuration/StudentSyncOptions.cs       # ConnectionString, BatchSize
├── Context/StudentSyncDbContext.cs           # schema: sync_student
├── Context/StudentSyncDbContextDesignTimeFactory.cs
├── Configurations/StudentEntityConfiguration.cs
├── Domain/ExternalStudent.cs                 # source DTO
├── Domain/StudentEntity.cs                   # internal EF entity
├── Source/IExternalStudentSource.cs          # external-system abstraction
├── Source/InMemoryExternalStudentSource.cs   # 50-student in-memory simulator
├── Extraction/StudentExtractor.cs            # IDataExtractor<ExternalStudent>
├── Mapping/StudentMapper.cs                  # IRecordMapper<ExternalStudent, StudentEntity>
├── Validation/StudentValidator.cs            # IRecordValidator<StudentEntity>
├── Writing/StudentWriter.cs                  # IRecordWriter<StudentEntity> — EF upsert
├── Modules/StudentSyncModule.cs              # ISyncModule
├── DependencyInjection/StudentSyncServiceCollectionExtensions.cs
└── Migrations/20260529111407_StudentInitial.{cs,Designer.cs}
```

**Project references:** `Sync.Abstractions` only.
**Package references:** `Microsoft.EntityFrameworkCore.SqlServer 9.0.0` + `.Design` (private) + `Microsoft.Extensions.{Options,Logging,DependencyInjection}.Abstractions`.

**Infrastructure isolation verified:** zero references to `Hangfire.*`, `CapitalUniversity.Sync.Infrastructure`, `CapitalUniversity.Sync.Persistence`, or `CapitalUniversity.Sync.Host` in the module project.

---

## 3. Schema

New `sync_student` schema in the existing `CapitalUniversityDb` database, isolated from `HangFire.*`, `sync.*` (Phase 3 audit tables), and `dbo.*` (main app).

```
sync_student.students
  Id                  uniqueidentifier  PK
  ExternalStudentId   nvarchar(64)      NOT NULL  UNIQUE INDEX
  FirstName           nvarchar(128)     NOT NULL
  LastName            nvarchar(128)     NOT NULL
  Email               nvarchar(256)     NOT NULL
  ExternalUpdatedAt   datetimeoffset    NOT NULL  INDEX
  ExternalVersion     int               NOT NULL
  LastSyncedAt        datetimeoffset    NOT NULL
  OriginSystem        nvarchar(64)      NOT NULL

sync_student.__StudentSyncMigrationsHistory
```

`ExternalStudentId` is the **stable merge key** per `Sync_Platform_Model.md`. The unique index makes upserts safe under concurrent writers.

---

## 4. Pipeline Wiring

```
StudentSyncModule.PullAsync(SyncContext)
  ├─ checkpointStore.GetAsync("students")
  └─ ISyncPipeline.RunAsync<ExternalStudent, StudentEntity>(SyncPipelineRequest {
         Extractor          = StudentExtractor      ─→ IExternalStudentSource (in-memory)
         Mapper             = StudentMapper
         Validator          = StudentValidator       (email/name/id checks)
         Writer             = StudentWriter          (EF upsert by ExternalStudentId)
         ExternalKeySelector = s => s.ExternalStudentId
         BatchSize          = 25
         CurrentCheckpoint  = ←checkpoint cursor (ExternalUpdatedAt ISO-O)
     })
  └─ on success + maxObserved present:
       checkpointStore.SaveAsync("students", new SyncCheckpoint {
         Cursor = MaxExternalUpdatedAt.ToString("O")
       })
       (wrapped in try/catch — checkpoint-save failure → warning + replay notice)
```

The `StudentExtractor` tracks `MaxExternalUpdatedAt` during enumeration so the module can advance the checkpoint deterministically after the pipeline returns success.

---

## 5. Runtime Evidence — First Tick

### Trigger

```
POST /admin/trigger/students?direction=Pull
→ {"module":"students","direction":"Pull","jobId":"336","correlationId":"17888b3a-ba5f-4afc-9885-ad73c2879d4f"}
```

### Log trace (selected lines)

```
Sync job enqueued.   Module=students Direction=Pull TriggeredBy=admin Queue=students-sync JobId=336
Sync execution started. Module=students Direction=Pull TriggeredBy=admin Attempt=1
Pipeline started.    Module=students BatchSize=25 HasCheckpoint=False
Pipeline stage completed. Module=students Stage=Extraction DurationMs=2 Batch=1   (Debug)
Pipeline stage completed. Module=students Stage=Mapping    DurationMs=0 Batch=1   (Debug)
Pipeline stage completed. Module=students Stage=Validation DurationMs=1 Batch=1   (Debug)
… (batch 2) …
Pipeline metrics.    Module=students Batches=2 Processed=48 Skipped=0 ValidationFailed=2 DistinctWarnings=1 ExtractionMs=2 MappingMs=0 ValidationMs=1 WritingMs=148 TotalMs=155
Checkpoint advanced. Module=students Cursor=01/01/2026 00:50:00 +00:00
Sync execution succeeded. Module=students Direction=Pull Processed=48 Failed=0 Duration=00:00:00.1552343
```

**Routing:** `Queue=students-sync` (configured via `Sync:ModuleQueues:students = "students-sync"`).

### SQL verification

```
SELECT COUNT(*) FROM sync_student.students;          →  48

SELECT TOP 3 ExternalStudentId, FirstName, Email FROM sync_student.students ORDER BY ExternalStudentId;
EXT-S-0001  First1  student0001@university.test
EXT-S-0002  First2  student0002@university.test
EXT-S-0003  First3  student0003@university.test

SELECT Status, RecordsProcessed, HangfireJobId FROM sync.runs WHERE ModuleName='students' AND HangfireJobId='336';
2 (Succeeded)   48   336

SELECT ModuleName, LastSyncedAt, Cursor FROM sync.checkpoints WHERE ModuleName='students';
students   2026-05-29 11:16:37.230 +00:00   2026-01-01T00:50:00.0000000+00:00
```

50 students in source − 2 with empty email rejected by validator = **48 persisted**.
**`DistinctWarnings=1`** — both validation rejections collapsed to a single warning (`"Email is required."`) thanks to the Phase-4-final-refinement aggregation.

---

## 6. Runtime Evidence — Second Tick (Checkpoint Recovery)

### Trigger

```
POST /admin/trigger/students?direction=Pull
→ {"module":"students","direction":"Pull","jobId":"343",...}
```

### Metrics

```
Pipeline metrics. Module=students Batches=0 Processed=0 Skipped=0 ValidationFailed=0 DistinctWarnings=0 ExtractionMs=0 MappingMs=0 ValidationMs=0 WritingMs=0 TotalMs=6
```

`Batches=0 Processed=0` — the extractor's cursor (`2026-01-01T00:50:00+00:00`) is at or above every student's `ExternalUpdatedAt`, so the in-memory source streams zero records. No upsert. **`sync_student.students` row count remains 48** (verified directly).

This proves:
- The checkpoint persists across runs (Phase 3 store).
- The extractor honors the cursor (Phase 4 pipeline filter).
- Pipeline behavior is idempotent when there's nothing new.

---

## 7. Compliance — Phase 5 Success Criteria

| Criterion | Status | Evidence |
|---|---|---|
| End-to-end student sync works | ✅ | First tick: 48 rows persisted; CorrelationId, JobId, queue routing all visible. |
| Full checkpoint recovery works | ✅ | Second tick: `Batches=0 Processed=0`; row count unchanged. |
| Logs provide full observability | ✅ | Pipeline-start, per-batch stage (Debug), single completion-metrics line, checkpoint-advanced — all carry `CorrelationId` via MEL scope. |
| Module remains infrastructure-isolated | ✅ | `CapitalUniversity.Sync.Student.csproj` references only `Sync.Abstractions` + EF Core packages. No Hangfire/Persistence/Host references. |

---

## 8. Cross-Cutting Verification

| Concern | Status | Notes |
|---|---|---|
| `ISyncLogger` used for structured logs | ✅ | Module logs checkpoint advance + save failure via `ISyncLogger`. Pipeline logs use the same. |
| Correlation tracking preserved | ✅ | `context.CorrelationId` flows from dispatcher → executor → pipeline → module → log scope. Verified by CorrelationId appearing in every Hangfire-dispatched log line. |
| No infrastructure leakage outside boundaries | ✅ | Module project has no Hangfire ref; writer uses its own `StudentSyncDbContext`, not `SyncDbContext` (Phase 3 audit context). |
| Per-module queue isolation | ✅ | Routed to `students-sync` queue per `Sync:ModuleQueues:students`. |
| Multi-DbContext startup migration | ✅ | Both `SyncDbContext` and `StudentSyncDbContext` apply at boot. Each owns its own `MigrationsHistory` table in its own schema. |
| Pipeline contract honored | ✅ | `IDataExtractor`, `IRecordMapper`, `IRecordValidator`, `IRecordWriter` all implemented per Phase 4 contracts. |
| Warning aggregation | ✅ | 2 validation rejections → 1 distinct warning ("Email is required."). Verified in metrics line. |
| Batched writes (no `SaveChanges` per record) | ✅ | `StudentWriter.UpsertBatchAsync` calls `SaveChangesAsync` once per batch. |
| `AsNoTracking` reads where appropriate | n/a | Writer reads existing rows for the upsert decision; entities must be tracked to update. No tracked reads outside the write path. |
| Idempotent writer | ✅ | Upsert by `ExternalStudentId`. Second tick after a cursor reset would update existing rows (external-wins) without duplicates due to the unique index. |

---

## 9. Architectural Decisions

1. **Module owns its own DbContext + migration.** `StudentSyncDbContext` is separate from `SyncDbContext` (Phase 3 audit) and from any Core app context. This matches the modular-ownership principle in `Sync_Platform_Model.md` — each business module owns its persistence. Future modules (Payments, Schedule, etc.) will follow the same pattern.

2. **`sync_student` schema in the existing database.** Avoids the operational overhead of a second physical database while keeping table isolation crisp. Splitting to a dedicated database is a connection-string change.

3. **In-memory `IExternalStudentSource` is registered behind an interface.** Production replaces `InMemoryExternalStudentSource` with an HTTP/SQL adapter (or a Phase 7 "External University System" client) by changing one DI line. Module code is source-agnostic.

4. **Checkpoint cursor format = ISO-8601 `"O"` of `ExternalUpdatedAt`.** Module-specific format. Phase 4's `SyncCheckpoint` model accepts arbitrary strings via the `Cursor` field, so this is contained inside the module.

5. **Writer reads-then-writes within the batch.** A more performant option is `MERGE` SQL or `EFCore.BulkExtensions`. Deliberately deferred to Phase 8 (Performance) per the implementation plan; Phase 5 uses plain EF to keep the first real module simple and reviewable.

6. **Validator collapses to a single regex + null checks.** Standard `IRecordValidator<T>` semantics. Errors are aggregated by the pipeline; the validator itself is stateless and trivially unit-testable.

7. **Push path returns `Ok(0, Zero)` — Phase 6 placeholder.** No-op for now; the Phase 5 spec is pull-only.

---

## 10. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Writer's read-then-write is N+1-ish for large batches. | Low (Phase 5 batch size = 25) | Will be replaced with batched MERGE / `EFCore.BulkExtensions` in Phase 8. |
| Writer wraps two SQL operations (read + write) without a transaction. A race could double-insert if two workers process overlapping batches. | Low | The unique index on `ExternalStudentId` would surface this as `DbUpdateException`; current writer would surface the failure to the pipeline → executor → retry. Phase 9 hardens with explicit transaction. |
| `InMemoryExternalStudentSource` is for verification only — must be swapped before production cutover. | Medium | Documented; behind `IExternalStudentSource`. |
| `appsettings.Development.json` carries DB password in plaintext (carried from earlier phases). | Medium | Move to user-secrets before broader dev use. |
| `AllowAllDashboardAuthorizationFilter` fronts the admin trigger endpoint (carried). | High (deploy) | Must be replaced before non-local deployment. |
| Multi-DbContext startup migration locks (`sp_getapplock`) serially. | Low | Each context has its own MigrationsHistory; lock contention only matters during initial deploy. |
| Two modules write to the same physical DB via separate connections. | Low | Schema isolation. No table sharing. |

**Carried debt** (unchanged from prior phases): dashboard auth filter, dev SQL password, `SyncRunMetadata.Tags` mutability, `TriggeredBy` as free-form string, repo-vs-spec layout naming, pre-existing unrelated `tests/Core.UniTests` build error.

---

## 11. Testability Status

- All pipeline parts (`StudentExtractor`, `StudentMapper`, `StudentValidator`, `StudentWriter`) are unit-testable with minimal setup. Writer needs an in-memory `StudentSyncDbContext` (EF Core in-memory provider) or a transient SQL container.
- `IExternalStudentSource` mock yields any test fixture. `InMemoryExternalStudentSource` itself serves as a default test double for higher-level tests.
- `StudentSyncModule.PullAsync` is testable end-to-end using a fake `ISyncPipeline` plus a mocked `IServiceScopeFactory`.
- Architecture test (Phase 1+) should assert that `CapitalUniversity.Sync.Student` does not reference `Hangfire.*` or `CapitalUniversity.Sync.Infrastructure`.

No automated tests written in Phase 5 — verification was runtime + raw SQL inspection.

---

## 12. Production-Readiness Reassessment

| Dimension | Status | Notes |
|---|---|---|
| Compiles cleanly | ✅ | Six projects (Abstractions/Infrastructure/Persistence/Student/Host) + the Sync.Student migration; `TreatWarningsAsErrors=true`; 0/0. |
| Runtime smoke | ✅ | First tick processes 48 with validation aggregation; second tick processes 0; SQL row count stable. |
| Schema migration | ✅ | `StudentInitial` applied at boot; new schema `sync_student`. |
| Module isolation | ✅ | No infra references in module project. |
| Queue isolation | ✅ | Routed to `students-sync`. |
| Pipeline metrics | ✅ | Single completion line, Phase 4 final refinement shape. |
| Checkpoint correctness | ✅ | Cursor advances; subsequent ticks idempotent. |
| Writer idempotency | ✅ | Upsert by stable external key. |
| Dashboard auth | ❌ | Carried debt. |

**Verdict:** First real production sync flow is in place and behaves correctly under the full architecture stack. Production-deployable for an internal pilot once the dashboard auth filter is replaced and the in-memory source is swapped for the real upstream adapter.

---

## 13. Phase Exit Checklist

- [x] `CapitalUniversity.Sync.Student` project created and registered in `CapitalUniversity.sln`
- [x] `ExternalStudent` + `StudentEntity` + `StudentSyncDbContext` defined
- [x] EF migration generated and auto-applied at host startup
- [x] In-memory `IExternalStudentSource` registered behind interface
- [x] `StudentExtractor` honors checkpoint, tracks max-cursor
- [x] `StudentMapper`/`StudentValidator`/`StudentWriter` implemented per Phase 4 contracts
- [x] `StudentSyncModule` composes the pipeline and advances the checkpoint
- [x] `AddStudentSync(IConfiguration)` DI extension wires everything from `Sync:Student` config
- [x] Host references the module, applies migration, registers `student-sync-pull` recurring on `students-sync` queue
- [x] First tick: 48 records persisted; 2 dropped by validator; 1 distinct warning
- [x] Second tick: 0 records processed (checkpoint recovery)
- [x] CorrelationId visible across dispatcher → executor → pipeline → module → repository
- [x] Module project references only `Sync.Abstractions` + EF packages
- [x] No public-surface changes to `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] Clean Debug build, `TreatWarningsAsErrors=true`, 0/0
- [x] Audit document produced

---

**Phase 5 complete. Stopping per instruction. Phase 6 (Push Sync — Internal → External) is next when invoked.**
