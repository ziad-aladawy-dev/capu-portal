# Sync Platform — Implementation Verification Audit

**Model document:** `docs/Sync_Platform_Model.md`
**Branch:** `sync_platform_hardening`
**Audit date:** 2026-06-02
**Auditor scope:** Verify implementation against documented architecture. No refactoring proposed. Only proven, evidence-backed findings recorded.

---

## 1. Model Extract

**Purpose.** Modular, bidirectional sync service between an external University System (authoritative source of truth) and the internal portal. Must support modular ownership, queue-based async processing, large datasets, retries/resiliency, easy future-module onboarding, scheduled + on-demand sync, consistency with scalability.

**Assumptions / Principles.**
- A1. External system is the source of truth; external updates override internal state; conflicts resolve in favor of external.
- A2. Synchronization must be asynchronous; never sync directly inside an API request (`Request → Store Change → Publish Queue Job → Background Worker`).
- A3. Modular ownership — each business module owns its sync implementation; the central host only schedules, dispatches, coordinates queues, handles retries, tracks execution.
- A4. Merge identity is the external key (`ExternalStudentId` / `ExternalId`); never internal IDs, row positions, or temporary identifiers.
- A5. Sync metadata fields on every synced entity: `ExternalId`, `ExternalUpdatedAt`, `ExternalVersion`, `LastSyncedAt`, `OriginSystem`. "implementation must be based on interfaces and inheritance to eliminate the chance of inconsistencies."
- A6. Conflict resolution matrix: External updated → External wins; Internal updated → push externally; Both → External wins; External deleted → Internal soft-delete; Internal deleted → push deletion externally.
- A7. Loop prevention via origin markers / version tracking / change hashes / origin metadata.
- A8. Incremental sync preferred (`UpdatedAt > LastCheckpoint`, RowVersion/CDC/Change Tracking); avoid repeated full scans.
- A9. Three sync modes: Scheduled Daily Sync (Hangfire recurring, "Daily at 2 AM"); On-Demand Admin Sync (enqueue only, never synchronous); Repair/Reconciliation.
- A10. Queue design: per-module `*.sync.pull` / `*.sync.push` Hangfire queues.
- A11. Outbox pattern for internal→external: `Save Internal Entity → Save Outbox Event → Commit Transaction → Enqueue job`; workers process outbox later.
- A12. Pull pipeline: Read Checkpoint → Fetch → Chunk → Enqueue → Workers → Validate → Map → Upsert → Store Checkpoint.
- A13. Push pipeline: Internal Update → Store Outbox Event → Enqueue → Worker Sends External Update → Mark Event Processed.
- A14. Batch processing: chunked (500 / 1000 / 2000 by payload size); never single transaction/memory-load/request.
- A15. Retry: async, exponential backoff (1m / 5m / 15m / 1h), idempotent; managed by Hangfire; dead-letter after exhaustion.
- A16. **Idempotency (flagged "Most Important"):** every operation safely repeatable; duplicate execution must not corrupt state. Protections: upserts, external unique keys, version checks, processed-message tracking.
- A17. Recommended tables: `sync_runs`, `sync_jobs`, `sync_failures`, `sync_checkpoints`, `sync_dead_letters`, `outbox_messages`, `inbox_messages`.
- A18. Tech stack: .NET Worker Service, Hangfire (jobs + recurring + retries), Hangfire SQL Server storage, EFCore.BulkExtensions for bulk ops, Manual DI registration.
- A19. `ISyncModule { string ModuleName; Task PullAsync(SyncContext); Task PushAsync(SyncContext); }`; `ISyncCheckpointStore { GetAsync(module); SaveAsync(module, checkpoint); }`.

**Expected flow.** Recurring/On-demand trigger → dispatcher opens a run + enqueues a Hangfire job on the module queue → worker resolves the module → Pull (checkpoint→extract→chunk→map→validate→upsert→advance checkpoint) or Push (read outbox→send→mark processed) → retries on failure → dead-letter on exhaustion.

**Responsibilities.** Host: schedule, dispatch, coordinate, retry, track. Module: domain extract/map/validate/write + checkpoint. Shared infrastructure: pipeline orchestration, batching, idempotency, persistence (runs/failures/checkpoints/dead-letters).

**Dependencies.** Hangfire + SQL Server storage; per-module SQL DBs (outbox); Core operational DB via a write gateway; EF Core.

**Failure / Retry.** Hangfire `AutomaticRetry`; exponential backoff; dead-letter store; orphan reaper; retention.

**State transitions (runs).** Enqueued → Running → Succeeded | Failed | DeadLettered | Cancelled.

---

## 2. Implementation Files Located

**Abstractions** (`5.Sync/CapitalUniversity.Sync.Abstractions`)
- Contracts: `ISyncModule.cs`, `ISyncPipeline.cs`, `ISyncDispatcher.cs`, `ISyncCheckpointStore.cs`, `IDataExtractor.cs`, `IRecordWriter.cs`, `IRecordMapper.cs`, `IRecordValidator.cs`, `IFailureRepository.cs`, `ICursorObserver.cs`, `ISyncModuleRegistry.cs`, `ISyncLogger.cs`, `SyncPipelineExtensions.cs`
- Models/Enums: `SyncContext`, `SyncCheckpoint`, `SyncResult`, `SyncPipelineRequest`, `SyncRunRecord`, `SyncRunMetadata`, `SyncFailureRecord`, `SyncDeadLetterRecord`, `SyncLimits`, `OutboxPayloadSerializer`, `TextHelpers`; `SyncDirection`, `SyncRunStatus`, `OutboxOperation`, `OutboxStatus`

**Infrastructure** (`CapitalUniversity.Sync.Infrastructure`)
- Pipeline: `SyncPipeline.cs`, `BatchProcessor.cs`, `IdempotencyHandler.cs`
- Dispatch/Execution: `SyncDispatcher.cs`, `SyncModuleRegistry.cs`, `Execution/SyncModuleExecutor.cs`, `SyncDeadLetterFilter.cs`
- Scheduling: `SyncRetentionService.cs`, `SyncRetentionRecurringTrigger.cs`, `SyncOrphanReaperService.cs`, `SyncOrphanReaperRecurringTrigger.cs`
- Observability/Alerting/Config: `QueueLagProbe`, `SyncLogger`, `SqlConnectivityHealthCheck`, `LoggingSyncAlertingHook`, `SyncHangfireOptions/QueuePool`, `SyncQueueConfigurationValidator`
- DI: `SyncInfrastructureServiceCollectionExtensions.cs`

**Persistence** (`CapitalUniversity.Sync.Persistence`)
- `Context/SyncDbContext.cs`; `Repositories/SyncCheckpointStore.cs`, `FailureRepository.cs`; entities `SyncRunEntity`, `SyncCheckpointEntity`, `SyncFailureEntity`, `SyncDeadLetterEntity`; configurations + migrations (`InitialCreate`, `AddHangfireJobIdToRuns`)

**Host** (`CapitalUniversity.Sync.Host`)
- `Program.cs` (DI, Hangfire, queue pools, auth, admin endpoints, migrations); `Scheduling/SyncRecurringJobsRegistrar.cs`, `Scheduling/SyncRecurringTrigger.cs`; HTTP adapters

**Modules** (Student, Staff, Courses, Finance, Schedules) — examined in depth via **Staff**: `StaffSyncModule.cs`, `Pull/{StaffExtractor, StaffMapper, StaffValidator, StaffWriter}.cs`, `Push/{StaffOutboxEntity, StaffOutboxExtractor, StaffOutboxMapper, StaffOutboxValidator, StaffOutboxWriter, StaffOutboxDispatch}.cs`, `Persistence/StaffSyncDbContext.cs`, `DependencyInjection/StaffSyncServiceCollectionExtensions.cs`

**Core write path** (`2.Core`)
- `CapitalUniversity.Core.Abstractions/Sync/ICoreWriteGateway.cs`; `CapitalUniversity.Core.Infrastructure/Sync/CoreWriteGateway.cs`

---

## 3. Per-Assumption Verification

| # | Assumption | Expected Behavior | Actual Implementation | Match |
|---|-----------|-------------------|-----------------------|-------|
| A1 | External is source of truth | External updates override internal | `CoreWriteGateway.UpsertAsync` external-wins guard: `RespectExternalUpdatedAt` skips when `incomingStamp <= currentStamp` (`CoreWriteGateway.cs:101-108`); stamps `OriginSystem="external"` (`:118`) | ✅ |
| A2 | Async only; never sync in request | Request → store → enqueue → worker | `SyncDispatcher` only opens a run + enqueues a Hangfire job; admin trigger endpoint enqueues only (`Program.cs:408-432`). No synchronous sync path | ✅ |
| A3 | Modular ownership; host only orchestrates | Per-module sync logic; host schedules/dispatches/retries/tracks | Each module is a separate project + DbContext + queue; `StaffSyncModule` delegates to pipeline scaffold (`StaffSyncModule.cs:42-60`); host does registry/dispatch/recurring/retention/reaper only | ✅ |
| A4 | Merge key = external id, never internal | Lookup by ExternalId | Gateway merges on `ExternallySourced.ExternalId`; drops + logs rows without it (`CoreWriteGateway.cs:57-75, 81-89`); pull key selector `s.ExternalStaffId` (`StaffSyncModule.cs:45`) | ✅ |
| A5a | Metadata fields present | ExternalId/ExternalUpdatedAt/ExternalVersion/LastSyncedAt/OriginSystem | `ExternallySourcedData` block carries all five; stamped by gateway (`CoreWriteGateway.cs:115-118, 126-127`) | ✅ |
| A5b | "based on interfaces **and inheritance**" | Inheritance-based metadata | Implemented via **interface + composition** (`IExternallySourced.ExternallySourced` `OwnsOne`), explicitly **replacing** the "prior inheritance-era schema" (`CoreWriteGateway.cs:13-25`) | ⚠️ |
| A6a | External updated → External wins | Newer external overwrites | External-wins guard (`CoreWriteGateway.cs:101-108`) | ✅ |
| A6b | Internal updated → push externally | Outbox push | Push flow: outbox extract → sink push → mark Processed (`StaffOutboxWriter.cs:54-62`) | ✅ |
| A6c | Both updated → External wins | External wins | Same guard as A6a | ✅ |
| A6d | External deleted → Internal soft-delete | Soft-delete on external delete | **No delete path in gateway**; `UpsertAsync` only inserts/updates; extractor emits upserts only. No soft-delete-on-external-delete evidenced | ❌ |
| A6e | Internal deleted → push deletion | Push delete op | `OutboxOperation.Delete=1` enum exists; push writer treats every row as a payload push regardless of `Operation` (`StaffOutboxWriter.cs`), no delete-specific external call evidenced | ⚠️ |
| A7 | Loop prevention | Origin markers / version / hash | `OriginSystem` stamped on pull (`CoreWriteGateway.cs:118`); `ExternalVersion` tracked. No explicit "skip push when OriginSystem==external" filter evidenced in the push extractor | ⚠️ |
| A8 | Incremental sync | UpdatedAt > checkpoint | `StaffExtractor` ISO-8601 `UpdatedAt` cursor + safety-buffer clawback; checkpoint advanced via `ICursorObserver` (`SyncPipelineExtensions.cs:77-103`) | ✅ |
| A9a | Scheduled daily at 2 AM | Hangfire recurring daily 02:00 | All module pull+push registered with **`Cron.Minutely()`** (`SyncRecurringJobsRegistrar.cs:49-107`) — every minute, not daily 2 AM | ❌ |
| A9b | On-demand admin sync, enqueue only | Admin trigger enqueues | `POST /admin/trigger/{module}` → `dispatcher.DispatchAsync` (`Program.cs:408-432`) | ✅ |
| A9c | Repair/reconciliation | Replay capability | `POST /admin/replay/{correlationId}` re-dispatches original (module,direction) with `ReplayOf` tag (`Program.cs:816-860`); `/requeue/{jobId}` (`:436-442`) | ✅ |
| A10 | Per-module pull/push queues | `*.sync.pull` / `*.sync.push` | Per-module dispatch queues via `Sync:ModuleQueues`; configurable queue pools w/ disjoint queues + worker counts (`Program.cs:138-166`); recurring triggers on a trigger queue | ✅ |
| A11 | Outbox: save entity + outbox atomically | Transactional outbox write | Push **consumes** per-module outbox tables. The atomic "save internal entity + outbox event in one transaction" is a Core/API write-side concern; **not evidenced** in the sync host (admin seed endpoints insert an outbox row standalone, `Program.cs:470-484`) | ⚠️ |
| A12 | Pull pipeline order | Checkpoint→Fetch→Chunk→Map→Validate→Upsert→Checkpoint | `SyncPipeline.RunAsync`: extract → `BatchProcessor.ChunkAsync` → idempotency dedup → Map → optional Validate → MergeWithRetry; checkpoint advanced post-success in `RunStandardPullAsync` (`SyncPipelineExtensions.cs:60-103`). Note: doc orders Validate before Map; impl maps then validates | ⚠️ |
| A13 | Push pipeline order | Outbox→Enqueue→Send→Mark Processed | `RunStandardPushAsync` → extract Pending → map (schema-version guard) → validate → sink push → set `Status=Processed` (`StaffOutboxWriter.cs:45-62`) | ✅ |
| A14 | Chunked batches 500/1000/2000 | Configurable chunk sizes | `BatchProcessor.ChunkAsync` streams bounded batches; default `BatchSize=500`, guard `(0, MaxBatchSize=1000]` (`SyncLimits.MaxBatchSize=1000`). Doc's 2000 example exceeds the hard cap | ⚠️ |
| A15a | Backoff 1m/5m/15m/1h | Exponential backoff stages | `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]` on `SyncModuleExecutor.ExecuteAsync` — exact match | ✅ |
| A15b | Dead-letter after exhaustion | Move to dead-letter | `SyncDeadLetterFilter` on terminal FailedState inserts `SyncDeadLetterEntity` + flips run → DeadLettered (idempotent on unique `IX_dead_letters_HangfireJobId`) | ✅ |
| A16 | **Idempotency (Most Important)** | Repeatable; no corruption on duplicate | Upsert by external key + version guard (`CoreWriteGateway.cs:98-120`); per-run `IdempotencyHandler` HashSet dedup; outbox `Status` gate (`StaffOutboxWriter.cs:45-48`) + at-least-once sink key (`:54-57`); dead-letter unique index; checkpoint-save-failure documented as replay-safe (`SyncPipelineExtensions.cs:93-102`) | ✅ |
| A17 | Recommended tables | runs/jobs/failures/checkpoints/dead_letters/outbox/inbox | `sync.runs`, `sync.failures`, `sync.checkpoints`, `sync.dead_letters` present (`SyncDbContext.cs:12-15`). `sync_jobs` table exists but **orphaned / not a DbSet** (`SyncDbContext.cs:17-20`). `outbox_messages` is realized as **per-module** outbox tables, not a single central table. `inbox_messages` **absent** | ⚠️ |
| A18a | .NET Worker + Hangfire + SQL storage | Worker host w/ Hangfire SQL | Host uses Hangfire + `UseSqlServerStorage` + `AddHangfireServer` (`Program.cs:119-166`) | ✅ |
| A18b | EFCore.BulkExtensions for bulk ops | Bulk insert/update | Write path uses plain EF: per-batch dictionary read + `SaveChangesAsync` loop (`CoreWriteGateway.cs:86-140`). **No BulkExtensions** in the verified write path | ⚠️ |
| A18c | Manual DI registration | Explicit per-module DI | `AddStudentSync/AddStaffSync/...` explicit (`Program.cs:109-113`); modules registered as `ISyncModule` singletons | ✅ |
| A19a | `ISyncModule` shape | `Task PullAsync(SyncContext)` | Impl: `Task<SyncResult> PullAsync(SyncContext, CancellationToken)` — adds return value + cancellation token (enhancement) | ⚠️ |
| A19b | `ISyncCheckpointStore` shape | `GetAsync(module)` / `SaveAsync(module, cp)` | Impl matches with added `CancellationToken` (`SyncCheckpointStore.cs:18-71`) | ✅ |

---

## 4. Divergences

### Divergence 1 — Scheduled cadence is minutely, not "daily at 2 AM"
- **Model:** A9 — "Scheduled Daily Sync … Hangfire Recurring Job, Daily at 2 AM."
- **Implementation:** Every module pull and push recurring job is registered with `Cron.Minutely()`.
- **Evidence:** `SyncRecurringJobsRegistrar.cs:49-107` (all ten `AddOrUpdate<SyncRecurringTrigger>(… cronExpression: Cron.Minutely())`). A `SyncRecurringTrigger` even adds deterministic 0–50 s stagger jitter specifically because "all 10 module triggers fire at :00 of every minute" (`SyncRecurringTrigger.cs:14-27, 47-65`).
- **Impact:** Far more aggressive than the documented daily reconciliation. Each minute fires 10 dispatch round-trips against the audit DB + Hangfire store. Within design — incremental cursor + idempotent writers make minutely safe — but the model's stated cadence and its "large student reconciliation" framing do not match. Retention/reaper crons are operator-configurable (`SyncRetentionOptions.CronExpression`, `SyncOrphanReaperOptions.CronExpression`), but module sync cadence is hardcoded minutely.
- **Severity:** Medium
- **Notes:** Likely a hardening/throughput decision. The doc presents 2 AM as an example; still, "daily" vs "every minute" is a material operational divergence worth flagging.

### Divergence 2 — Conflict matrix "External deleted → Internal soft-delete" not implemented
- **Model:** A6d — external deletions should soft-delete the internal record.
- **Implementation:** `CoreWriteGateway.UpsertAsync` has only insert/update branches; no delete or soft-delete handling. Pull extractors emit upserts; there is no "absent upstream ⇒ soft-delete" reconciliation.
- **Evidence:** `CoreWriteGateway.cs:96-138` (insert/update/skip only); `ICoreWriteGateway.cs:24-65` (no delete contract).
- **Impact:** External deletions do not propagate to internal soft-deletes through the verified path. A row deleted upstream simply stops being updated (stale, not retired). For update-only modules (Student/Staff use `AllowInsert=false`) this is a genuine functional gap vs the documented matrix.
- **Severity:** Medium
- **Notes:** May be intentionally deferred. No tombstone/soft-delete column flow was found in the sync write path.

### Divergence 3 — `inbox_messages` table absent; `sync_jobs` orphaned; outbox is per-module not central
- **Model:** A17 — recommended tables include `outbox_messages` and `inbox_messages`; `sync_jobs`.
- **Implementation:** `SyncDbContext` exposes `Runs`, `Checkpoints`, `Failures`, `DeadLetters` only. `sync.jobs` was created by the initial migration but is deliberately left orphaned (not a DbSet) because its content duplicated `SyncRunEntity.HangfireJobId`. No `inbox_messages` table/entity. Outbox is realized as one table per module (`StaffOutboxEntity`, `StudentOutboxEntity`, …), each in its own module DB.
- **Evidence:** `SyncDbContext.cs:12-20`; per-module outbox entities (`StaffOutboxEntity.cs`); per-module DbContext registration (`Program.cs:254-267`).
- **Impact:** Low. The recommendations are explicitly "Recommended." Pull is direct (extract→upsert) so an inbox is unnecessary; per-module outbox tables are a defensible modular-ownership choice (comment at `StaffOutboxEntity.cs:5-11`). The orphaned `sync_jobs` column is dead schema, harmless but untidy.
- **Severity:** Low

### Divergence 4 — Metadata realized via composition, not inheritance
- **Model:** A5 — "implementation must be based on interfaces **and inheritance** to eliminate the chance of inconsistencies."
- **Implementation:** `IExternallySourced` interface + an owned `ExternallySourcedData` value block (`OwnsOne`), explicitly replacing the previous inheritance-based schema.
- **Evidence:** `CoreWriteGateway.cs:13-25` ("reached through composition … same physical columns as the prior inheritance-era schema"); generic constraint `where TEntity : BaseEntity, IExternallySourced` (`ICoreWriteGateway.cs:52`).
- **Impact:** None functional — the interface enforces uniform presence of all five metadata fields, satisfying the doc's stated goal ("eliminate inconsistencies"). Only the literal mechanism (composition vs inheritance) differs.
- **Severity:** Low

### Divergence 5 — EFCore.BulkExtensions not used in the write path
- **Model:** A18 — Bulk Operations: EFCore.BulkExtensions.
- **Implementation:** `CoreWriteGateway` performs one batched read into a dictionary then a per-row apply + a single `SaveChangesAsync`. Push writer iterates the batch and calls `SaveChangesAsync` once. No `BulkInsert/BulkUpdate`.
- **Evidence:** `CoreWriteGateway.cs:86-140`; `StaffOutboxWriter.cs:41-95`.
- **Impact:** Low–Medium. At the documented 500–1000 row batch sizes, plain EF `SaveChanges` is acceptable; under very large reconciliations the recommended bulk path would reduce round-trips. Idempotency/correctness unaffected.
- **Severity:** Low

### Divergence 6 — Push pipeline maps before validating; pull order likewise Map→Validate
- **Model:** A12 Pull pipeline lists "Validate → Map → Upsert."
- **Implementation:** `SyncPipeline` maps first, then runs the optional validator on the mapped/internal record.
- **Evidence:** `SyncPipeline.RunAsync` (Map then optional Validate) per Section 2 contracts; push mirrors this (`StaffOutboxMapper` → `StaffOutboxValidator`).
- **Impact:** Low. Validating the mapped internal record is functionally sound (it validates the shape that will actually be persisted). The doc's ordering is presented as a conceptual pipeline, not a hard contract.
- **Severity:** Low

### Divergence 7 — `ISyncModule` signature extended
- **Model:** A19 — `Task PullAsync(SyncContext context)`.
- **Implementation:** `Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)` (and same for Push).
- **Evidence:** `StaffSyncModule.cs:42-60`; `SyncPipelineExtensions.cs:30-42`.
- **Impact:** None negative — adds a structured result (for run accounting) and cooperative cancellation. Strict superset of the documented contract.
- **Severity:** Low

### Divergence 8 — Loop-prevention relies on metadata stamping only; no explicit push-side origin filter
- **Model:** A7 — prevent the push→pull→push loop via origin markers.
- **Implementation:** Pull stamps `OriginSystem="external"`. Push reads outbox rows that are seeded explicitly by the write side. No verified "skip pushing rows whose last write originated from external" guard in the outbox extractor.
- **Evidence:** `CoreWriteGateway.cs:118`; `StaffOutboxExtractor.cs:26-32` (selects all `Pending` rows, no origin filter).
- **Impact:** Low in current design — outbox rows are produced only by genuine internal changes (admin seed / Core write side), so an externally-sourced update does not auto-enqueue a push. The loop is structurally avoided rather than filtered. If a future Core write-side outbox trigger fires on sync-applied updates, the documented origin guard would matter.
- **Severity:** Low

---

## 5. Hidden-Logic Review

Examined for non-obvious behavior, silent failure modes, and ordering hazards:

- **Checkpoint advance decoupled from extractor type (`SyncPipelineExtensions.cs:74-103`).** Cursor read through `ICursorObserver`; a mocked extractor that does not implement it simply skips advance rather than silently breaking. Checkpoint-save failure is caught, logged at Error, and explicitly relies on idempotent replay. *No verified issue.*
- **External-wins replay guard (`CoreWriteGateway.cs:101-108`).** Uses `<=` so equal `ExternalUpdatedAt` is treated as a benign replay and skipped (counted `SkippedNotNewer`). Correct for idempotency; will also skip a legitimately-changed row that kept the same timestamp — but that is an upstream timestamp-hygiene concern, not a code defect. *No verified issue.*
- **Outbox poison handling (`StaffOutboxWriter.cs:68-91`).** On exception, increments `AttemptCount`; at `MaxAttempts=5` flips `Status=Failed` and logs Error (manual intervention). `OperationCanceledException` is re-thrown (not counted as a poison attempt). `SaveChangesAsync` runs once after the loop so partial progress within a batch persists. *No verified issue.*
- **Status gate on push (`StaffOutboxWriter.cs:45-48`).** Re-checks `Status == Pending` before sending — guards against a concurrently-processed row. Combined with the stable Guid idempotency key passed to the sink (`:54-57`), gives at-least-once-safe push. *No verified issue.*
- **Payload schema-version enforcement (`StaffOutboxMapper.cs:15-21`).** Mismatched `PayloadSchemaVersion` throws (fail-loud) rather than silently pushing a truncated/incompatible payload. *No verified issue.*
- **`AllowInsert=false` for Student/Staff pull (`StaffWriter.cs:34`).** Update-only; rows absent in Core are counted `SkippedNotFound` and logged, never silently dropped. Intentional — these Core entities carry FKs (`StructureNodeId`) not sourced from sync. *No verified issue.*
- **`sync.jobs` orphaned column.** Dead schema, never read/written. Cosmetic only. *No verified issue.*

---

## 6. Flow Verification

**Flow A — Pull (External → Internal).**
- Expected: Read Checkpoint → Fetch → Chunk → Workers → (Validate) → Map → Upsert → Store Checkpoint.
- Actual: `RunStandardPullAsync` reads checkpoint → `SyncPipeline.RunAsync` extracts (cursor-bounded) → `BatchProcessor.ChunkAsync` → idempotency dedup → Map → optional Validate → `CoreWriteGateway.UpsertAsync` (external-wins, by ExternalId) → on success advance checkpoint via `ICursorObserver`.
- Match: **YES (with Map/Validate order swapped).**
- Differences: Validation runs on the mapped internal record (after Map), not before; an extra per-run idempotency dedup stage exists beyond the doc.

**Flow B — Push (Internal → External).**
- Expected: Internal Update → Store Outbox Event → Enqueue → Worker Sends External Update → Mark Processed.
- Actual: `RunStandardPushAsync` → `StaffOutboxExtractor` reads `Pending` (ordered, capped 500) → `StaffOutboxMapper` (schema-version guard + deserialize) → `StaffOutboxValidator` → `StaffOutboxWriter` sends to sink with idempotency key → `Status=Processed`/`ProcessedAt`, or `AttemptCount++`→`Failed` at max.
- Match: **YES** for the worker→send→mark portion.
- Differences: The "Store Outbox Event in the same transaction as the internal entity save" step (A11) is a write-side responsibility not verifiable in the sync host; the verified path begins at outbox consumption.

**Flow — Dispatch / Retry / Dead-letter.**
- Expected: enqueue → run → retry (1m/5m/15m/1h) → dead-letter on exhaustion.
- Actual: `SyncDispatcher` opens run (Enqueued) + enqueues + links Hangfire job → `SyncModuleExecutor` (`AutomaticRetry Attempts=4, Delays {60,300,900,3600}`, per-module concurrency lock) MarkStarted→Pull/Push→MarkSucceeded/RecordFailure → on terminal failure `SyncDeadLetterFilter` inserts dead-letter + flips run → DeadLettered (idempotent on unique index).
- Match: **YES.** Backoff stages match the doc exactly.
- Differences: Run terminal status (DeadLettered) owned by the filter, not the executor (executor stays Running across retries) — an implementation refinement, transparent to the documented contract.

**Flow — Scheduled / On-demand / Repair.**
- Expected: daily 2 AM recurring; admin on-demand; repair/replay.
- Actual: recurring jobs are **minutely** (Divergence 1); admin `POST /admin/trigger/{module}` enqueues; `POST /admin/replay/{correlationId}` + `/requeue/{jobId}` cover repair.
- Match: **PARTIAL** — modes all present; cadence diverges (minutely vs daily).

---

## 7. Final Verdict

### Scores
- **Architecture:** 9 / 10 — Clean modular separation (per-module project/DbContext/queue), single Core write chokepoint (`ICoreWriteGateway`), stateless pipeline, well-isolated host orchestration. Composition-over-inheritance for metadata is a sound deviation. Minor: per-module outbox vs central, orphaned `sync_jobs`.
- **Logic:** 8 / 10 — Idempotency, external-wins, replay-safety, checkpoint decoupling, poison handling, schema-version enforcement are all solid and exceed the doc. Docked for the unimplemented external-delete → soft-delete branch and absent explicit push-side loop filter.
- **Operational Safety:** 9 / 10 — Exact retry-backoff match, dead-letter via DB unique index (race-safe), orphan reaper, retention sweeper, queue-lag probe, health checks, fail-fast queue-config validation, JWT-gated admin surface with no dev bypass. Docked slightly for minutely cadence load profile (mitigated by stagger jitter).
- **Maintainability:** 9 / 10 — Heavily documented intent in code, sealed concrete components, no hidden inheritance/control flow, reusable pipeline scaffold (`SyncPipelineExtensions`). Dead `sync.jobs` schema is the main untidiness.

### Confirmed Issues
1. **(Medium)** Scheduled module sync runs **minutely**, not the documented "daily at 2 AM" — `SyncRecurringJobsRegistrar.cs:49-107`.
2. **(Medium)** Conflict-matrix rule "External deleted → Internal soft-delete" is **not implemented**; gateway has no delete path — `CoreWriteGateway.cs:96-138`.

### Model Violations (documented expectation contradicted)
- A9a cadence (minutely vs daily) — Divergence 1.
- A6d external-delete soft-delete — Divergence 2.
- A17 `inbox_messages` absent / central `outbox_messages` realized per-module / `sync_jobs` orphaned — Divergence 3 (recommendations, Low).
- A5 inheritance → composition — Divergence 4 (goal still met, Low).
- A18 EFCore.BulkExtensions not used — Divergence 5 (recommendation, Low).
- A12 Map/Validate ordering — Divergence 6 (Low).
- A19 `ISyncModule` extended signature — Divergence 7 (superset, Low).
- A7 push-side origin filter — Divergence 8 (structurally avoided, Low).

### False Positives (apparent gaps that are actually correct)
- "No transactional outbox in sync host" — correct by design; that write is a Core/API responsibility, outside the sync host's verified boundary (A11).
- "Checkpoint not advanced by the pipeline" — intentional; the module owns checkpoint advance via `ICursorObserver` after a successful run (`SyncPipelineExtensions.cs:74-103`).
- "Executor leaves run in Running on failure" — intentional; terminal status is owned by the dispatcher (enqueue failure) and the dead-letter filter (exhaustion).
- "`<=` external-wins skip drops changes" — correct idempotency behavior; only affects upstreams with non-monotonic timestamps.

### Findings Summary
The Sync Platform implementation **faithfully realizes the documented architecture** and **exceeds it** on the doc's own "Most Important" axis — idempotency and resiliency (per-run dedup, external-wins version guard, race-safe dead-lettering on a DB unique index, orphan reaper, retention, replay/requeue admin tooling, fail-fast config validation). The retry backoff matches the spec exactly. Two material divergences merit attention: the **minutely cadence** (vs documented daily) and the **missing external-delete → soft-delete** propagation. The remaining differences are Low-severity — either explicit "recommendations" in the doc, sound design refinements (composition over inheritance, Map-then-Validate, structured `SyncResult`), or write-side concerns outside the sync host's boundary. No correctness or safety defect was proven in the examined paths.
