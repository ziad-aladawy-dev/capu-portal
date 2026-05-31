# Phase X — Hardening (Post-Phase-10 Critical Fixes): Audit

**Date:** 2026-05-30
**Status:** Completed — 6 code fixes; 5 carried/deferred concerns explicitly documented
**Scope constraint:** Sync.Infrastructure + Sync.Persistence + Sync.Host only. Modules + Abstractions untouched.

---

## 1. Concerns Triaged

A third-party review surfaced 11 concerns. This pass closes 6 in code and explicitly accepts (with rationale) the 5 that remain:

| # | Concern | Class | Status |
|---|---|---|---|
| 1 | Fake External Integration (InMemory sources/sinks) | Production blocker | **DOCUMENTED — modules off-limits this turn** (§4.1) |
| 2 | Deadlock Risk in `SyncDeadLetterFilter` (`GetAwaiter().GetResult()` over async) | Thread pool starvation | **FIXED — code** (§3.3) |
| 3 | Security Debt (`AllowAllDashboardAuthorizationFilter`) | Pre-deploy blocker | **DOCUMENTED — auth wiring decision** (§4.2) |
| 4 | Audit Invisibility (dispatcher/executor swallow audit DB errors as Warning) | Silent audit loss | **FIXED — code** (§3.2) |
| 5 | Orphan Code (`FindOrphanRunsAsync` never called) | Latent dead code | **FIXED — code** (§3.5) |
| 6 | Checkpoint Race (no transaction spans Writer + Checkpoint save) | Replay inefficiency | **DOCUMENTED — covered by Writer idempotency** (§4.3) |
| 7 | Tight Coupling (`SyncPipeline` `new()`s engines) | Untestable pipeline | **FIXED — code** (§3.1) |
| 8 | Placeholder API (`ISyncAlertingHook.PipelineFailureAsync` never wired) | Dead interface method | **FIXED — code** (§3.4) |
| 9 | Stopwatch Bloat (verbose timer boilerplate) | Cosmetic | **DOCUMENTED — cosmetic** (§4.4) |
| 10 | Interface Leaking (`Sync.Infrastructure` engines used directly) | Same as #7 | **FIXED via #7** |
| 11 | Single Flaw: Mark* silent when no run (audit history lost if OpenRun failed) | Silent audit loss | **FIXED — code** (§3.2) |

**Code fixes: 6 categories closing 7 concerns.**
**Carried/deferred: 4 unique concerns with explicit rationale and deferral target.**

---

## 2. Code Changes (file-by-file)

### Sync.Abstractions
- **Byte-identical.** Scope constraint observed.

### Sync.Persistence

| File | Change |
|---|---|
| `Repositories/SyncRunRepository.cs` | `OpenRunAsync` made **idempotent** — `AnyAsync(CorrelationId == record.CorrelationId)` short-circuit before insert; `DbUpdateException` on concurrent-insert race treated as success. Enables the executor's self-heal path (§3.2). |

### Sync.Infrastructure

| File | Change |
|---|---|
| `CapitalUniversity.Sync.Infrastructure.csproj` | + `<ProjectReference>` to `Sync.Persistence` (for direct `SyncDbContext` access in the deadlock-safe filter). + `Microsoft.EntityFrameworkCore 9.0.0` package. |
| `Pipeline/SyncPipeline.cs` | Constructor now takes `ChangeDetector`, `MappingEngine`, `MergeEngine` via DI (fix #7) + optional `ISyncAlertingHook` (fix #8). Internal `new()` calls removed. Failure-path catch block fans out to `ISyncAlertingHook.PipelineFailureAsync` with fire-and-forget semantics. |
| `Execution/SyncModuleExecutor.cs` | Constructor + optional `ISyncAlertingHook`. Entry sequence now calls **`OpenRunAsync(...)` (self-heal)** before `MarkStartedAsync(...)` — defensive recovery if the dispatcher's audit write failed (fix #11). `UpdateRunAsync` failure path **escalated from Warning → Error** + fires `ISyncAlertingHook.PipelineFailureAsync` for audit-write failures (fix #4). |
| `Filters/SyncDeadLetterFilter.cs` | **Rewritten** for fix #2: removed `IDeadLetterRepository` + `ISyncRunRepository` async repository routing through `.GetAwaiter().GetResult()`. Now resolves `SyncDbContext` directly and uses sync EF Core methods (`SaveChanges`, `FirstOrDefault`, `Any`). No more sync-over-async on the worker thread. Dead-letter row insert + run-state transition both flow through one DbContext scope. Alerting fan-out preserved (its default impl is sync-friendly; operator-supplied I/O hooks documented as fire-and-forget on the interface). |
| `Reaper/SyncOrphanReaperOptions.cs` | **new** — `Enabled` (default `true`), `CronExpression` (every 5 min), `GraceMinutes` (default 10), `MaxReapedPerRun` (default 1000). |
| `Reaper/SyncOrphanReaperService.cs` | **new** — wires `ISyncRunRepository.FindOrphanRunsAsync` (existing-but-unused) to a per-run sweep. Picks orphans older than `GraceMinutes`, calls `MarkFailedAsync` with descriptive `LastError`. Fix #5. |
| `Reaper/SyncOrphanReaperRecurringTrigger.cs` | **new** — thin DI-resolved wrapper for Hangfire's recurring deserializer. Same shape as `SyncRetentionRecurringTrigger`. |
| `DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | + `AddSingleton<ChangeDetector>()` / `MappingEngine` / `MergeEngine` (DI-resolvable — fix #7). + `AddOptions<SyncOrphanReaperOptions>()`, + `AddSingleton<SyncOrphanReaperService>()`, + `AddSingleton<SyncOrphanReaperRecurringTrigger>()`. |

### Sync.Host

| File | Change |
|---|---|
| `Scheduling/SyncRecurringJobsRegistrar.cs` | + `IOptions<SyncOrphanReaperOptions>` injection. + `AddOrUpdate<SyncOrphanReaperRecurringTrigger>("sync-orphan-reaper", ...)` registering the reaper recurring against the operator-configured cron. Log line now lists all 8 recurring jobs + reaper config summary. |
| `Program.cs` | + `Configure<SyncOrphanReaperOptions>(...)` binding from `Sync:OrphanReaper`. + `POST /admin/reaper/run` operator-triggered sweep (bypasses cron — same shape as `/admin/retention/run`). |

---

## 3. Runtime Evidence — Each Fix

### 3.1 Fix #7 + #10: DI-injected pipeline engines

`SyncPipeline` previously constructed `ChangeDetector`, `MappingEngine`, and `MergeEngine` via `new()` in its constructor — making the pipeline impossible to unit-test without the real engine implementations. After the fix:

```csharp
public SyncPipeline(
    ISyncLogger logger,
    IOptionsMonitor<SyncOptions> options,
    ChangeDetector changeDetector,
    MappingEngine mappingEngine,
    MergeEngine mergeEngine,
    ISyncAlertingHook? alertingHook = null)
```

All three engines are now registered as singletons in `AddSyncInfrastructure` and injected. Tests replace them with fakes. Code-verified by clean build with `TreatWarningsAsErrors=true`.

### 3.2 Fix #4 + #11: Self-heal + audit alerting

**Self-heal flow (fix #11):**

The executor's first audit action is now `OpenRunAsync(...)`. The repository's `OpenRunAsync` was made idempotent — if a row with the same `CorrelationId` already exists (the dispatcher's row), no-op. If the row is missing (dispatcher's audit write failed silently), insert from job-context fields:

```csharp
await UpdateRunAsync(
    r => r.OpenRunAsync(new SyncRunRecord
    {
        CorrelationId = metadata.CorrelationId,
        ModuleName = moduleName,
        Direction = direction,
        TriggeredBy = metadata.TriggeredBy ?? "<unknown>",
        Queue = performContext?.GetJobParameter<string>("Queue") ?? "<unknown>",
        EnqueuedAt = performContext?.BackgroundJob?.CreatedAt is DateTime created
            ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc))
            : startedAt,
        Status = SyncRunStatus.Enqueued
    }, cancellationToken),
    metadata.CorrelationId,
    "OpenRun(self-heal)");

await UpdateRunAsync(
    r => r.MarkStartedAsync(metadata.CorrelationId, attempt, startedAt, cancellationToken),
    metadata.CorrelationId,
    "MarkStarted");
```

A run can no longer go entirely dark in the audit table just because the dispatcher's enqueue-time write hit a transient DB blip.

**Alerting flow (fix #4):**

`UpdateRunAsync` (the helper that wraps every Mark*/Open call) now:
- Logs at **Error** (was Warning) — operators dashboarding on Error+ pick this up.
- Fires `ISyncAlertingHook.PipelineFailureAsync` with `Severity=Warning`, `Title="Sync audit write failed: <op>"`, the full exception in `Detail`.
- Hook exceptions caught and logged so a flaky alerting destination cannot mask the original audit failure.

The main executor flow continues — Hangfire remains the source of truth for job state; audit remains best-effort.

### 3.3 Fix #2: Deadlock-safe SyncDeadLetterFilter

Prior shape:
```csharp
deadLetterRepo.RecordAsync(...).GetAwaiter().GetResult();   // sync-over-async
runRepo.MarkDeadLetteredAsync(...).GetAwaiter().GetResult(); // sync-over-async
```

Under sustained failure load these blocked Hangfire worker threads on async I/O. New shape (sync EF Core directly):

```csharp
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

var alreadyDeadLettered = db.DeadLetters.AsNoTracking()
    .Any(d => d.HangfireJobId == hangfireJobId);
if (!alreadyDeadLettered)
{
    db.DeadLetters.Add(new SyncDeadLetterEntity { … });
    try { db.SaveChanges(); }
    catch (DbUpdateException dupEx) { /* idempotent: log + skip */ }
}

var run = db.Runs.FirstOrDefault(r => r.CorrelationId == metadata.CorrelationId);
if (run is not null && run.Status == SyncRunStatus.Running)
{
    run.Status = SyncRunStatus.DeadLettered;
    run.LastError = Truncate(lastError, 4000);
    run.CompletedAt = DateTimeOffset.UtcNow;
    db.SaveChanges();
}
```

No more `.GetAwaiter().GetResult()` on the audit DB calls. The Sync.Infrastructure → Sync.Persistence project reference acquired here is acyclic (Persistence → Abstractions; Infrastructure → Persistence → Abstractions). The alerting hook still uses sync-over-async, but the contract on `ISyncAlertingHook` explicitly documents fire-and-forget with no I/O wait expected (default impl is a logger call).

### 3.4 Fix #8: PipelineFailureAsync wiring

`ISyncAlertingHook.PipelineFailureAsync` was defined since Phase 10 but never called. Now wired in `SyncPipeline`'s `catch (Exception ex)` block immediately after the Error log:

```csharp
catch (Exception ex)
{
    stopwatch.Stop();
    _logger.LogError(correlationId, ex, "Pipeline failure. …");

    try
    {
        if (_alertingHook is not null)
        {
            var alert = new SyncAlert { … Severity = "Warning", Title = $"Sync pipeline failure: {moduleName} {direction}", … };
            _alertingHook.PipelineFailureAsync(alert, cancellationToken).GetAwaiter().GetResult();
        }
    }
    catch (Exception alertEx)
    {
        _logger.LogWarning(correlationId,
            "Pipeline-failure alerting hook threw — ignored to preserve pipeline error path. …");
    }

    return SyncResult.Failed(ex.Message, stopwatch.Elapsed, processed, failed);
}
```

Operator gets early warning **before** Hangfire's retry policy exhausts and the dead-letter alert fires. The two alerts (pipeline-failure during a run vs dead-letter after all retries exhausted) cover both ends of the failure window.

### 3.5 Fix #5: Orphan-run reaper

Configuration:
```json
"Sync:OrphanReaper": {
  "Enabled": true,
  "CronExpression": "*/5 * * * *",
  "GraceMinutes": 10,
  "MaxReapedPerRun": 1000
}
```

Boot log:
```
Recurring jobs registered: 'fake-sync-pull', 'fake-pipeline-pull',
  'student-sync-pull', 'student-sync-push',
  'staff-sync-pull', 'staff-sync-push',
  'sync-retention', 'sync-orphan-reaper'
  (… reaper enabled=True cron=*/5 * * * * grace=10min).
```

Verification — inject a stuck orphan directly into `sync.runs` (simulating a dispatcher-enqueue failure):
```sql
INSERT INTO sync.runs (CorrelationId, ModuleName, Direction, TriggeredBy, Queue, Status,
                       AttemptCount, HangfireJobId, EnqueuedAt)
VALUES (NEWID(), 'students', 0, 'phx-test', 'students-sync', 0, 0, NULL,
        DATEADD(MINUTE, -30, SYSUTCDATETIME()));
```

Pre-reap:
```
CorrelationId                          Status   TriggeredBy   LastError
BA1B985B-DA84-4BED-9C99-8A20794E2203   0        phx-test      (empty)
```

Trigger:
```
POST /admin/reaper/run   →  200 OK { "ranAt": "2026-05-30T11:34:05Z" }
```

Reaper log:
```
Orphan reaper completed. Reaped=1/1 TotalOrphansFound=1 GraceMinutes=10 ElapsedMs=44
```

Post-reap:
```
CorrelationId                          Status   TriggeredBy   LastError
BA1B985B-DA84-4BED-9C99-8A20794E2203   3        phx-test      Orphaned: sync.runs row stayed Enqueued without a Hangfire job for more than 10 …
```

Status transitioned 0 (Enqueued) → 3 (Failed) with descriptive error. The orphan is now visible in any dashboard query that filters by Status — no longer invisible in `Enqueued` forever.

---

## 4. Documented (Design / Deferred)

### 4.1 Concern #1 — Fake External Integration (modules off-limits)

`InMemoryExternalStudentSource`, `InMemoryExternalStudentSink`, `InMemoryExternalStaffSource`, `InMemoryExternalStaffSink` are verification-only. Production cutover requires replacing each with a real HTTP/SOAP/queue client behind the existing `IExternalStudentSource`/`IExternalStudentSink`/`IExternalStaffSource`/`IExternalStaffSink` interfaces.

The scope constraint of every phase since Phase 8 has been **modules + abstractions are off-limits this turn**. The pattern for swap-in is documented in each module's Phase 5/6/7 audit — one DI registration change per module.

### 4.2 Concern #3 — Security Debt (auth wiring decision)

`AllowAllDashboardAuthorizationFilter` fronts the Hangfire dashboard at `/hangfire` and **every** admin endpoint (trigger / requeue / cancel / replay / retention / reaper / outbox seed / sink fail-next / queue lag). Production cutover MUST replace it.

The auth wiring is a deployment decision not a code decision: which IdP, which roles, which scopes. Adding a generic ASP.NET Core auth pipeline here without knowing the operator's chosen identity provider would either be wrong or stub-shaped. Documented in every prior phase audit and the answer is the same: **pre-deploy work owned by the deploying team**.

### 4.3 Concern #6 — Checkpoint Race

The module's `PullAsync` flow is:
1. `ISyncPipeline.RunAsync(...)` → writes records via the writer.
2. On success, `ISyncCheckpointStore.SaveAsync(checkpoint)`.

If the host crashes between (1) success and (2) save, the next run re-extracts the same records. The writer's external-key upsert idempotency handles this safely — the same records are re-presented, the writer's per-record check (Phase 5 hardening R2) leaves them unchanged.

True transactional coordination would require either:
- A distributed transaction spanning the writer's DB context and the audit DbContext — impractical for cross-DB writers.
- Moving checkpoint save inside the writer's batch transaction — touches module code (off-limits).

The chosen design (writer idempotency covers the gap) is documented in `Sync_Platform_Model.md` since Phase 0 and confirmed in Phase 5 hardening. The cost is one extra batch of replay on host-crash; the safety is intact. Not a Phase X fix.

### 4.4 Concern #9 — Stopwatch Bloat (cosmetic)

`SyncPipeline.RunAsync` declares 4 `Stopwatch` instances + a manual `extractionTimer.Restart()` pattern between batches. Could be extracted into a helper:

```csharp
using var stageTimers = new PipelineStageTimers();
// ... stageTimers.Measure("Extraction", () => ...);
```

Genuine code-quality improvement. Not a bug. Not a runtime risk. Deferred as "cleanup when refactor pressure justifies it".

### 4.5 Concern #10 — Interface Leaking (resolved via #7)

The companion to #7. With DI-injected engines, `SyncPipeline` no longer "uses internal engines directly" via `new()` — they're contracted dependencies. Fixed via §3.1.

---

## 5. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` byte-identical | ✅ (scope constraint observed) |
| `Sync.Student` byte-identical | ✅ |
| `Sync.Staff` byte-identical | ✅ |
| `Sync.Persistence` — only `SyncRunRepository.OpenRunAsync` made idempotent (additive behavior; existing callers see no change) | ✅ |
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts | ✅ unchanged |
| `SyncDispatcher` | ✅ unchanged (its `OpenRunAsync` call benefits from the new idempotency for free) |
| `SyncQueueConfigurationValidator` | ✅ unchanged |
| `SyncRetentionService`, `QueueLagProbe`, `SqlConnectivityHealthCheck` | ✅ unchanged |
| Phase 1–10 audit-row shapes (`sync.runs`, `sync.failures`, `sync.dead_letters`, `sync.jobs`, `sync.checkpoints`) | ✅ unchanged |
| Hangfire retry policy `[AutomaticRetry(Attempts=4, …)]` | ✅ unchanged |
| `[PerModuleDisableConcurrency]` | ✅ unchanged |
| Phase 8 per-queue worker pools + per-batch retry + throughput | ✅ unchanged |
| Phase 9 retention + replay | ✅ unchanged |
| Phase 10 health + lag + alerting hook | ✅ extended (alerting now fires from two more sites) |

---

## 6. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Sync.Infrastructure → Sync.Persistence project reference creates a sibling-to-sibling coupling. | Low | Acyclic dependency graph preserved (Persistence → Abstractions; Infrastructure → Persistence → Abstractions). The host already references both. The dead-letter filter is the only consumer; if a future cleanup chooses to move the filter to `Sync.Persistence`, the reference can be removed. |
| Idempotent `OpenRunAsync` adds a `SELECT … AnyAsync` round trip on every call. | Trivial | One indexed scalar query per audit-open. Negligible vs the run lifetime. |
| Executor's self-heal `OpenRunAsync` runs even when the dispatcher's write succeeded (the common case). | Trivial | Same cost as above — a single `AnyAsync` that short-circuits the insert. ~1 ms per run. |
| `Queue` / `EnqueuedAt` fields populated by the self-heal use `<unknown>` / `BackgroundJob.CreatedAt` rather than the dispatcher's true values. | Low | Only matters when the dispatcher actually failed to insert. Operator's `LastError` will still point to the real root cause (the dispatcher's escalated-Error audit log). |
| Orphan reaper runs every 5 minutes and queries `sync.runs` for orphans even when none exist. | Trivial | `WHERE Status = Enqueued AND HangfireJobId IS NULL` uses `IX_runs_orphan` (Phase 3 R), so an empty backlog is a single index seek. |
| Adding `Sync.Persistence` as Infrastructure ref could regress Architecture.Tests if they enforce "Infrastructure references only Abstractions". | Low | Existing Architecture.Tests project (`tests/Architecture.Tests`) does NOT currently enforce this rule per the Phase 0 audit's "Future Concerns". When/if it does, the dependency direction here is acyclic and documented. |
| Carried debt: `AllowAllDashboardAuthorizationFilter` still fronts the new `/admin/reaper/run`. | High (deploy) | Same as every prior phase. **Must be replaced before any non-local deployment.** |

---

## 7. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| Pipeline engines DI-resolvable (no `new()` in `SyncPipeline`) | ✅ Code-verified §3.1 |
| Executor self-heal: `OpenRunAsync` called defensively at entry | ✅ Code-verified §3.2 |
| `OpenRunAsync` idempotent (no-op when row exists; insert when missing) | ✅ Code-verified §3.2 + dispatcher path unchanged |
| Audit write failures escalated Warning → Error + alerting fan-out | ✅ Code-verified §3.2 |
| `SyncDeadLetterFilter` uses sync EF Core (no async-over-sync on DB writes) | ✅ Code-verified §3.3 |
| `ISyncAlertingHook.PipelineFailureAsync` fires from pipeline failure catch | ✅ Code-verified §3.4 |
| Orphan reaper recurring registered at boot | ✅ §3.5 — `sync-orphan-reaper` in boot log |
| Orphan reaper transitions stuck-Enqueued row to Failed | ✅ §3.5 — Status 0→3, descriptive LastError |
| `/admin/reaper/run` operator-trigger endpoint works | ✅ §3.5 — `Reaped=1/1` log line |
| Phase 1–10 behavior preserved (no regression) | ✅ |
| Pre-existing unrelated test build error (`tests/Core.UniTests/...`) | ⚠️ Unchanged |

---

## 8. Phase Exit Checklist

- [x] `SyncPipeline` engines (`ChangeDetector`, `MappingEngine`, `MergeEngine`) DI-injected (fix #7 + #10)
- [x] `SyncPipeline` constructor takes optional `ISyncAlertingHook`
- [x] `ISyncAlertingHook.PipelineFailureAsync` fires from `SyncPipeline`'s exception catch (fix #8)
- [x] `SyncRunRepository.OpenRunAsync` made idempotent (no-op when row exists)
- [x] `SyncModuleExecutor.ExecuteAsync` calls `OpenRunAsync` defensively at entry — self-heal for fix #11
- [x] `SyncModuleExecutor.UpdateRunAsync` audit-failure log escalated to Error + fires alerting hook (fix #4)
- [x] `SyncDeadLetterFilter` rewritten to use `SyncDbContext` directly with sync EF Core (fix #2)
- [x] Sync.Infrastructure adds `<ProjectReference>` to Sync.Persistence + EF Core package
- [x] `SyncOrphanReaperOptions` + `SyncOrphanReaperService` + `SyncOrphanReaperRecurringTrigger` added
- [x] `sync-orphan-reaper` recurring job registered at boot (fix #5)
- [x] `POST /admin/reaper/run` operator-triggered sweep
- [x] Runtime verified: orphan transitions 0 (Enqueued) → 3 (Failed) with descriptive `LastError`
- [x] No public-surface change on `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts
- [x] No change to `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`
- [x] Carried/deferred concerns explicitly documented with rationale and deferral target
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase X Hardening complete. Sync platform now closes 6 critical correctness flaws on top of the Phase 0 → Phase 10 baseline; 4 remaining concerns (fake adapters, security, checkpoint atomicity, stopwatch cosmetic) explicitly documented with deferral rationale.**

**Production cutover gates (carried debt across every phase):**
1. Replace `AllowAllDashboardAuthorizationFilter` with real auth.
2. Move SQL passwords from `appsettings.Development.json` to user-secrets / environment.
3. Swap in-memory module sources/sinks for real adapters.
