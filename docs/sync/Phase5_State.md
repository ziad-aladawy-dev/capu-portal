# Phase 5 — Consolidated State

**Date:** 2026-05-29
**Verdict (per third-party review):** Sync layer is **Crash-Safe** and **Replay-Safe**.
**Position in the implementation plan:** Phase 5 (First Real Module — Students.Sync) is the last delivered phase. Phase 6 (Push Sync) and beyond are not yet started.

This document indexes everything Phase 5 produced and replaces having to read seven audit files separately. Each prior audit remains the authoritative source for its own pass; this is the table of contents.

---

## 1. Audit File Index

In chronological order:

| # | Document | What it covers |
|---|---|---|
| 1 | [`Phase5_Audit.md`](Phase5_Audit.md) | First real module: `Sync.Student` project, `StudentSyncDbContext`, in-memory source, pipeline parts, recurring registration. Initial runtime verification. |
| 2 | [`Phase5_Hardening_Audit.md`](Phase5_Hardening_Audit.md) | Eight operational risks: concurrency lock, batch-size validator, replay-safe XML contract, race-safe writer, bounded warnings (cap 100), `Extracted` metric, retention strategy, partial-success replay. |
| 3 | [`SyncAuditRetention.md`](SyncAuditRetention.md) | Standalone strategy document — table-by-table windows, future cleanup mechanism, implementation triggers. **No scheduler shipped.** |
| 4 | [`Phase5_Hardening_Refinement_Audit.md`](Phase5_Hardening_Refinement_Audit.md) | Replay visibility + deterministic failure injection: `SyncContext.Attempt`, replay metrics, `CorrelationScopedFailingStudentWriter`, `FailureInjectionMode`, two runtime-verified scenarios. |
| 5 | [`Phase5_Hardening_Refinement2_Audit.md`](Phase5_Hardening_Refinement2_Audit.md) | Operational hazards closeout: central `MaxBatchSize` guard, cap lowered to 50, idempotency memory warning, OCE classification tightened, cleanup timeout, checkpoint-save → Error, push warning, extractor safety buffer. |

---

## 2. Code-State Snapshot

### Project layout (the actual files on disk)

```
src/5.Sync/
├── CapitalUniversity.Sync.Abstractions/                  contracts only — zero infra
│   ├── Models/{SyncContext, SyncDirection, SyncResult, SyncRunMetadata, SyncCheckpoint}.cs
│   ├── Pipeline/{ISyncPipeline, SyncPipelineRequest, IDataExtractor, IRecordMapper,
│   │             IRecordValidator, IRecordWriter}.cs
│   ├── Persistence/{ISyncCheckpointStore, ISyncRunRepository, IFailureRepository,
│   │                IDeadLetterRepository, SyncRunStatus, Records/*}.cs
│   ├── Logging/ISyncLogger.cs
│   ├── Modules/{ISyncModule, ISyncModuleRegistry}.cs
│   ├── Dispatching/ISyncDispatcher.cs
│   └── Errors/{SyncException, SyncModuleNotFoundException, SyncExecutionException}.cs
│
├── CapitalUniversity.Sync.Infrastructure/                Hangfire bridge
│   ├── Dispatching/SyncDispatcher.cs                     enqueue-only, opens audit row
│   ├── Execution/SyncModuleExecutor.cs                   [PerModuleDisableConcurrency, AutomaticRetry]
│   │                                                     CleanupTimeout = 15s
│   │                                                     Split OCE catch: legitimate vs spurious
│   ├── Filters/{SyncDeadLetterFilter, PerModuleDisableConcurrencyAttribute}.cs
│   ├── Pipeline/{SyncPipeline, BatchProcessor, ChangeDetector, IdempotencyHandler,
│   │             MappingEngine, MergeEngine}.cs          MaxBatchSize=1000, MaxDistinctWarnings=50,
│   │                                                     IdempotencyMemoryWarningThreshold=1M
│   ├── Logging/SyncLogger.cs                             MEL adapter
│   ├── Modules/SyncModuleRegistry.cs
│   └── Configuration/{SyncOptions, SyncHangfireOptions}.cs
│
├── CapitalUniversity.Sync.Persistence/                   EF Core audit
│   ├── Context/SyncDbContext.cs                          schema: sync
│   ├── Entities/{SyncRunEntity, SyncJobEntity, SyncCheckpointEntity, SyncFailureEntity,
│   │             SyncDeadLetterEntity}.cs
│   ├── Repositories/{SyncRunRepository, FailureRepository, DeadLetterRepository,
│   │                 EfSyncCheckpointStore}.cs           Idempotent dead-letter; transition guards
│   └── Migrations/{20260528151843_InitialCreate, 20260528180608_AddHangfireJobIdToRuns}
│
├── CapitalUniversity.Sync.Student/                       first real domain module
│   ├── Context/StudentSyncDbContext.cs                   schema: sync_student
│   ├── Domain/{ExternalStudent, StudentEntity}.cs
│   ├── Source/{IExternalStudentSource, InMemoryExternalStudentSource}.cs
│   ├── Extraction/StudentExtractor.cs                    SafetyBuffer = 1 second
│   ├── Mapping/StudentMapper.cs
│   ├── Validation/StudentValidator.cs
│   ├── Writing/StudentWriter.cs                          CAS retry on unique violation
│   ├── Modules/StudentSyncModule.cs                      Error log on checkpoint save fail,
│   │                                                     Warning on PushAsync no-op
│   ├── Configuration/{StudentSyncOptions, StudentSyncOptionsValidator}.cs   BatchSize ∈ (0, 1000]
│   └── Migrations/20260529111407_StudentInitial
│
└── CapitalUniversity.Sync.Host/                          web host
    ├── Modules/Fake/FakeSyncModule.cs                    Phase-1 simple fake
    ├── Modules/FakePipeline/{FakePipelineSyncModule + parts}              Phase-4 pipeline demo
    ├── Modules/FailingPipeline/{FailingPipelineSyncModule + parts}        Phase-4 failure verification
    │                                                                       Stages: Extraction, Mapping, Writing, SpuriousOce
    ├── Modules/CancellingPipeline/{CancellingPipelineSyncModule + parts}  Phase-4 cancellation verification
    │                                                                       CancellationCoordinator (correlation-scoped tokens)
    ├── Modules/PartialFailingStudents/{PartialFailingStudentSyncModule,   Phase-5 deterministic failure injection
    │   CorrelationScopedFailingStudentWriter, FailureInjectionMode}.cs    Modes: AfterBatch1Commit, BeforeCheckpointSave
    ├── Hangfire/AllowAllDashboardAuthorizationFilter.cs                   dev-only
    └── Scheduling/{FakeSyncRecurringTrigger, SyncRecurringJobsRegistrar}.cs
```

### Key constants (all in code; tweak in one place)

| Constant | Value | Where |
|---|---|---|
| `SyncPipeline.MaxBatchSize` | 1000 | `Sync.Infrastructure.Pipeline.SyncPipeline` |
| `SyncPipeline.MaxDistinctWarnings` | 50 | same |
| `SyncPipeline.IdempotencyMemoryWarningThreshold` | 1,000,000 | same |
| `SyncModuleExecutor.CleanupTimeout` | 15 s | `Sync.Infrastructure.Execution.SyncModuleExecutor` |
| `[AutomaticRetry(Attempts=4, DelaysInSeconds={60, 300, 900, 3600})]` | — | same |
| `StudentSyncOptionsValidator.MaxBatchSize` | 1000 | `Sync.Student.Configuration` |
| `StudentExtractor.SafetyBuffer` | 1 s | `Sync.Student.Extraction` |
| `StudentWriter.MaxAttempts` | 2 | `Sync.Student.Writing` |
| `BackgroundJobServerOptions.CancellationCheckInterval` | 1 s | `Sync.Host.Program` |
| `[PerModuleDisableConcurrency(timeoutSeconds: 3600)]` | — | `Sync.Infrastructure.Execution.SyncModuleExecutor` |
| Recurring crons | every minute | `Sync.Host.Scheduling.SyncRecurringJobsRegistrar` |

### Schemas

| Schema | Owner | Tables |
|---|---|---|
| `HangFire` | Hangfire SQL storage | created by Hangfire on first boot |
| `sync` | `SyncDbContext` (audit) | `runs`, `jobs`, `checkpoints`, `failures`, `dead_letters`, `__SyncMigrationsHistory` |
| `sync_student` | `StudentSyncDbContext` | `students`, `__StudentSyncMigrationsHistory` |

---

## 3. What Is Verified (and at what level)

### Runtime-verified end-to-end (multiple times across audits)

| Property | Last verified in | Evidence shape |
|---|---|---|
| Students success path: 50 → 48 written, checkpoint advances | `Phase5_State.md` regression §4 | `Pipeline metrics ... Batches=2 Extracted=50 ... Processed=48 ... Attempt=1 ReplayDetected=False` |
| Push no-op visibility | `Phase5_State.md` regression §4 | `Push not implemented for module students. ... do not treat this as a successful sync.` |
| Per-module concurrency lock | `Phase5_State.md` regression §4 | Two consecutive triggers of cancelling-pipeline run sequentially (≈6 ms gap between completion of #1 and start of #2) |
| Batch-size guard (module-level via options) | `Phase5_Hardening_Audit.md` §5 | `OptionsValidationException` on startup with `BatchSize=2000` |
| Retry replay | `Phase5_Hardening_Refinement_Audit.md` §4 + `Phase5_State.md` §4 | 5 explicit replay-oriented log lines + `AttemptCount=2` + 48 distinct rows |
| Checkpoint-lag replay | `Phase5_Hardening_Refinement_Audit.md` §5 | Paired-log narrative (`BeforeCheckpointSave injection` + next-run `Checkpoint advanced`) |
| Cancellation → Cancelled status | `Phase4_Final_Refinement_Audit.md` §3 | `sync.runs.Status=5`, 0 failures, 0 dead-letters, Information-level log |
| Spurious OCE → Failure (not Cancellation) | `Phase5_Hardening_Refinement2_Audit.md` §3.4 | `Pipeline spurious OperationCanceledException. ... Reporting as Failed.` + `Status=Running` (retry-bound, not Cancelled) |
| Idempotency-skip metric (`IdempotencySkipped`) | `Phase5_Hardening_Refinement2_Audit.md` §3.1 | `IdempotencySkipped=0` (or N) in completion log; `Extracted = IdempotencySkipped + ValidationFailed + Processed` |
| Replay metrics (`Attempt`, `ReplayDetected`, `ReplayReason`) | `Phase5_Hardening_Refinement_Audit.md` §4 | `Attempt=2 ReplayDetected=True ReplayReason=RetryReplay` on retry |
| State-transition rejection guards | `Phase4_Final_Refinement_Audit.md` §4 | `MarkStartedAsync ignored invalid transition. From=Cancelled\|Succeeded\|DeadLettered To=Running` |
| Safety-buffer back-date pickup | `Phase5_Hardening_Refinement2_Audit.md` §3.2 | Recurring tick after `Cursor=00:50:00` re-emits record #50 (`Batches=1 Extracted=1 Processed=1`) — idempotent, no row growth |

### Code-verified only

| Property | Why not runtime-tested | Where in code |
|---|---|---|
| `StudentWriter` unique-constraint race-handler convergence | Requires two independent processes racing the same key; module-concurrency lock now prevents the in-process case | `StudentWriter.UpsertBatchAsync` catch / retry |
| `MaxDistinctWarnings=50` overflow bucket | Requires an intentionally-misbehaving validator emitting >50 unique messages | `SyncPipeline.RecordWarning` |
| `IdempotencyMemoryWarningThreshold = 1 M` warning | Requires a multi-million-record run | `SyncPipeline.RunAsync` |
| `CleanupTimeout = 15 s` | Requires a slow / unresponsive SQL during shutdown | `SyncModuleExecutor` legitimate-OCE catch |
| Pipeline central `MaxBatchSize` guard | Requires a module that bypasses its own options validator; `StudentSyncOptionsValidator` runtime test exercises the same pattern | `SyncPipeline.RunAsync` head |
| `Enqueue → Failed` transition (dispatcher enqueue exception) | Requires a SQL outage during enqueue | `SyncDispatcher` `catch (Exception enqueueEx)` |
| `Running → Failed` transition | Reserved for future operator-driven path; no current call site | `SyncRunRepository.MarkFailedAsync` allow-list |
| `Failed → Running` rejection | Pre-condition (a Failed row) not naturally producible at runtime | `SyncRunRepository.MarkStartedAsync` switch default |
| Checkpoint-save failure → Error log | Requires inducing a SQL transient on the checkpoint table only | `StudentSyncModule` catch around `SaveAsync` |
| Parallel-correlation safety of `CorrelationScopedFailingStudentWriter` | Mechanism is `ConcurrentDictionary` + `Interlocked.Increment` — sound by primitive semantics | dictionary impl + `RunState` |

---

## 4. Open Items (Phase 6+)

Explicitly deferred, with rationale documented in their respective audits:

| Item | Defer-to phase | Document |
|---|---|---|
| Push sync (Internal → External) — `StudentSyncModule.PushAsync` placeholder + outbox routing | **Phase 6** | Implementation plan |
| Multi-module expansion (Staff, Courses, Schedule, Finance) | **Phase 7** | Implementation plan |
| Per-batch retry (vs whole-execution retry) | **Phase 8** | `Phase5_Hardening_Refinement2_Audit.md` §4 |
| Streaming / Bloom-filter idempotency dedup | **Phase 8** | `Phase5_Hardening_Refinement2_Audit.md` §4 |
| Bulk-write evaluation (`EFCore.BulkExtensions`, `MERGE`, Dapper) | **Phase 8** | Implementation plan |
| Audit-retention scheduler (Hangfire recurring `DELETE TOP N` cleanup) | **Phase 9 or 10** | `SyncAuditRetention.md` |
| Replace `AllowAllDashboardAuthorizationFilter` with real auth | **Pre-deploy** | Carried debt across all audits |
| Move dev SQL password to user-secrets | **Pre-deploy** | Carried debt |
| `SyncRunMetadata.TriggeredBy` typed enum | **Phase 9** (not blocking) | Carried debt |
| `SyncRunMetadata.Tags` deep-immutable | **Phase 9** (not blocking) | Carried debt |

---

## 5. Operational Quick-Reference

### Modules registered

| Module name | Purpose | Recurring? | Queue |
|---|---|---|---|
| `fake` | Phase-1 simple fake | every minute | `default` |
| `fake-pipeline` | Phase-4 pipeline demo | every minute | `default` |
| `students` | First real domain | every minute | `students-sync` |
| `failing-pipeline` | Failure-path verification | **no — admin trigger only** | `default` |
| `cancelling-pipeline` | Cancellation verification | **no — admin trigger only** | `default` |
| `partial-failing-students` | Partial-success replay verification | **no — admin trigger only** | `default` |

### Admin endpoints (dev-only — fronted by `AllowAllDashboardAuthorizationFilter`)

| Endpoint | Purpose |
|---|---|
| `GET /` | Service info (queues, schemas, dashboard URL) |
| `GET /healthz` | `200 healthy` |
| `GET /hangfire` | Hangfire dashboard |
| `POST /admin/trigger/{module}?direction=Pull\|Push&failureMode=AfterBatch1Commit\|BeforeCheckpointSave` | On-demand enqueue |
| `POST /admin/requeue/{jobId}` | Hangfire `Requeue` — fast-forward retries |
| `POST /admin/cancel/{correlationId:guid}` | Signal `CancellationCoordinator` for the run |
| `POST /admin/reset-failure-injection` | Clear `CorrelationScopedFailingStudentWriter` state |

### Config knobs

| Key | Default | Source |
|---|---|---|
| `Sync:Hangfire:ConnectionString` | (empty — required) | `appsettings.json` |
| `Sync:Hangfire:Queues` | `["students-sync", "finance-sync", "hr-sync", "push-sync", "default"]` | `appsettings.json` |
| `Sync:Hangfire:SchemaName` | `HangFire` | `appsettings.json` |
| `Sync:DefaultQueue` | `default` | `appsettings.json` |
| `Sync:ModuleQueues` | `{ "fake": "default", "fake-pipeline": "default", "students": "students-sync" }` | `appsettings.json` |
| `Sync:Student:ConnectionString` | dev-only inline (move to secrets) | `appsettings.Development.json` |
| `Sync:Student:BatchSize` | 25 (validated 0 < n ≤ 1000) | `appsettings.json` |
| `Sync:FailingPipeline:FailureStage` | `Extraction` (env override: `Sync__FailingPipeline__FailureStage`) | env |

---

## 6. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Five sync projects (`Abstractions`, `Infrastructure`, `Persistence`, `Student`, `Host`) compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| End-to-end success regression (Scenario A) | ✅ Students 50 → 48 |
| Push warning visibility (Scenario B) | ✅ `Push not implemented for module students. ...` |
| Per-module concurrency lock (Scenario C) | ✅ Sequential execution, ~6 ms gap |
| Retry replay with deterministic injection (Scenario D) | ✅ `AttemptCount=2`, 48 distinct, 4 replay-oriented log lines |
| Hangfire SQL storage, queues, retry policy, dashboard | ✅ Unchanged from Phase 2 baseline |
| `sync` schema (5 tables) + `sync_student` schema (1 table) | ✅ Migrations apply on boot |
| Pre-existing unrelated test build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged from earlier phases — out of sync-layer scope |

---

## 7. Verdict

The sync layer through Phase 5 is in a stable, crash-safe, replay-safe state. The phase delivered:

- A working first real domain module (`Students.Sync`) on the full pipeline stack.
- Eight operational hazards closed in Hardening 1.
- Replay visibility + deterministic failure-injection in Refinement 1.
- Six additional operational hazards closed in Refinement 2 (centralized batch guard, OCE classification, cleanup timeout, checkpoint Error escalation, safety buffer, push visibility).

Carried debt is limited to: dashboard auth filter (must replace pre-deploy), dev SQL secrets (move to user-secrets pre-deploy), and a handful of nominal tightenings (Tags deep-immutability, TriggeredBy enum). None of these block Phase 6.

**Recommended next focus**: Phase 6 (Push Sync — Internal → External). The `PushAsync` placeholder, the `push-sync` queue, and the dispatcher's queue-routing-by-direction (`{module}:Push` keying) are all already in place — Phase 6 is wiring a real outbox-style push pipeline into that scaffolding.

---

**End of Phase 5 consolidated state. Awaiting explicit Phase 6 (or Phase 8) instruction.**
