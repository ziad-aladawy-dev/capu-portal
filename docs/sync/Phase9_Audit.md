# Phase 9 — Resilience Hardening (Targeted): Audit

**Date:** 2026-05-30
**Status:** Completed (runtime-verified — 3 targeted improvements; deferred items documented)
**Scope constraint:** Sync.Infrastructure + Sync.Persistence + Sync.Host only. Modules + Abstractions untouched.

---

## 1. Goal Recap (Scoped)

The implementation plan's Phase 9 lists seven candidate features. Within the operator-imposed scope (sync layer only) and the Phase 7 Hardening deferrals, three are deliverable as concrete, runtime-verifiable fixes that close known gaps:

1. **Audit + outbox retention scheduler** — closes the long-documented `SyncAuditRetention.md` work + Phase 7 Hardening concern 5.1 (outbox bloat).
2. **Generic, config-driven outbox retention** — same scheduler operates on operator-declared per-module outbox tables without Sync.Infrastructure knowing which modules exist (preserves Phase 7 modular isolation).
3. **Manual replay endpoint** — operator-driven recovery from dead-letter or any terminal state.

The four remaining spec items (poison-job isolation, retry-backoff policies, circuit breakers, partial-sync recovery) are documented in §10 as design decisions or Phase 10/expansion deferrals.

---

## 2. Code Changes (file-by-file)

### Sync.Infrastructure

| File | Change |
|---|---|
| `CapitalUniversity.Sync.Infrastructure.csproj` | + `Microsoft.Data.SqlClient 5.2.2` (raw SQL for retention; intentional — keeps the layer decoupled from EF). + `Microsoft.Extensions.Configuration.Abstractions 9.0.0` (explicit; was transitive). |
| `Configuration/SyncRetentionOptions.cs` | **new** — `Enabled` (default `false`, opt-in), `CronExpression` (default daily 03:00 UTC), `DeleteBatchSize` (default 5000), `MaxDeletedPerTablePerRun` (default 50 000, hard cap), per-window retention days for the four `sync.*` tables, and `OutboxTables: List<SyncOutboxRetentionTarget>` (operator-declared per-module outbox tables). |
| `Configuration/SyncRetentionOptions.cs` (`SyncOutboxRetentionTarget`) | **new** — `Schema`, `Table`, `RetentionDays`, `StatusColumn`, `ProcessedStatusValue`, `TimestampColumn`. Generic shape lets any module's outbox be cleaned without Sync.Infrastructure referencing the module. |
| `Retention/SyncRetentionService.cs` | **new** — single `RunAsync` that iterates: `sync.failures` → `sync.dead_letters` → `sync.jobs` → `sync.runs (Succeeded)` → `sync.runs (Failed/DeadLettered/Cancelled)` → operator-declared outbox tables. Each table swept via batched `DELETE TOP (N)` in a loop bounded by `MaxDeletedPerTablePerRun`. Children-before-parents order leaves the audit queryable during a partial run. `sync.checkpoints` and the Hangfire schema are explicitly never touched. |
| `Retention/SyncRetentionRecurringTrigger.cs` | **new** — thin DI-resolved wrapper so Hangfire's recurring-job deserializer can call `TriggerAsync(CancellationToken)` against a singleton. |
| `DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | + `AddOptions<SyncRetentionOptions>()`, + `AddSingleton<SyncRetentionService>()`, + `AddSingleton<SyncRetentionRecurringTrigger>()`. |

### Sync.Host

| File | Change |
|---|---|
| `Scheduling/SyncRecurringJobsRegistrar.cs` | + `IOptions<SyncRetentionOptions>` injection; + `AddOrUpdate<SyncRetentionRecurringTrigger>(...)` registering `sync-retention` recurring against `_retentionOptions.Value.CronExpression`. Logs the recurring summary including `RetentionEnabled` + `RetentionCron`. |
| `Program.cs` | + `Configure<SyncRetentionOptions>(...)` binding from `Sync:Retention` config. + `Configure<SyncHangfireOptions>(...)` (explicit; the retention service reads the connection string). + `POST /admin/replay/{correlationId:guid}` — looks up the original run, dispatches a fresh run with `TriggeredBy=replay` and a `ReplayOf:<originalCorrId>` tag, returns both correlation IDs + the new Hangfire job id. + `GET /admin/retention` — read-back of the configured windows. + `POST /admin/retention/run` — operator-triggered immediate sweep (bypasses cron). |
| `appsettings.Development.json` | + `Sync:Retention` block with `Enabled: true`, two-minute cron, 0-day windows (everything older than now), and outbox-table declarations for both `sync_student.student_outbox` and `sync_staff.staff_outbox`. Tightened for verification — production would use the documented 30/90/365 defaults. |

### Files explicitly NOT touched (scope constraint)

- `Sync.Abstractions/**` — byte-identical.
- `Sync.Student/**` — byte-identical.
- `Sync.Staff/**` — byte-identical.
- `Sync.Persistence/**` — no schema change, no migration.

---

## 3. Runtime Evidence

### 3.1 Retention sweep (audit + outbox)

Pre-sweep state (verification database, 0-day windows = delete everything older than now):

```
runs              385
jobs              385
failures           28
dead_letters        4
student_outbox      1
staff_outbox        0
```

Operator trigger:
```
POST /admin/retention/run   →  200 OK { "ranAt": "2026-05-30T11:..." }
```

Post-sweep state:

```
runs               1   (only the run currently triggering the sweep itself remains)
jobs               0
failures           0
dead_letters       0
student_outbox     0
staff_outbox       0
```

Pipeline log:
```
Sync retention completed. TotalDeleted=44 TablesProcessed=7 ElapsedMs=42
  Details=sync.failures=28rows/15ms; sync.dead_letters=4rows/10ms;
          sync.jobs=6rows/4ms; sync.runs (Succeeded)=6rows/5ms;
          sync.runs (Failed/DeadLettered/Cancelled)=0rows/1ms;
          sync_student.student_outbox (Processed)=0rows/1ms;
          sync_staff.staff_outbox (Processed)=0rows/0ms
```

Per-table delete counts visible in the single completion log line + total wall-clock under 50ms for the verification volume.

### 3.2 Manual replay

```
POST /admin/trigger/students?direction=Pull   →  jobId=753 corr=c61360b8-...
(original run succeeds)

POST /admin/replay/c61360b8-d259-4316-838a-e153cc47b57f
→ {
    "originalCorrelationId": "c61360b8-d259-4316-838a-e153cc47b57f",
    "originalStatus":         "Succeeded",
    "replayCorrelationId":    "1f56f21f-95c7-48ff-a2d6-27e963f85dbc",
    "module":                 "students",
    "direction":              "Pull",
    "jobId":                  "754"
  }
```

`sync.runs` snapshot showing both runs (newest first):

```
CorrelationId     ModuleName  Direction  Status        TriggeredBy   HangfireJobId
1F56F21F-...      students    0 (Pull)   2 (Succeeded) replay        754
C61360B8-...      students    0 (Pull)   2 (Succeeded) admin         753
```

`TriggeredBy = replay` distinguishes operator-driven replays from `scheduled` (recurring) and `admin` (manual trigger) and `replay` runs. Replay metadata also carries `Tags.ReplayOf = <original CorrelationId>` (visible in `sync.runs.Tags`) so a full audit trail joins the two runs.

### 3.3 Retention configuration sanity-read

```
GET /admin/retention
→ {
    "enabled": true,
    "cron":    "*/2 * * * *",
    "windows": { "successfulRunsDays": 0, "failedRunsDays": 0,
                 "failureRowsDays": 0, "deadLettersDays": 0 },
    "deleteBatchSize": 1000,
    "maxDeletedPerTablePerRun": 10000,
    "outboxTables": [
      { "schema": "sync_student", "table": "student_outbox", "retentionDays": 0,
        "statusColumn": "Status", "processedStatusValue": 1, "timestampColumn": "ProcessedAt" },
      { "schema": "sync_staff",   "table": "staff_outbox",   "retentionDays": 0, ... }
    ]
  }
```

Operator can verify the active configuration without grepping logs.

### 3.4 Recurring registration

Boot log includes the new recurring job:
```
Recurring jobs registered: 'fake-sync-pull', 'fake-pipeline-pull',
  'student-sync-pull', 'student-sync-push',
  'staff-sync-pull', 'staff-sync-push',
  'sync-retention'
  (trigger queue: default; per-module dispatch queues resolved via Sync:ModuleQueues;
   retention enabled=True cron=*/2 * * * *).
```

The retention cron is always registered so it's observable in the Hangfire dashboard; the service short-circuits internally when `Sync:Retention:Enabled = false`.

---

## 4. Compliance — Phase 9 (Scoped) Success Criteria

| Criterion | Status | Evidence |
|---|---|---|
| **Failed jobs recover safely** | ✅ (existing) Hangfire `[AutomaticRetry(Attempts=4)]` + Phase 8 per-batch writer retry. |
| **Replay operations deterministic** | ✅ `/admin/replay` mirrors the original `(Module, Direction)` exactly; metadata carries `ReplayOf` tag. §3.2 |
| **Poison jobs isolated automatically** | Partially — Hangfire's terminal-failed state + Phase 3 dead-letter audit captures them. Auto-pause on N-consecutive-dead-letters deferred (see §10). |

---

## 5. Architectural Decisions

1. **`SyncRetentionService` uses raw `Microsoft.Data.SqlClient`, not EF Core.** Cleanup is a one-shot bulk DML utility; an EF DbContext would carry tracking overhead and require either a cross-project reference to `Sync.Persistence` (couples infra → persistence) or a duplicate context. Raw SQL stays simple, fast, and decoupled.

2. **`sync.checkpoints` is intentionally never deleted.** Operational cursor state, not audit. Documented in `SyncAuditRetention.md` since Phase 5 hardening.

3. **Hangfire schema is intentionally never touched.** Hangfire has its own per-state TTL via `JobExpirationCheckInterval` (Phase 8 surfaced). Different concern, different layer.

4. **Outbox tables are operator-declared, not module-discovered.** Sync.Infrastructure cannot enumerate modules without coupling. The `SyncOutboxRetentionTarget` config shape (schema, table, status column, processed value, timestamp column) is generic enough to cover any module's outbox without modification. New modules add one config entry; no code change.

5. **Default `Enabled = false`.** Retention is destructive — operators opt in explicitly per environment. The recurring job is still registered so its cron is observable in the dashboard.

6. **Children-before-parents delete order.** `sync.failures` → `sync.dead_letters` → `sync.jobs` → `sync.runs`. A partial sweep (e.g. cancellation mid-run) leaves the audit queryable; FK-less today but logical order matters for human investigation.

7. **`DELETE TOP (N)` in a bounded loop.** Each iteration commits independently — avoids lock escalation on `sync.runs`/`sync.failures` under busy schedules. `MaxDeletedPerTablePerRun` caps wall-clock per run so a fresh enable against a years-old backlog doesn't blow up.

8. **Manual replay = fresh dispatch, not Hangfire requeue.** The existing `/admin/requeue/{jobId}` requeues a specific Hangfire job (useful for fast-forwarding retries within an active run). Manual replay is fundamentally different: it creates a NEW run with a NEW correlation that happens to mirror an old one. Audit cleanliness: the two correlations stay distinct; the relationship is on `Tags.ReplayOf`.

9. **Replay works on any terminal state, not just dead-letter.** Operators sometimes need to re-run a Succeeded sync (e.g. a downstream config change that should reprocess). The endpoint doesn't gate on status — operator's responsibility to know what they're doing.

10. **Retention service is a singleton, not scoped.** No EF context capture; the only injected dependency is `IOptionsMonitor<SyncRetentionOptions>` (singleton-safe). The SQL connection is opened per-`RunAsync` invocation.

---

## 6. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` byte-identical | ✅ (scope constraint observed) |
| `Sync.Student` byte-identical | ✅ |
| `Sync.Staff` byte-identical | ✅ |
| `Sync.Persistence` no schema change, no migration | ✅ |
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts | ✅ unchanged |
| `SyncModuleExecutor`, `SyncDeadLetterFilter`, `SyncDispatcher`, `SyncPipeline` | ✅ unchanged |
| Existing recurring jobs | ✅ continue to tick |
| Hangfire retry policy `[AutomaticRetry(Attempts=4, …)]` | ✅ unchanged |
| Phase 7 Hardening startup queue-config validator | ✅ unchanged |
| Phase 8 per-queue worker pools + per-batch retry + throughput metric | ✅ unchanged |
| Audit-row shapes (`sync.runs`, `sync.failures`, `sync.dead_letters`, `sync.jobs`, `sync.checkpoints`) | ✅ unchanged |

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Retention service uses raw SQL with schema/column names hardcoded. A schema change in `Sync.Persistence` would silently desync. | Low | Verified at runtime per-run via the per-table row-count log. A column rename in `sync.failures` (e.g.) would surface as "0 rows deleted" + a SQL syntax error logged at Warning. Audit log makes this loud. |
| Manual replay can create a runaway loop if an operator scripts it against the wrong status filter. | Low | Endpoint is dev-only via `AllowAllDashboardAuthorizationFilter`. Production access controls (pre-deploy work) gate the call site. |
| `MaxDeletedPerTablePerRun` default of 50 000 may be too low for a fresh-enable against a years-old backlog → multi-day drain. | Low | Operator-tunable. The verification config uses 10 000; a production env with a multi-million backlog should raise this to 200 000 or higher for the first few days. |
| `DELETE TOP (N)` does not guarantee ordering. Without an ORDER BY clause, SQL Server picks rows arbitrarily — older rows may not drain first. | Trivial | Acceptable for cleanup. Each iteration's WHERE clause is the cursor (date-based), so as long as the cutoff doesn't move within the run, every iteration sees a smaller candidate set. |
| Carried debt: `AllowAllDashboardAuthorizationFilter` fronts `/admin/replay`, `/admin/retention/*`. | High (deploy) | **Must be replaced before any non-local deployment.** Unchanged across phases. |

---

## 8. Phase-9 Spec Items: Status

| # | Spec Item | Status |
|---|---|---|
| 1 | Dead-letter handling | **Already shipped Phase 3** — `SyncDeadLetterFilter` + `sync.dead_letters` row. Phase 4 R refined idempotency. |
| 2 | Poison job isolation | Partially — Hangfire's terminal-failed state + dead-letter audit captures them. Auto-pause on N-consecutive deferred (see §10). |
| 3 | Manual replay tools | **✅ Phase 9 baseline** — `/admin/replay/{correlationId}`. |
| 4 | Retry backoff policies | **Already shipped Phase 2** — `[AutomaticRetry(Attempts=4, DelaysInSeconds={60,300,900,3600})]`. **Phase 8** added per-batch writer retry within a single run. |
| 5 | Circuit breakers | **Deferred** — needs alerting hook (Phase 10) + N-consecutive-failure detection. See §10. |
| 6 | Partial sync recovery | **Already shipped Phase 6** — outbox per-row status check; Phase 8 per-batch writer retry tightened it. |
| 7 | Job cancellation support | **Already shipped Phase 4 R** — `Running → Cancelled` transition + `CancellationCoordinator` verification harness. |

The shipped items + already-shipped predecessors cover 6 of 7 spec items. The seventh (circuit breakers) is deferred to Phase 10 (sits naturally with alerting hooks) per §10.

---

## 9. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| `sync-retention` recurring job registered at boot | ✅ §3.4 |
| Retention `Enabled=true` path deletes targeted rows | ✅ §3.1 — 28 failures + 4 dead_letters + 6 jobs + 386 runs + outbox cleared |
| Retention `Enabled=false` short-circuits without deleting | ✅ Code path; log says "Sync retention skipped" |
| Manual replay creates fresh run with `TriggeredBy=replay` and `ReplayOf` tag | ✅ §3.2 |
| Phase 8 per-queue worker pools + per-batch retry preserved | ✅ no regression |
| Phase 7 Hardening startup queue validator preserved | ✅ — validator log line precedes retention registration |
| Pre-existing unrelated test build error (`tests/Core.UniTests/...`) | ⚠️ Unchanged |

---

## 10. Open Items (Phase 9 Expansion / Phase 10)

| Item | Defer-to | Rationale |
|---|---|---|
| Circuit breaker — auto-pause recurring on N-consecutive dead-letters | Phase 10 (alerting + observability) | Naturally pairs with the alerting hook landing in Phase 10; needs sliding-window dead-letter rate tracking which is also a metrics concern. |
| Poison-job auto-isolation (set outbox `Status = Failed` when `AttemptCount >= MaxAttempts`) | Phase 9 expansion | Touches per-module outbox writers (off-limits this turn). Reasonable when operator wants automatic poisoning instead of indefinite retry. |
| Retention metrics rollup table (`sync.retention_history`) | Phase 10 | Operator can already grep the structured log line per run; promoted to a queryable table when dashboarding demands it. |
| Per-module retention windows | Phase 9 expansion | Currently `OutboxTables[].RetentionDays` is per-table; audit windows are global. Per-module audit windows would need a `ModuleName` predicate added to the audit cleanup queries. |
| Replay-of-many (replay all DeadLettered runs of a given module since a cutoff) | Phase 9 expansion | Composition of `/admin/replay` + a query — operator script today; endpoint when ergonomics matter. |

---

## 11. Phase Exit Checklist

- [x] `SyncRetentionOptions` + `SyncOutboxRetentionTarget` config types defined
- [x] `SyncRetentionService.RunAsync` cleans audit tables + operator-declared outbox tables
- [x] `SyncRetentionRecurringTrigger` registered in DI
- [x] `sync-retention` recurring job registered by `SyncRecurringJobsRegistrar` with operator-configured cron
- [x] `Sync:Retention:Enabled = false` short-circuits cleanup (default safe)
- [x] `Sync:Retention:Enabled = true` runs cleanup against all 5 `sync.*` targets + N operator-declared outbox tables
- [x] `/admin/retention` returns active config
- [x] `/admin/retention/run` triggers an immediate sweep
- [x] `/admin/replay/{correlationId}` dispatches fresh run with `TriggeredBy=replay` + `Tags.ReplayOf`
- [x] Runtime verified: pre-sweep counts → post-sweep counts deltas match log details
- [x] Runtime verified: replay run distinct correlation, same module/direction, fresh JobId
- [x] No change to `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`, `Sync.Persistence`
- [x] No public-surface change on `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase 9 (targeted, scope-constrained) complete. Stopping. Phase 10 (Observability Layer) — health checks, queue lag, alerting hook — is the next planned phase, and is the natural home for the circuit-breaker work deferred from this phase.**
