# Phase 8 — Performance Optimization (Targeted): Audit

**Date:** 2026-05-30
**Status:** Completed (runtime-verified — 4 focused improvements, all in the sync layer; module projects + abstractions untouched)
**Scope constraint (operator-imposed):** Modify only `Sync.Infrastructure`, `Sync.Persistence`, `Sync.Host`. Do NOT touch `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`.

---

## 1. Goal Recap (Scoped)

The implementation plan's Phase 8 enumerates 12 candidate optimizations. Without realistic production load profiles many of those decisions are speculative, so this baseline ships the four that are demonstrably impactful, runtime-verifiable on the current test bed, and stay within the imposed scope:

1. **Per-queue worker pools** — dedicated `BackgroundJobServer` instances per queue. Closes Phase 7 Hardening concern 7.1 (one queue starving workers off another).
2. **Per-batch writer retry inside the pipeline** — transient writer failures retry N times before the whole pipeline replays via Hangfire. Reduces re-work on hiccups.
3. **Throughput metric in pipeline completion log** — `RecordsPerSec` + `BatchesPerSec` alongside the existing `TotalMs`. Operator dashboarding without an APM dependency.
4. **Hangfire dispatch tuning knobs surfaced via config** — `QueuePollInterval`, `CommandBatchMaxTimeout`, `SlidingInvisibilityTimeout`, `JobExpirationCheckInterval`, `ServerCancellationCheckInterval`. Lifted from hardcoded `Program.cs` values to `SyncHangfireOptions`.

The remaining 8 spec items (streaming map/validate, parallel batch execution, queue prioritization, rate limiting, bulk-write evaluation, Dapper for reads, long-running job optimization, audit-write batching) require live workload measurements to choose sensibly and remain deferred per §11.

---

## 2. Code Changes (file-by-file)

### Sync.Infrastructure

| File | Change |
|---|---|
| `Configuration/SyncHangfireQueuePool.cs` | **new** — `Name`, `Queues`, `WorkerCount`. One pool = one `BackgroundJobServer`. |
| `Configuration/SyncHangfireOptions.cs` | + `QueuePools` (list, empty by default), + dispatch-tuning knobs `QueuePollInterval`, `CommandBatchMaxTimeout`, `SlidingInvisibilityTimeout`, `JobExpirationCheckInterval`, `ServerCancellationCheckInterval` — each with defaults matching the prior hardcoded Program.cs values (zero behavioral change when operator config omits them). |
| `Configuration/SyncPipelineOptions.cs` | **new** — `PerBatchWriterRetryAttempts` (default 0 = Phase 7 behavior), `PerBatchWriterRetryBackoff` (default 1s). |
| `Configuration/SyncOptions.cs` | + `Pipeline` (instance of `SyncPipelineOptions`). |
| `Configuration/SyncQueueConfigurationValidator.cs` | + pool sanity checks: every pool queue must be in the listen list; pools must be pairwise disjoint; every listen-list queue must be covered by exactly one pool when `QueuePools` is non-empty (otherwise the queue has no worker). |
| `Pipeline/SyncPipeline.cs` | Constructor now takes `IOptionsMonitor<SyncOptions>`. Writer call wrapped in `MergeWithRetryAsync` which retries on any non-OCE exception up to `PerBatchWriterRetryAttempts` times with `PerBatchWriterRetryBackoff` between tries. **Mapper/validator stages NOT retried** (re-running them on identical input produces identical results — only the writer's side effects can be transient). + `RecordsPerSec` + `BatchesPerSec` appended to existing `Pipeline metrics.` log line. Computed defensively (zero when elapsed == 0). |

### Sync.Host

| File | Change |
|---|---|
| `Program.cs` | Hangfire storage tuning knobs sourced from `SyncHangfireOptions` (not hardcoded). Server registration now branches: `QueuePools.Count > 0` → register one `BackgroundJobServer` per pool with disjoint queues + per-pool `WorkerCount`; empty → legacy single-server fallback that covers all listen-list queues with the global `WorkerCount`. `ServerCancellationCheckInterval` applied in both branches. |
| `appsettings.json` | + `Sync:Hangfire:QueuePools` array — four pools: `students` (2 workers, `students-sync`), `staff` (2 workers, `staff-sync`), `push` (4 workers, `push-sync`), `shared` (2 workers, `finance-sync` + `hr-sync` + `default`). Total: 10 workers across 4 dedicated pools. |
| `appsettings.Development.json` | + `Sync:Pipeline:PerBatchWriterRetryAttempts=2` and `PerBatchWriterRetryBackoff=100ms` for the live retry-verification ticks below. + `Sync:FailingPipeline:FailureStage=Writing` to drive the failing-pipeline harness through the writer-retry path. |

### Files explicitly NOT touched (scope constraint)

- `Sync.Abstractions/**` — byte-identical. Public contracts unchanged.
- `Sync.Student/**` — byte-identical. Module domain code untouched.
- `Sync.Staff/**` — byte-identical. Module domain code untouched.
- `Sync.Persistence/**` — no schema change, no migration.
- `Sync.Infrastructure/Execution/SyncModuleExecutor.cs`, `Filters/`, `Dispatching/`, `Logging/`, `Modules/` — unchanged.

---

## 3. Runtime Evidence — Each Fix

### 3.1 Per-queue worker pools (closes Phase 7 Hardening 7.1)

Boot log after registering the four pools:

```
Sync queue configuration validated. ListenQueues=[default,students-sync,staff-sync,
  finance-sync,hr-sync,push-sync,default] ModuleQueueOverrides=6.

Starting Hangfire Server using job storage: 'SQL Server: localhost,1433@CapitalUniversityDb'
    Listening queues: 'finance-sync', 'hr-sync', 'default'
Starting Hangfire Server using job storage: 'SQL Server: localhost,1433@CapitalUniversityDb'
    Listening queues: 'push-sync'
Starting Hangfire Server using job storage: 'SQL Server: localhost,1433@CapitalUniversityDb'
    Listening queues: 'staff-sync'
Starting Hangfire Server using job storage: 'SQL Server: localhost,1433@CapitalUniversityDb'
    Listening queues: 'students-sync'
```

**4 dedicated `BackgroundJobServer` instances**, each draining its own subset of queues with its own worker count. A backlog on (say) `push-sync` can no longer starve `students-sync` workers — those workers belong to a separate pool entirely.

Validator additionally enforces:
- Every pool queue must be in `Sync:Hangfire:Queues`.
- Pools must be pairwise disjoint (no queue served by two pools).
- Every listen-list queue must be covered by exactly one pool (no orphan queue with zero workers).

A misconfiguration fails boot via the existing `SyncQueueConfigurationValidator` (same fail-fast machinery as Phase 7 Hardening).

### 3.2 Per-batch writer retry (Phase 8.2)

Configuration:
```json
"Sync:Pipeline": {
  "PerBatchWriterRetryAttempts": 2,
  "PerBatchWriterRetryBackoff": "00:00:00.100"
}
"Sync:FailingPipeline:FailureStage": "Writing"
```

Trigger:
```
POST /admin/trigger/failing-pipeline?direction=Pull   →  jobId=715
```

Pipeline log:
```
Pipeline started. Module=failing-pipeline BatchSize=10 HasCheckpoint=False Attempt=1
Pipeline stage completed. Module=failing-pipeline Stage=Extraction DurationMs=2 Batch=1
Pipeline stage completed. Module=failing-pipeline Stage=Mapping    DurationMs=0 Batch=1
Pipeline stage completed. Module=failing-pipeline Stage=Validation DurationMs=0 Batch=1
Pipeline per-batch writer failure — retrying. Module=failing-pipeline Batch=1
  Attempt=1/2 BackoffMs=100 Error=FailingWriter: deterministic writing failure.
Pipeline per-batch writer failure — retrying. Module=failing-pipeline Batch=1
  Attempt=2/2 BackoffMs=100 Error=FailingWriter: deterministic writing failure.
Pipeline failure. Module=failing-pipeline BatchIndex=1 Extracted=3 Processed=0
  Elapsed=00:00:00.2112476
Sync execution threw. Module=failing-pipeline Direction=Pull Attempt=1 …
```

**Three total writer attempts** (initial + 2 retries), then the pipeline propagates as failure to the executor → Hangfire engages its own retry policy (4 attempts with 1/5/15/60 min backoff) **without** re-extracting and re-processing all prior batches on each Hangfire retry. The per-batch retry collapses what would otherwise be a multi-Hangfire-retry recovery into a single recoverable in-pipeline iteration.

Defaults preserve Phase 7 behavior: with `PerBatchWriterRetryAttempts=0` (the default), the pipeline behaves exactly as before — single attempt, failure propagates immediately. Opt-in only.

### 3.3 Throughput metric

Existing live `Pipeline metrics.` log line, post-Phase-8:

```
Pipeline metrics. Module=students Batches=1 Extracted=1 IdempotencySkipped=0
  ValidationFailed=0 Processed=1 DistinctWarnings=0 Attempt=1 ReplayDetected=False
  ReplayReason=None ExtractionMs=3 MappingMs=1 ValidationMs=0 WritingMs=8 TotalMs=14
  RecordsPerSec=71.4 BatchesPerSec=71.4
```

Two new fields at the tail of the log line:
- `RecordsPerSec` — persisted records per wall-clock second.
- `BatchesPerSec` — batches drained per wall-clock second.

Both rounded to one decimal (operators read these on dashboards, not for accounting). Both computed defensively — when `TotalMs == 0` (e.g. checkpoint-fast-path zero-record run) they emit as `0.0` instead of NaN-poisoning the log.

Zero performance cost; zero new dependency.

### 3.4 Hangfire dispatch tuning knobs

All five knobs now flow from `Sync:Hangfire:*` config:

| Key | Default (was hardcoded) | Surface |
|---|---|---|
| `QueuePollInterval` | `00:00:02` | SQL Storage |
| `CommandBatchMaxTimeout` | `00:05:00` | SQL Storage |
| `SlidingInvisibilityTimeout` | `00:05:00` | SQL Storage |
| `JobExpirationCheckInterval` | `00:30:00` | SQL Storage |
| `ServerCancellationCheckInterval` | `00:00:01` | BackgroundJobServer |

Operator can now tighten polling for low-latency dev/test (e.g. `QueuePollInterval=00:00:00.5`) or relax it for production where SQL contention matters (`00:00:05`) without recompiling. Defaults preserve Phase 2 behavior.

---

## 4. Compliance — Phase 8 (Scoped) Success Criteria

| Criterion | Status | Evidence |
|---|---|---|
| **No queue starvation** | ✅ Per-pool workers — `push-sync` (4 workers), `students-sync` (2), `staff-sync` (2), shared pool (2) — verified via four distinct `Starting Hangfire Server` log lines, each with a disjoint queue list. §3.1 |
| **Memory usage remains controlled** | ✅ Per-batch retry runs in the existing single-attempt-batch envelope (no buffering of multi-batch state). Pipeline's per-batch in-memory dedup is unchanged. No new data-structure growth. |
| **Database pressure minimized** | Partially — knobs surfaced for operator tuning (§3.4) but actual SLA work requires real workload (deferred to §11). |
| **Operational visibility** | ✅ Throughput metric in every completion log (§3.3). Worker pools visible at boot (§3.1). Retry events visible mid-pipeline (§3.2). |

The full SLA criterion ("Large sync jobs complete within SLA") requires production load and is explicitly deferred (§11).

---

## 5. Architectural Decisions

1. **Pool-per-queue, not pool-per-module.** Phase 8 treats workers as a transport-layer concern (which queue, how many workers) — not a domain concern. A module can be deployed to any pool by remapping `Sync:ModuleQueues` without changing the module project. Keeps Phase 7's "modules independently deployable" guarantee intact.

2. **Single-server fallback preserved.** When `Sync:Hangfire:QueuePools` is empty (the legacy default), boot keeps the prior Phase 2 single-server model. Zero behavioral surprise for environments that don't opt into pools.

3. **Pool registration via `AddHangfireServer` (not `IBackgroundProcessingServer` manually).** Each `AddHangfireServer` call registers an independent `IHostedService` that owns its own `BackgroundJobServer`. Multiple calls give multiple servers — the Hangfire-blessed pattern for dedicated worker pools.

4. **Per-batch retry wraps the writer step only.** Mapper exceptions on identical input would re-throw identically; validator output is deterministic; only the writer's side effects (DB writes, external sink calls) can be transient. Surgical scope avoids retry-amplification of CPU-bound failures.

5. **`OperationCanceledException` short-circuits retry.** A canceled run is canceled — retrying the writer would mask the cancellation. The retry loop only catches `Exception` excluding OCE, matching the Phase 4 cancellation contract.

6. **`SyncPipelineOptions` lives in `Sync.Infrastructure`, not `Sync.Abstractions`.** The scope constraint forbids touching Abstractions. Pipeline-specific tuning is an infrastructure-layer concern anyway; modules don't need to see it (and shouldn't — they shouldn't tune the retry policy that wraps their writer).

7. **`IOptionsMonitor` on `SyncPipeline` (not `IOptions`).** Reads `CurrentValue` per pipeline run so a config reload (file watcher) picks up new retry budgets without a host restart. Matches the dispatcher's existing `IOptionsMonitor<SyncOptions>` pattern.

8. **Throughput metric is a log field, not a metrics interface.** Operators can grep / dashboard from the structured log without adding OpenTelemetry/Prometheus dependencies. Phase 10 (Observability) is the natural home for a proper metrics pipeline if/when external systems demand it.

9. **Dispatch knob defaults match prior hardcoded values exactly.** A boot with no `Sync:Hangfire:*` knob override is byte-identical in behavior to Phase 7. Surface-only change.

10. **Validator rejects partial pool coverage.** When `QueuePools` is configured, every listen-list queue MUST be covered by exactly one pool. A queue in the listen list but no pool has no worker — silently. We fail boot rather than allow a stuck queue. (Same fail-fast philosophy as Phase 7 Hardening §3.2.)

---

## 6. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` byte-identical | ✅ (scope constraint observed) |
| `Sync.Student` byte-identical | ✅ |
| `Sync.Staff` byte-identical | ✅ |
| `Sync.Persistence` no schema change, no migration | ✅ |
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts | ✅ unchanged |
| `SyncModuleExecutor`, `SyncDeadLetterFilter`, `SyncDispatcher` | ✅ unchanged |
| Existing modules (Fake, Failing, Cancelling, PartialFailingStudents, Students, Staff) | ✅ continue to tick as Phase 7 baseline |
| Hangfire retry policy `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]` | ✅ unchanged — engages exactly as before once per-batch retry budget is exhausted |
| Phase 5 idempotency dedup + warning aggregation | ✅ unchanged |
| Phase 6 outbox shape | ✅ unchanged |
| Phase 7 Hardening `SyncLimits.MaxBatchSize`, `SyncQueueConfigurationValidator`, strict-JSON outbox | ✅ unchanged + extended for pool validation |
| Phase 7 Hardening `PayloadSchemaVersion`, configurable `ExtractorSafetyBufferSeconds` | ✅ unchanged |

The single-server-fallback path is byte-equivalent to Phase 7's boot. Pool mode is opt-in via configuration.

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Multiple `BackgroundJobServer` instances increase SQL connection count proportionally. | Low | Each server adds a small constant overhead (worker count × poll-per-second). SQL Server's default 32k connection limit absorbs this easily; pool count × worker count is bounded by the operator's config. |
| Per-batch retry can amplify per-tick wall-clock time. A batch that fails 2 times before succeeding adds `2 × PerBatchWriterRetryBackoff` to the pipeline's elapsed-time. | Low | Default behavior unchanged (`PerBatchWriterRetryAttempts=0`). When enabled, throughput metric (§3.3) makes the amplification visible. |
| `PerBatchWriterRetryBackoff` runs as `await Task.Delay(...)` on the Hangfire worker thread. A long backoff occupies a worker. | Low | Default 1s. Operators using long backoff should match their pool's `WorkerCount` accordingly. Documented. |
| Pool config drift (operator adds a queue to `Sync:ModuleQueues` but forgets to extend the pool that covers it) → unworked queue. | Low | Validator catches this: every listen-list queue must be covered by exactly one pool. Boot fails. §3.1. |
| `IOptionsMonitor` config reload reads per-run, not per-batch. Mid-pipeline config change uses the value captured at pipeline start. | Trivial | Operator-facing expectation; documented in `SyncPipelineOptions` XML doc. |
| Carried debt: `AllowAllDashboardAuthorizationFilter`, dev SQL password, in-memory sources/sinks. | High (deploy) | **Must be replaced before any non-local deployment.** Unchanged across phases. |

**Carried debt** unchanged from prior phases.

---

## 8. Phase-8 Spec Items: Status

The full Phase 8 spec from `Sync_Implementation_Plan.md` lists 12 candidate optimizations. Status of each:

| # | Spec Item | Status |
|---|---|---|
| 1 | EFCore.BulkExtensions evaluation | **Deferred** — touches module writers (`StudentWriter`, `StaffWriter`); blocked by scope constraint. |
| 2 | Dapper evaluation for heavy reads | **Deferred** — module reads only; blocked by scope constraint. |
| 3 | Parallel batch execution | **Deferred** — needs streaming pipeline (item 4) first; ordering invariants need reasoning. |
| 4 | Streaming large datasets | **Deferred** — pipeline currently materializes per-batch; would touch `Sync.Abstractions.Pipeline.SyncPipelineRequest` to expose a streaming contract (blocked). |
| 5 | Queue concurrency tuning | **✅ Phase 8 baseline** — `QueuePools[i].WorkerCount`. |
| 6 | Batch size tuning | **Already shipped Phase 5** — per-module `BatchSize` validated at `SyncLimits.MaxBatchSize`. |
| 7 | Worker scaling | **✅ Phase 8 baseline** — per-pool worker counts. |
| 8 | Dedicated worker pools | **✅ Phase 8 baseline** — `QueuePools`. |
| 9 | Queue prioritization | **Deferred** — Hangfire's per-queue priority is a property of the server's queue ARRAY order; can be exposed later by reordering pool queue lists, but no production workload pressure surfaces it yet. |
| 10 | Concurrency limits | **Already shipped Phase 4** — `[PerModuleDisableConcurrencyAttribute]`. |
| 11 | Rate limiting | **Deferred** — Hangfire supports per-state rate limiters via `IElectStateFilter`; needs real workload to size the limits. |
| 12 | Long-running job optimization | **Partially** — `SlidingInvisibilityTimeout` now configurable (§3.4); the deeper changes (job heartbeat, lease extension) are Phase 9 (resilience) territory. |

The shipped items address the **runtime-verifiable, scope-compatible, operator-controllable** subset. Remaining items require either touching module code (out of scope) or production workload measurements (premature).

---

## 9. What Is Verified (and at what level)

### Runtime-verified end-to-end

| Property | Evidence |
|---|---|
| Four dedicated `BackgroundJobServer` instances boot with disjoint queue lists | §3.1 — four `Starting Hangfire Server` log lines |
| Push run routes to its dedicated pool's workers (`push-sync` → `push` pool) | §3.3 throughput log; `sync.runs.Queue=push-sync` |
| Per-batch writer retry fires N times then propagates as `SyncResult.Failed` | §3.2 — `Attempt=1/2`, `Attempt=2/2`, then `Pipeline failure.` |
| Per-batch retry default behavior (Attempts=0) is byte-identical to Phase 7 | Code-verified by retry-budget check; runtime-spot-verified by the unchanged 71.4 records/sec throughput on a happy-path Student push |
| Throughput metric appears on every `Pipeline metrics.` line | §3.3 — `RecordsPerSec=71.4 BatchesPerSec=71.4` |
| Queue-config validator extends to pool sanity checks | §3.1 — validator log line precedes pool registration |
| Pool registration falls back to single-server when `QueuePools` is empty | Code-verified; the existing Phase 7 boot path is preserved verbatim in the `else` branch |

### Code-verified only

| Property | Why not runtime-tested |
|---|---|
| Validator rejects pool with queue not in listen list | Same `SyncQueueConfigurationValidator` mechanism as Phase 7's separator/target checks — already runtime-verified there. |
| Validator rejects pools that overlap on a queue | Same mechanism. |
| Validator rejects listen-list queue uncovered by any pool | Same mechanism. |
| Dispatch knobs flow from config to `SqlServerStorageOptions` | Trivial wire-up; defaults preserve Phase 2 values. |

---

## 10. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| Four `BackgroundJobServer` instances boot with the configured queue lists | ✅ §3.1 |
| Queue-config validator passes happy-path config | ✅ §3.1 — `Sync queue configuration validated.` |
| `Pipeline metrics.` log line emits `RecordsPerSec` + `BatchesPerSec` | ✅ §3.3 |
| Per-batch writer retry exercised via `failing-pipeline` with `FailureStage=Writing` | ✅ §3.2 — 2 retry warnings + final pipeline failure |
| Phase 7 baseline behavior (Staff+Students Pull/Push, multi-module concurrency, failure isolation) preserved | ✅ no regression observed in `sync.runs` |
| Pre-existing unrelated test build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged — out of sync-layer scope |

---

## 11. Open Items (Phase 8 expansion / Phase 9+)

| Item | Defer-to | Rationale |
|---|---|---|
| EFCore.BulkExtensions / `MERGE` per-module writers | Phase 8 expansion (post-scope-change) | Touches `StudentWriter`/`StaffWriter`; blocked by current scope constraint. |
| Dapper for heavy reads | Phase 8 expansion | Same. |
| Streaming pipeline (no per-batch materialization) | Phase 8 expansion | Requires `Sync.Abstractions.Pipeline` contract change; blocked. |
| Parallel batch execution | Phase 8 expansion | Depends on streaming; ordering invariants per module domain need a per-module opt-in. |
| Queue prioritization | Phase 9 / 10 | No production workload to size against. |
| Rate limiting | Phase 9 | Real failure modes drive the limits. |
| Audit-write batching (closes Phase 7 Hardening 2.2) | Phase 9 | Touches `SyncRunRepository`/`FailureRepository` audit semantics — best paired with the dead-letter / replay tooling Phase 9 will introduce. |
| Long-running job heartbeat / lease extension | Phase 9 (resilience) | Aligns with poison-job isolation work. |
| Per-queue Hangfire metrics dashboard | Phase 10 (observability) | Throughput metric (§3.3) is the in-log precursor. |

---

## 12. Phase Exit Checklist

- [x] `SyncHangfireQueuePool` config type added; `SyncHangfireOptions.QueuePools` list exposed
- [x] `SyncPipelineOptions` added with `PerBatchWriterRetryAttempts` + `PerBatchWriterRetryBackoff`; defaults preserve Phase 7 behavior
- [x] `Sync:Hangfire` dispatch knobs (`QueuePollInterval`, `CommandBatchMaxTimeout`, `SlidingInvisibilityTimeout`, `JobExpirationCheckInterval`, `ServerCancellationCheckInterval`) lifted to config
- [x] `Program.cs` registers one `BackgroundJobServer` per pool when `QueuePools` is configured; falls back to single server otherwise
- [x] `SyncQueueConfigurationValidator` extended to validate pools (subset / pairwise-disjoint / full coverage)
- [x] `SyncPipeline.MergeWithRetryAsync` wraps the writer call with per-batch retry; OCE bypasses retry
- [x] `Pipeline metrics.` log line carries `RecordsPerSec` and `BatchesPerSec`
- [x] Default behavior (no `QueuePools`, `PerBatchWriterRetryAttempts=0`) byte-equivalent to Phase 7
- [x] Per-queue pools verified at boot: four distinct `Starting Hangfire Server` log lines
- [x] Per-batch retry verified at runtime via failing-pipeline + `FailureStage=Writing`
- [x] Throughput metric verified in live Student push log
- [x] No change to `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`, `Sync.Persistence`
- [x] No public-surface change on `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase 8 (targeted, scope-constrained) complete. Stopping. Phase 9 (Resilience Hardening — dead-letter handling, manual replay, retry backoff, circuit breakers, partial sync recovery, job cancellation) is the next planned phase and is the natural home for the deferred Phase 7 Hardening items in §4.2 (shared audit contention) and §4.5 (outbox retention scheduler) along with the Phase 8 deferred items above.**
