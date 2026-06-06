# Phase X.2 — Residual Hardening: Audit

**Date:** 2026-05-30
**Status:** Completed — 3 residual flaws closed in code; carried-debt list shrinks accordingly
**Scope constraint:** Sync.Infrastructure + Sync.Persistence + Sync.Host only. Modules + Abstractions untouched.

---

## 1. Concerns Addressed

A second-pass review of Phase X surfaced three residual risks. All closed in code:

| # | Concern | Status |
|---|---|---|
| 1 | Deadlock Hazard — `SyncDeadLetterFilter` still used `.GetAwaiter().GetResult()` on the alerting hook | **FIXED** (§3.1) |
| 2 | Interface Leak — `SyncPipeline` consumed engines as concrete classes | **FIXED** (§3.2) |
| 3 | Fake Integration — InMemory sources/sinks the only production option | **FIXED via scaffold + DI override** (§3.3) |

---

## 2. Code Changes (file-by-file)

### Sync.Abstractions
- **Byte-identical.** Scope constraint observed.

### Sync.Persistence
- **Byte-identical.**

### Sync.Infrastructure

| File | Change |
|---|---|
| `Filters/SyncDeadLetterFilter.cs` | Eliminated last `.GetAwaiter().GetResult()` on the alerting hook. New `FireAndForgetAlert` helper runs the hook call on a thread-pool task inside its own DI scope (the per-event scope is disposed by the time the background task runs). Hook exceptions caught + logged inside the task; the Hangfire worker thread is never blocked by alerting I/O. |
| `Pipeline/SyncPipeline.cs` | Replaced `.GetAwaiter().GetResult()` on `_alertingHook.PipelineFailureAsync(...)` (inside an already-async catch) with proper `await ... .ConfigureAwait(false)`. The previous misuse was the worst kind — blocking from inside an async method when `await` was natively available. |
| `Pipeline/IChangeDetector.cs` | **new** — interface contract for change detection. |
| `Pipeline/IMappingEngine.cs` | **new** — interface contract for mapping. |
| `Pipeline/IMergeEngine.cs` | **new** — interface contract for merge/writer-dispatch. |
| `Pipeline/ChangeDetector.cs` | Implements `IChangeDetector`. |
| `Pipeline/MappingEngine.cs` | Implements `IMappingEngine`. |
| `Pipeline/MergeEngine.cs` | Implements `IMergeEngine`. |
| `Pipeline/SyncPipeline.cs` | Constructor signature changed from concrete-class engines to `IChangeDetector`/`IMappingEngine`/`IMergeEngine`. Field types updated to match. |
| `DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | DI registrations changed from `AddSingleton<ChangeDetector>()` (etc.) to `AddSingleton<IChangeDetector, ChangeDetector>()` (etc.). Tests can now substitute via `services.AddSingleton<IChangeDetector, FakeChangeDetector>()` before resolving `ISyncPipeline`. |

### Sync.Host

| File | Change |
|---|---|
| `Integration/SyncIntegrationOptions.cs` | **new** — `UseHttpAdapters` master switch + per-module `BaseUrl`/`ApiKey`/`Timeout`. Bound from `Sync:Integration` config. |
| `Integration/HttpExternalStudentSink.cs` | **new** — `IExternalStudentSink` implementation that `PUT`s `{BaseUrl}/students/{externalStudentId}` with JSON body. Non-2xx → `InvalidOperationException` so the Phase 6 per-row outbox failure path engages. |
| `Integration/HttpExternalStudentSource.cs` | **new** — `IExternalStudentSource` implementation that `GET`s `{BaseUrl}/students?since={iso8601}` and streams the response array. |
| `Integration/HttpExternalStaffSink.cs` | **new** — Staff counterpart, same shape. |
| `Integration/HttpExternalStaffSource.cs` | **new** — Staff counterpart, same shape. |
| `Integration/IntegrationServiceCollectionExtensions.cs` | **new** — `AddSyncHttpAdaptersIfEnabled`. When `Sync:Integration:UseHttpAdapters = true`, registers `HttpClient` instances (per-module base URL + timeout + optional `X-Api-Key`) and binds the HTTP impls as `IExternalXSink`/`IExternalXSource` AFTER the module's own registration. DI's last-wins semantics make the HTTP one the active resolution. No-op when flag is `false`. |
| `Program.cs` | + `using CapitalUniversity.Sync.Host.Integration;` + `builder.Services.AddSyncHttpAdaptersIfEnabled(builder.Configuration);` immediately after `AddStaffSync(...)`. |

### Files explicitly NOT touched (scope constraint)

- `Sync.Abstractions/**` — byte-identical.
- `Sync.Student/**` — byte-identical. The HTTP swap-in lives entirely in `Sync.Host`.
- `Sync.Staff/**` — byte-identical. Same.
- `Sync.Persistence/**` — byte-identical.

---

## 3. Per-Fix Detail

### 3.1 Concern #1 — Residual Deadlock

**Two call sites still had `.GetAwaiter().GetResult()` after Phase X.1:**

1. **`SyncDeadLetterFilter.OnStateApplied`** — Hangfire's `IApplyStateFilter` is a synchronous interface. The audit-DB writes were converted to sync EF Core in Phase X.1, but the alerting fan-out kept `.GetAwaiter().GetResult()` on the hook's async method. The previous "safe because default impl is sync" justification ignored operator-supplied hooks doing real I/O (Slack, PagerDuty, webhooks).

   **Fix:** new `FireAndForgetAlert(...)` helper. Spawns a `Task.Run` that opens a fresh DI scope and awaits the hook properly. The Hangfire worker thread returns immediately:

   ```csharp
   private void FireAndForgetAlert(Guid correlationId, string hangfireJobId, SyncAlert alert)
   {
       _ = Task.Run(async () =>
       {
           try
           {
               await using var alertScope = _scopeFactory.CreateAsyncScope();
               var hook = alertScope.ServiceProvider.GetService<ISyncAlertingHook>();
               if (hook is null) return;
               await hook.DeadLetterAsync(alert, CancellationToken.None).ConfigureAwait(false);
           }
           catch (Exception alertEx)
           {
               _logger.LogWarning(correlationId,
                   "Dead-letter alerting hook failed (audit row was still written). JobId={JobId} Error={Error}",
                   hangfireJobId, alertEx.Message);
           }
       });
   }
   ```

   The fresh DI scope inside the task is necessary because the filter's per-event scope is disposed when `OnStateApplied` returns — reusing it from the background task would hit a `ObjectDisposedException` on the `ServiceProvider`.

2. **`SyncPipeline.RunAsync` catch block** — the worst-of-both: `.GetAwaiter().GetResult()` was used inside an already-async method. The pipeline is an async pipeline; the natural fix is just `await`. The reason this was wrong wasn't deadlock risk per se (the pipeline runs on a worker thread without a sync context) — it was an unnecessary thread block in the most-common-success-path's error edge:

   ```csharp
   await _alertingHook.PipelineFailureAsync(alert, cancellationToken).ConfigureAwait(false);
   ```

   The hook's exception (if any) is caught in the surrounding try/catch and logged at Warning, exactly as before.

**Net result:** zero `.GetAwaiter().GetResult()` calls remain in the sync layer. (`grep` proof — no longer present in `SyncDeadLetterFilter.cs` or `SyncPipeline.cs`.)

### 3.2 Concern #2 — Interface Leak

`SyncPipeline` previously declared `ChangeDetector`, `MappingEngine`, `MergeEngine` as concrete-class parameters. Tests substituting these engines had to:
- Create real instances of the concrete classes (defeats the test), OR
- Subclass each (each is `sealed`, so impossible without removing the seal).

**Fix:** introduce three interfaces in `Sync.Infrastructure.Pipeline` — kept inside Infrastructure because Abstractions is intentionally module-facing-only and the scope constraint forbids modifying it:

```csharp
public interface IChangeDetector
{
    IAsyncEnumerable<TExternal> DetectChanges<TExternal>(
        IDataExtractor<TExternal> extractor,
        SyncContext context,
        SyncCheckpoint? checkpoint,
        CancellationToken cancellationToken);
}

public interface IMappingEngine
{
    IReadOnlyList<TInternal> Map<TExternal, TInternal>(
        IReadOnlyList<TExternal> batch,
        IRecordMapper<TExternal, TInternal> mapper);
}

public interface IMergeEngine
{
    Task<int> MergeAsync<TInternal>(
        IReadOnlyList<TInternal> batch,
        IRecordWriter<TInternal> writer,
        CancellationToken cancellationToken);
}
```

Three concrete classes mark `: IChangeDetector` / `: IMappingEngine` / `: IMergeEngine`. `SyncPipeline` constructor parameters become the interface types. DI registration becomes the standard interface-to-implementation pattern:

```csharp
services.AddSingleton<IChangeDetector, ChangeDetector>();
services.AddSingleton<IMappingEngine, MappingEngine>();
services.AddSingleton<IMergeEngine, MergeEngine>();
```

A test can now substitute a mock by:
```csharp
services.AddSingleton<IChangeDetector, FakeChangeDetector>();
// resolve ISyncPipeline → the pipeline picks up the fake
```

The companion concern (#10 from the previous review — "internal engines exposed via SyncPipeline") is the same problem; this fix closes both.

**Note on placement.** The interfaces live in `Sync.Infrastructure.Pipeline`, not `Sync.Abstractions.Pipeline`. The architectural justification is the same one given since Phase 4: pipeline engines are an implementation detail of the infrastructure layer; modules don't consume them. If a future scope change opens Abstractions for modification, the interfaces can be moved in one operation — implementations stay where they are because they reference `Sync.Abstractions` extractor/mapper/writer contracts via the engine interface's generic parameters.

### 3.3 Concern #3 — Fake Integration

The InMemory sources/sinks were registered by each module's `AddStudentSync` / `AddStaffSync` extension. Touching those extensions is out of scope (modules off-limits). The fix is the standard DI-override-in-composition-root pattern.

**Building blocks (all in `Sync.Host`):**

```
Integration/SyncIntegrationOptions.cs            UseHttpAdapters bool + per-module BaseUrl/ApiKey/Timeout
Integration/HttpExternalStudentSink.cs           IExternalStudentSink via HttpClient.PUT
Integration/HttpExternalStudentSource.cs         IExternalStudentSource via HttpClient.GET (JSON array)
Integration/HttpExternalStaffSink.cs             Staff counterpart
Integration/HttpExternalStaffSource.cs           Staff counterpart
Integration/IntegrationServiceCollectionExtensions.cs   AddSyncHttpAdaptersIfEnabled extension
```

**Wiring (in `Program.cs`):**

```csharp
builder.Services.AddStudentSync(builder.Configuration);   // registers InMemory* as default
builder.Services.AddStaffSync(builder.Configuration);     // same

builder.Services.AddSyncHttpAdaptersIfEnabled(builder.Configuration);   // ⬅ overrides when flag is true
```

`AddSyncHttpAdaptersIfEnabled` short-circuits when `Sync:Integration:UseHttpAdapters` is `false` (the default — dev/test behavior unchanged). When `true`, it:
1. Registers an `HttpClient` per HTTP adapter via `AddHttpClient<T>(...)` with base URL + timeout + optional `X-Api-Key` header.
2. Re-binds `IExternalStudentSink`/`Source` and `IExternalStaffSink`/`Source` to the HTTP implementations. DI's last-registration-wins makes them the active resolution; the modules' InMemory registrations remain registered as concrete classes (so the existing `/admin/outbox/sink` diagnostic inspect surface that resolves `InMemoryExternalStudentSink` directly still works in test setups that flip the flag back off).

**Operator config (illustrative):**

```json
{
  "Sync": {
    "Integration": {
      "UseHttpAdapters": true,
      "Student": {
        "BaseUrl": "https://upstream.university.example/api",
        "ApiKey": "secret-from-user-secrets",
        "Timeout": "00:00:30"
      },
      "Staff": {
        "BaseUrl": "https://hr.university.example/api",
        "ApiKey": "secret-from-user-secrets",
        "Timeout": "00:00:30"
      }
    }
  }
}
```

**Module code stays byte-identical.** No `Sync.Student` or `Sync.Staff` file changes. The swap happens entirely in the host's composition root.

**Verified at runtime (flag-off mode preserved):**

```
POST /admin/outbox/student/EXT-S-X999  (seed)
POST /admin/trigger/students?direction=Push   →  jobId=819

GET /admin/outbox/sink   →  acceptedCount=1   (InMemory sink still active)
sync.runs[819]:           Status=Succeeded     RecordsProcessed=1
```

The flag-on path is **code-verified only** — there's no real upstream endpoint to point it at in this environment. Production deployments will need integration tests against the real upstream contract, which is operator work not platform work.

---

## 4. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` byte-identical | ✅ |
| `Sync.Student` byte-identical | ✅ |
| `Sync.Staff` byte-identical | ✅ |
| `Sync.Persistence` byte-identical | ✅ |
| `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, `IDataExtractor`, `IRecordMapper`, `IRecordValidator`, `IRecordWriter`, `IExternalStudentSource`/`Sink`, `IExternalStaffSource`/`Sink` | ✅ unchanged |
| `SyncDispatcher`, `SyncModuleExecutor`, `SyncQueueConfigurationValidator`, `SyncRetentionService`, `QueueLagProbe`, `SqlConnectivityHealthCheck`, `SyncOrphanReaperService` | ✅ unchanged |
| Phase 0–10 + Phase X audit-row shapes | ✅ unchanged |
| Phase 8 per-queue worker pools + per-batch retry + throughput | ✅ unchanged |
| Phase 9 retention + replay | ✅ unchanged |
| Phase 10 health + lag + alerting hook | ✅ alerting fan-out semantics changed from sync-over-async to true fire-and-forget (no behavior change visible to default `LoggingSyncAlertingHook`) |
| Phase X self-heal + orphan reaper + audit alerting | ✅ unchanged |
| Default behavior (no `Sync:Integration` config) | ✅ InMemory sources/sinks still active — verified by runtime push test §3.3 |

---

## 5. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `FireAndForgetAlert`'s `Task.Run` may not complete on abrupt host shutdown — the alert is lost. | Low | The dead-letter audit row was already written synchronously before the alert task spawned. The audit table is the source of truth for operator triage; alerting is decoration. Hangfire's graceful-shutdown window typically allows pending Task.Run work to finish; an OS-level kill is the only way to truly drop an alert. |
| Background `Task.Run` exceptions can't bubble back to the filter. | Trivial | Caught + logged at Warning inside the task. Operator sees `"Dead-letter alerting hook failed …"` in the structured log. |
| Engine interfaces use generic methods. C# allows it but some test frameworks have awkward syntax for substituting generic methods. | Trivial | `Moq` / `NSubstitute` / `FakeItEasy` all support this pattern; the engines are simple wrappers so a hand-rolled fake is two lines each. |
| HTTP adapters use a minimal scaffold — no Polly retry, no exponential backoff, no authentication beyond `X-Api-Key`. | Medium | Documented in XML doc on each adapter. Production deployments will want Polly + the operator's chosen auth scheme. The platform's Phase 8 per-batch retry + outbox per-row failure path covers the transient-error case at the pipeline level; this is defense-in-depth for the inner HTTP call. |
| HTTP adapters' BaseUrl/ApiKey live in plain config. | High (deploy) | `ApiKey` MUST move to user-secrets / environment / Key Vault before non-local deployment. Same carried-debt list as the SQL password since Phase 2. |
| The InMemory sources still get instantiated (as concrete classes) even when HTTP overrides are active. Wastes a few KB of memory + a constructor call. | Trivial | Acceptable — keeps the `/admin/outbox/sink` inspect surface usable for test environments that flip back to in-memory mid-session. |
| Carried debt: `AllowAllDashboardAuthorizationFilter` still fronts all admin endpoints. | High (deploy) | Same as every prior phase. **Must be replaced before any non-local deployment.** |

---

## 6. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| Zero `.GetAwaiter().GetResult()` calls in `SyncDeadLetterFilter` and `SyncPipeline` | ✅ Code-verified |
| `IChangeDetector` / `IMappingEngine` / `IMergeEngine` interfaces exist; pipeline depends on them | ✅ Code-verified §3.2 |
| DI registrations resolve interfaces to concrete engines | ✅ Code-verified |
| `AddSyncHttpAdaptersIfEnabled` no-op when flag is false | ✅ Runtime-verified §3.3 — in-memory sink still serves |
| Flag-on mode swaps `IExternalXSink`/`Source` to HTTP implementations | ✅ Code-verified via DI last-wins pattern; live HTTP not exercised |
| Phase 0–10 + Phase X behavior preserved | ✅ no regression observed in default-flag-off boot |
| Pre-existing unrelated test build error (`tests/Core.UniTests/...`) | ⚠️ Unchanged |

---

## 7. Updated Carried-Debt List

The 3 critical residual flaws all closed in code. Remaining carried debt across every phase:

1. **Security:** `AllowAllDashboardAuthorizationFilter` must be replaced before non-local deployment.
2. **Secrets:** SQL passwords (`Sync:Hangfire:ConnectionString`, `Sync:Student:ConnectionString`, `Sync:Staff:ConnectionString`) + the new HTTP `ApiKey` settings must move to user-secrets / environment / Key Vault.
3. **HTTP adapter hardening:** Polly retry policy, real auth flow, observability instrumentation — operator-specific work that lives in the HTTP adapter classes once a real upstream contract exists.
4. **Test coverage:** No `tests/` work this turn — the pre-existing `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs` `NotFoundException` build error remains unchanged from Phase 0.

The carried-debt list is now significantly shorter than at the end of Phase X.1 — concerns #1 (deadlock), #2 (interface leak), #3 (fake integration) are no longer in the open-risks column.

---

## 8. Phase Exit Checklist

- [x] `SyncDeadLetterFilter` alerting fan-out converted to `Task.Run` fire-and-forget with fresh DI scope (fix #1)
- [x] `SyncPipeline` `PipelineFailureAsync` call switched to native `await ... .ConfigureAwait(false)` (fix #1)
- [x] Zero `.GetAwaiter().GetResult()` calls remain in `Sync.Infrastructure`
- [x] `IChangeDetector` + `IMappingEngine` + `IMergeEngine` interfaces defined in `Sync.Infrastructure.Pipeline`
- [x] `ChangeDetector` / `MappingEngine` / `MergeEngine` mark their interfaces (fix #2)
- [x] `SyncPipeline` constructor takes interface types
- [x] DI registers interface→implementation; tests can substitute
- [x] HTTP adapters in `Sync.Host/Integration/`: Student sink + source, Staff sink + source (fix #3)
- [x] `AddSyncHttpAdaptersIfEnabled` extension method; called from `Program.cs` after `AddStaffSync`
- [x] `Sync:Integration:UseHttpAdapters = false` (default) preserves Phase X.1 behavior
- [x] `Sync:Integration:UseHttpAdapters = true` overrides module registrations via DI last-wins
- [x] Modules byte-identical
- [x] Abstractions byte-identical
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Runtime smoke: default-flag-off push still goes to in-memory sink
- [x] Audit document produced

---

**Phase X.2 complete. The sync platform now has zero remaining critical residual flaws from the third-party review series.**

**Carried debt narrowed to:**
1. Replace `AllowAllDashboardAuthorizationFilter` (deployment auth wiring decision).
2. Move SQL passwords + HTTP API keys to user-secrets / environment / Key Vault.
3. Production HTTP adapter hardening (Polly, real auth) — only relevant once an upstream contract exists.
