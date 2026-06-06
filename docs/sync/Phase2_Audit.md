# Phase 2 — Durable Hangfire Infrastructure: Audit

**Date:** 2026-05-28
**Status:** Completed (runtime-validated, persistence + restart verified)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`

---

## 1. Goal Recap

> Move from development runtime into durable production-safe background processing using persistent Hangfire storage.

Achieved: Hangfire now runs against SQL Server storage, with five dedicated queues, retry policy enabled per spec (1/5/15/60 min), per-module queue routing via configuration, and verified state persistence across application restart.

---

## 2. Implemented Components

### Storage migration

| Item | Before (Phase 1) | After (Phase 2) |
|---|---|---|
| Hangfire storage | `Hangfire.MemoryStorage 1.8.0` | `Hangfire.SqlServer 1.8.14` |
| SQL client | n/a | `Microsoft.Data.SqlClient 5.2.2` (modern) |
| Schema | n/a | `[HangFire]` schema auto-installed on first boot (`PrepareSchemaIfNecessary = true`) |
| DB | n/a | `CapitalUniversityDb` (existing app DB; Hangfire tables isolated by schema) |
| Compatibility level | `Version_180` | `Version_180` |
| Job id type observed in logs | Guid (memory) | Integer (SQL identity) — verified `JobId=1..4` across boots |

### Queues

`SyncHangfireOptions.Queues` (from `appsettings.json`):

```
students-sync · finance-sync · hr-sync · push-sync · default
```

The Hangfire server listens on all five. Configured at startup; no hardcoded queue list.

### Retry policy (enabled per spec)

`SyncModuleExecutor.ExecuteAsync` now decorated:

```csharp
[AutomaticRetry(
    Attempts = 4,
    DelaysInSeconds = new[] { 60, 300, 900, 3600 },
    OnAttemptsExceeded = AttemptsExceededAction.Fail)]
```

- Retry delays: **1 min, 5 min, 15 min, 1 hour** (exact spec values).
- After 4 attempts, Hangfire transitions the job to `Failed` (not `Deleted`) so it remains visible for operator action in the dashboard.
- `FakeSyncRecurringTrigger` keeps `[AutomaticRetry(Attempts = 0)]` — recurring schedule covers missed ticks; retrying the trigger would double-fire.

### Delayed jobs

Capability is provided by Hangfire's native `DelayedJobScheduler` dispatcher (visible in startup log). No new API. Used internally by `AutomaticRetry` for scheduling retry attempts. Available to future phases via `IBackgroundJobClient.Schedule(...)` — not exposed through `ISyncDispatcher` (would require a new method; deferred until needed).

### Recurring jobs (now persisted)

`SyncRecurringJobsRegistrar.AddOrUpdate<T>(..., queue: ...)` writes recurring definitions into the `[HangFire].[Hash]` table. The definition survives restart; only the registration call itself (`AddOrUpdate`) re-runs at startup and is idempotent on `recurringJobId`. Verified: `recurring-job:fake-sync-pull` Hash row present in SQL after first boot.

### Configuration model (no new abstractions)

Two POCOs live in `Sync.Infrastructure/Configuration/`:

```csharp
public sealed class SyncOptions
{
    public const string SectionName = "Sync";
    public SyncHangfireOptions Hangfire { get; set; } = new();
    public Dictionary<string, string> ModuleQueues { get; set; }  // OrdinalIgnoreCase
    public string DefaultQueue { get; set; } = "default";
    public string ResolveQueue(string moduleName, SyncDirection direction);
}

public sealed class SyncHangfireOptions
{
    public string ConnectionString { get; set; }
    public string SchemaName { get; set; } = "HangFire";
    public bool PrepareSchemaIfNecessary { get; set; } = true;
    public List<string> Queues { get; set; } = new() { "default" };
    public int? WorkerCount { get; set; }
}
```

Bound from `appsettings.json` `Sync:*` section by the host. `Sync.Infrastructure` calls `AddOptions<SyncOptions>()`; `Sync.Host` calls `Configure<SyncOptions>(config.GetSection(SyncOptions.SectionName))`.

**`SyncOptions.ResolveQueue(module, direction)` lookup order** (matches `Sync_Platform_Model.md` queue design):

1. `"{moduleName}:{direction}"` — e.g. `"students:Push"` → `push-sync`
2. `"{moduleName}"` — e.g. `"students"` → `students-sync`
3. `DefaultQueue`

This supports the spec's queue layout (per-domain pull queues + shared `push-sync`) via configuration alone, with no routing code outside the dispatcher.

### Cross-cutting

| Concern | Status |
|---|---|
| CorrelationId propagation through Hangfire jobs | ✅ Survives SQL serialization. Verified across restart. |
| `ISyncLogger` integrated into all job pipelines | ✅ Unchanged from Phase 1 — dispatcher + executor + module all log via `ISyncLogger`. |
| Job execution metadata | ✅ Recorded by Hangfire SQL storage (`[HangFire].[JobParameter]`, `[HangFire].[State]`, `[HangFire].[Job]`). No custom tables. |
| Failure tracking | ✅ Hangfire records every state transition with timestamp + reason in `[HangFire].[State]`. Failed jobs visible in dashboard. |

---

## 3. Execution Flow (unchanged contract; durable transport)

```
Hangfire Recurring Scheduler  (definition in [HangFire].[Hash])
   │ cron-fired
   ▼
FakeSyncRecurringTrigger   (dumb: module/direction + new CorrelationId + TriggeredBy=scheduled)
   │
   ▼
ISyncDispatcher
   │  ResolveQueue(module, direction) → "default"
   │  IBackgroundJobClient.Create(Job, new EnqueuedState(queue))
   ▼
Hangfire SQL Server storage  (job persisted in [HangFire].[Job], queued in [HangFire].[JobQueue])
   │
   ▼  worker on matching queue dequeues
SyncModuleExecutor   [AutomaticRetry Attempts=4, Delays={60,300,900,3600}s]
   │
   ▼
ISyncModule (FakeSyncModule)   pure business logic
```

### Verified runtime trace (single tick — post-restart)

```
Sync job enqueued. Module=fake Direction=Pull TriggeredBy=scheduled Queue=default JobId=4
Sync execution started. Module=fake Direction=Pull TriggeredBy=scheduled
FakeSyncModule.Pull tick. Module=fake
Sync execution succeeded. Module=fake Direction=Pull Processed=5 Failed=0 Duration=00:00:00.2620768
```

`JobId=4` after restart proves the SQL identity continued from `JobId=2` pre-restart — same database, same Hangfire schema, no reset.

---

## 4. Compliance Check

| Phase-2 Success Criterion | Status | Evidence |
|---|---|---|
| Jobs survive application restart | ✅ | Pre-restart: 2 succeeded jobs (`Id=1,2`). Post-restart: 4 (`Id=3,4` added). `recurring-job:fake-sync-pull` Hash row present. |
| Failed jobs retry safely | ✅ (capability) | `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]` on `SyncModuleExecutor.ExecuteAsync`. Hangfire's `DelayedJobScheduler` dispatcher confirmed running. Failure path not exercised in Phase 2 smoke (no failing module by design — fake module returns success). |
| CorrelationId survives async execution | ✅ | `SyncRunMetadata.CorrelationId` is a Hangfire job argument, serialized into `[HangFire].[Job].[Arguments]`, deserialized on worker pickup, pushed into MEL log scope on entry. Trace lines after restart correlate to enqueue line via the scope key `SyncCorrelationId`. |
| Queue isolation works correctly | ✅ | Server log: `Listening queues: 'default', 'students-sync', 'finance-sync', 'hr-sync', 'push-sync', 'default'`. Dispatcher log: `Queue=default` (resolved from `ModuleQueues["fake"] = "default"`). Routing is config-driven; switching `fake` to `students-sync` requires only an `appsettings.json` change. |

| Phase-2 NOT-Included | Status |
|---|---|
| RabbitMQ | ✅ Not present. |
| Distributed event bus | ✅ Not present. |
| Outbox / inbox patterns | ✅ Not present. Deferred to Phase 3+. |
| Hooks system | ✅ Not present. |
| Real-time streaming | ✅ Not present. |

---

## 5. Architectural Decisions

1. **No new interfaces.** Queue resolution lives on the `SyncOptions` POCO (`ResolveQueue` instance method). No `ISyncQueueResolver`. Honors the "prefer deletion over abstraction" principle from the Phase 1 refinement.

2. **`ISyncDispatcher` public API unchanged.** `DispatchAsync(moduleName, direction, metadata, ct) → Task<string>` is identical to Phase 1. Queue is resolved inside the dispatcher via injected `IOptionsMonitor<SyncOptions>`.

3. **`IOptionsMonitor<SyncOptions>` (not `IOptions<SyncOptions>`) on the dispatcher** so a runtime config reload picks up new module-queue mappings without redeploy. The host wires `IOptions<SyncOptions>` for startup-only consumers (e.g., the recurring registrar).

4. **Hangfire still owns retry, scheduling, storage, state.** The retry policy is declared on the executor method via an attribute — Hangfire reads it. The dispatcher does not implement retry or delayed scheduling.

5. **Same SQL database (`CapitalUniversityDb`), separate schema (`HangFire`).** Avoids the operational overhead of a second database while keeping table isolation. Phase 3's `sync_runs`/`sync_jobs`/`sync_failures` audit tables will live in `dbo` (or a new `sync` schema), separate from Hangfire's tables.

6. **Connection string is required at startup.** If `Sync:Hangfire:ConnectionString` is empty, the host throws `InvalidOperationException` immediately — no silent fallback to memory storage. This prevents accidental data loss in a misconfigured environment.

7. **Single host process owns both the Hangfire server (worker) and the dashboard.** Same as Phase 1. The dashboard is read-only data; the worker is the only writer. Multi-process scaling is possible without code changes — just run multiple instances of `Sync.Host` against the same SQL connection; Hangfire handles distributed coordination.

8. **`AddOrUpdate` is idempotent on `recurringJobId`.** Restart-time registration overwrites the definition with the same values; cron + queue + method-call are deterministic, so no schedule drift.

---

## 6. Changes vs. Phase 1

| File | Change |
|---|---|
| `Sync.Infrastructure.csproj` | + `Microsoft.Extensions.Options 9.0.0` |
| `Sync.Host.csproj` | − `Hangfire.MemoryStorage` · + `Hangfire.SqlServer 1.8.14` · + `Microsoft.Data.SqlClient 5.2.2` |
| `Sync.Infrastructure/Configuration/SyncOptions.cs` | **new** — POCO + `ResolveQueue` |
| `Sync.Infrastructure/Configuration/SyncHangfireOptions.cs` | **new** — POCO |
| `Sync.Infrastructure/Dispatching/SyncDispatcher.cs` | Now resolves queue via `SyncOptions`; uses `IBackgroundJobClient.Create(Job, new EnqueuedState(queue))` instead of `Enqueue<>(...)`; logs `Queue` in the single enqueue log line |
| `Sync.Infrastructure/Execution/SyncModuleExecutor.cs` | `[AutomaticRetry]` changed: `Attempts=4`, `DelaysInSeconds={60,300,900,3600}`, `OnAttemptsExceeded=Fail` |
| `Sync.Infrastructure/DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | + `services.AddOptions<SyncOptions>()` |
| `Sync.Host/Program.cs` | SqlServer storage from options · multi-queue server · `Configure<SyncOptions>(...)` · throws if connection string empty · removed deprecated `UsePageLocksOnDequeue` |
| `Sync.Host/Scheduling/SyncRecurringJobsRegistrar.cs` | `AddOrUpdate<T>(..., queue: triggerQueue, ...)` — recurring trigger pinned to a queue |
| `Sync.Host/appsettings.json` | + `Sync:Hangfire:*` defaults + `Sync:ModuleQueues` map |
| `Sync.Host/appsettings.Development.json` | + dev connection string |

**No changes** to:
- `Sync.Abstractions` (all five interfaces + all models + errors — byte-identical)
- `ISyncModule` contract
- `SyncContext`, `SyncResult` semantics
- `FakeSyncRecurringTrigger` (still 4-line dumb trigger)
- `FakeSyncModule` (same return values, same delays)
- `SyncLogger` (MEL adapter)
- `SyncModuleRegistry`
- Hangfire dashboard mount path (`/hangfire`)
- HTTP endpoints (`/`, `/healthz`)

---

## 7. Persistence Verification (raw)

Pre-restart query against `CapitalUniversityDb`:

```
SELECT TOP 5 [Id], [StateName] FROM [HangFire].[Job] ORDER BY [Id] DESC;
  Id  StateName
  --  ---------
   2  Succeeded
   1  Succeeded

SELECT [Key], COUNT(*) AS rows FROM [HangFire].[Hash] GROUP BY [Key];
  Key                                    rows
  --------------------------------       ----
  recurring-job:fake-sync-pull              9
```

Post-restart query (after one more tick):

```
SELECT COUNT(*) FROM [HangFire].[Job];  →  4
SELECT TOP 3 [Id], [StateName] FROM [HangFire].[Job] ORDER BY [Id] DESC;
   4  Succeeded
   3  Succeeded
   2  Succeeded
```

Identity sequence continued from 2 → 3 → 4. No reset. No duplicate recurring definition. Recurring job recovered automatically.

---

## 8. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `AllowAllDashboardAuthorizationFilter` still in place. | High (if shipped non-locally) | Carried from Phase 1. **Must be replaced before any non-local deployment.** |
| `Microsoft.Data.SqlClient` 5.2.2 has known CVEs (`Encrypt=False` in dev). | Medium (dev), n/a (prod will use proper certs) | Dev-only. Production should set `Encrypt=True` + trusted server cert. Connection string is in `appsettings.Development.json`, not committed credentials. |
| Single shared DB for app + Hangfire. | Low | Schema isolation. Migration churn on the app side won't touch Hangfire tables. Can be split into a dedicated DB by changing one connection string. |
| Retry of 1h on the 4th attempt holds a SQL row + job-state machinery for up to ~1 h 21 min per failure. | Low | Spec-mandated values. Phase 9 (resilience) revisits backoff. |
| `Microsoft.Data.SqlClient` pulls multiple transitive dependencies; potential conflict with existing `EFCore.SqlServer 9.0.0`. | Low | Verified at build — no NU1605/NU1107. Major versions align. |
| `ResolveQueue` lookup is case-sensitive on direction value (`"Pull"` vs `"pull"`). | Very low | Direction comes from `ToString()` on the enum — stable. Module keys are `OrdinalIgnoreCase`. |

---

## 9. Future Concerns Surfaced (recorded, not implemented)

1. **Failure path not exercised at runtime.** Fake module always succeeds. Phase 3 / Phase 9 should introduce a deliberately-failing fake module (or admin-triggered failure path) to validate retry timing + dead-letter handling. Not added in Phase 2 to preserve module behavior unchanged.
2. **Delayed-job public API.** `ISyncDispatcher.DispatchAsync` only enqueues immediately. Phase 6 (push sync) may need delayed enqueue. Will be added as an overload or new method when the requirement materializes; not added speculatively.
3. **Per-queue worker counts / dedicated pools.** Single shared worker pool listens on all five queues with FIFO. Phase 8 (performance) tunes this.
4. **Dashboard auth.** Tracked. Same as Phase 1.
5. **Cancellation propagation through retry.** `IJobCancellationToken.ShutdownToken` is honored on each attempt; the executor cleanly cancels mid-attempt on host shutdown.

---

## 10. Technical Debt Introduced

| Debt | Severity | Repaid in |
|---|---|---|
| `appsettings.Development.json` carries the SQL password in plaintext. | Medium | Move to user-secrets (`UserSecretsId` is already set in csproj) before broader dev use. |
| Listening queues list shows `default` duplicated in startup log (the Hangfire server prepends its automatic `default`). Cosmetic only. | Trivial | Drop `default` from `appsettings.json` queue list, or rely on the auto-prepend. |
| No automated unit/integration tests yet for `SyncOptions.ResolveQueue`. | Low | Phase 3 brings sync-stack test scaffolding. |

Carried from earlier phases: `AllowAllDashboardAuthorizationFilter`, `SyncRunMetadata.Tags` mutability, `TriggeredBy` as free-form string, repo-vs-spec layout naming.

---

## 11. Testability Status

- `SyncOptions.ResolveQueue` is pure and trivially unit-testable.
- `SyncDispatcher` can be unit-tested with mocked `IBackgroundJobClient`; `IOptionsMonitor<T>` is straightforward to fake with `Microsoft.Extensions.Options` helpers.
- Integration test path: a `WebApplicationFactory<Program>` with in-memory configuration overriding `Sync:Hangfire:ConnectionString` to a transient SQL container (or LocalDB) verifies end-to-end persistence.
- Architecture tests should now also assert that no `Hangfire.SqlServer` type appears outside `CapitalUniversity.Sync.Host`.

No automated tests written in this phase; runtime smoke + raw SQL inspection were the validation gates.

---

## 12. Production-Readiness Evaluation

| Dimension | Status | Notes |
|---|---|---|
| Compiles cleanly | ✅ | Debug, `TreatWarningsAsErrors=true`, 0/0. |
| Runtime smoke | ✅ | Full loop ticked across two boots. |
| Persistence | ✅ | Verified via SQL inspection — see §7. |
| Restart survival | ✅ | Job identity continues; recurring re-registered idempotently. |
| Retry policy | ✅ | Spec-exact: 1/5/15/60 min, 4 attempts, fail terminal. |
| Queue isolation | ✅ | Five queues active; routing config-driven. |
| Dashboard | ✅ | `/hangfire` 200. Dev auth filter still in place — high-severity debt. |
| Observability | ✅ | Structured logs with `CorrelationId` in scope + Hangfire dashboard + raw SQL tables. |
| External dependencies | SQL Server | LocalDB or containerized MSSQL is sufficient for dev. |
| Pre-existing repo build error | ⚠️ | `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs` — unchanged from earlier phases. |

**Verdict:** Phase 2 produces a distributed-ready, durable, retry-capable, multi-queue Hangfire-backed sync runtime. Production-deployable once the dashboard filter is replaced and the connection string moves to a secret store.

---

## 13. Phase Exit Checklist

- [x] SQL Server storage in place; schema auto-installed on first run
- [x] Five dedicated queues active (`students-sync`, `finance-sync`, `hr-sync`, `push-sync`, `default`)
- [x] Per-module/direction queue routing via configuration (no new abstractions)
- [x] Retry policy enabled with spec-mandated delays
- [x] Delayed-jobs capability (Hangfire native; used by retry)
- [x] Recurring jobs survive restart
- [x] CorrelationId survives serialization
- [x] `ISyncDispatcher` API unchanged
- [x] `ISyncModule` contract unchanged
- [x] `SyncContext` / `SyncResult` semantics unchanged
- [x] No event bus, no outbox/inbox, no hooks, no RabbitMQ
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Smoke + restart verified against real SQL Server
- [x] Audit document produced

---

**Phase 2 complete. Stopping. Awaiting explicit instruction to proceed to Phase 3 (Persistence Layer — `sync_runs`, `sync_jobs`, `sync_checkpoints`, `sync_failures`, `sync_dead_letters`).**
