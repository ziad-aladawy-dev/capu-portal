# Phase 1 — Hangfire Host Skeleton (Refined): Architecture Notes

**Date:** 2026-05-28
**Status:** Completed (runtime-validated, refinement pass applied)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`
**Refinement scope:** Cleanup-only. Deletions preferred over abstractions. No new components. No new systems. All public interfaces preserved.

---

## 1. Identity Hierarchy (binding)

There are exactly three identities in the sync runtime. They are not interchangeable, and only one is generated per layer.

| Identity | Owner | Purpose | Lifetime |
|---|---|---|---|
| **CorrelationId** | Us (created at trigger boundary) | System trace identity. Propagated through Hangfire serialization, MEL log scope, and all downstream logging. | One per sync run |
| **RunId** | Conceptual — equals `CorrelationId` in Phase 1 | A "run" is identified by its `CorrelationId`. **No separate `RunId` field exists.** | n/a |
| **JobId** | Hangfire | Transport identifier for the enqueued background job. Visible in the Hangfire dashboard. Logged once at dispatch time so operators can pivot between trace logs and dashboard. | One per Hangfire job |

Rationale: Phase 1 establishes a 1:1 relationship between a triggered run and a Hangfire job. A separate `RunId` would have been a duplicate of `CorrelationId` with no behavioral difference. If a future phase ever needs to attach multiple jobs to a single business run (e.g., multi-batch pipelines in Phase 4), `RunId` can be re-introduced **without** changing `CorrelationId` semantics.

---

## 2. Ownership Boundaries (binding)

Stated explicitly so the boundaries cannot drift:

| Layer | Owns | Does **not** own |
|---|---|---|
| **Hangfire** | Execution lifecycle: storage, queueing, retry, scheduling, state transitions, dashboard, cancellation token. | Business logic. Business identity. Business audit. |
| **`SyncDispatcher`** | Enqueueing a Hangfire job for `SyncModuleExecutor.ExecuteAsync` and logging the resulting `JobId` once. | Module selection. Execution. Persistence. Retry policy. State. |
| **`FakeSyncRecurringTrigger`** (and any future recurring trigger) | Choosing module name + direction. Minting a fresh `CorrelationId`. Tagging `TriggeredBy = "scheduled"`. Calling the dispatcher. | Logging. Execution. Retry. Persistence. Any business decision. |
| **`SyncModuleExecutor`** | Translating a Hangfire-invoked method call into a module call: resolve module via registry → build `SyncContext` → invoke `PullAsync`/`PushAsync` → log result → throw on `Success == false` so Hangfire marks the job `Failed`. | Module business logic. Job lifecycle. Persistence. |
| **`ISyncModule`** implementations | Pure business logic. Reading external state, mapping, writing internal state, returning a `SyncResult`. | Hangfire APIs. Job state. Retry. Scheduling. Correlation generation. |

**Hangfire owns execution lifecycle. `SyncDispatcher` only enqueues. `SyncModuleExecutor` only translates Hangfire job → module execution. Sync modules are pure business logic only.**

---

## 3. Code Changes Applied in This Refinement

All changes are **deletions** or **collapses**. No additions.

### 3.1 `SyncRunMetadata` — slimmed (Phase-0 contract amendment)

`src/5.Sync/CapitalUniversity.Sync.Abstractions/Models/SyncRunMetadata.cs`

| Field | Status | Reason |
|---|---|---|
| `CorrelationId` | **Kept** | System trace identity. |
| `TriggeredBy` | **Kept** | Domain-level cause-of-run ("scheduled", "manual", "repair"). Not Hangfire-derivable. |
| `Tags` | **Kept** (unchanged) | Optional domain tags. |
| `RunId` | **Removed** | Redundant with `CorrelationId` in Phase 1 (see §1). |
| `CreatedAt` | **Removed** | Scheduling metadata. Hangfire already records job creation time; duplicating it created a shadow job model. |

This is a Phase-0 contract amendment landing in Phase 1. The Phase 0 audit reflects the original definition; this document supersedes it for the `SyncRunMetadata` shape.

### 3.2 `FakeSyncRecurringTrigger` — made dumb

`src/5.Sync/CapitalUniversity.Sync.Host/Scheduling/FakeSyncRecurringTrigger.cs`

| Concern | Before | After |
|---|---|---|
| Dependencies | `ISyncDispatcher`, `ISyncLogger` | `ISyncDispatcher` only |
| Identity generation | `RunId = Guid.NewGuid()`, `CorrelationId = Guid.NewGuid()` | `CorrelationId = Guid.NewGuid()` only |
| Logging | `_logger.LogInformation("Recurring trigger fired. …")` | **Removed** (dispatcher logs the enqueue) |
| Body length | 12 lines | 7 lines |
| Returns | `async Task` | `Task` (no awaits) |

The trigger is now a four-line method: pick module, pick direction, mint `CorrelationId`, call dispatcher. Any future recurring trigger should look identical in shape.

### 3.3 `SyncDispatcher` — collapsed to one log line

`src/5.Sync/CapitalUniversity.Sync.Infrastructure/Dispatching/SyncDispatcher.cs`

| Concern | Before | After |
|---|---|---|
| Log lines per dispatch | 2 (`"Dispatching sync job…"` + `"Sync job enqueued…"`) | 1 (`"Sync job enqueued… JobId=…"`) |
| `RunId` references in logs | Yes | None |
| Behavior | Enqueue + 2 logs | Enqueue + 1 log |

The single post-enqueue log line is the dispatcher's only observability output and is intentionally minimal: it exists so operators can pivot from a `CorrelationId` in trace logs to a `JobId` in the Hangfire dashboard.

### 3.4 `SyncModuleExecutor` — dropped `RunId` from logs

`src/5.Sync/CapitalUniversity.Sync.Infrastructure/Execution/SyncModuleExecutor.cs`

The `"Sync execution started. …"` line no longer renders `RunId={RunId}`. All other behavior is unchanged: `[AutomaticRetry(Attempts = 0)]`, module resolution, `SyncContext` construction, Pull/Push dispatch, error wrapping into `SyncExecutionException`, throw-on-`Success == false` for dashboard visibility.

### 3.5 `FakeSyncModule` — dropped `RunId` from logs

`src/5.Sync/CapitalUniversity.Sync.Host/Modules/Fake/FakeSyncModule.cs`

Log lines no longer pass `context.Metadata.RunId` (the field is removed). The 250 ms pull / 150 ms push delays and `SyncResult.Ok(5, …)` / `SyncResult.Ok(3, …)` return values are **unchanged**. Module behavior is preserved.

---

## 4. What Was **Not** Changed

- `ISyncModule` contract — unchanged.
- `ISyncDispatcher.DispatchAsync(moduleName, direction, metadata, ct)` signature — unchanged.
- `SyncContext` shape and `CorrelationId` passthrough semantics — unchanged.
- `SyncResult` shape, factory methods, semantics — unchanged.
- `ISyncModuleRegistry`, `ISyncCheckpointStore`, `ISyncLogger` — unchanged.
- Hangfire as execution engine — unchanged. Storage, server, dashboard config — unchanged.
- `SyncRecurringJobsRegistrar` (hosted service that registers the recurring job at startup) — unchanged.
- `AllowAllDashboardAuthorizationFilter` (dev-only) — unchanged.
- Project structure, csproj package references, DI wiring — unchanged.
- `FakeSyncModule` return values, processing delays, success semantics — unchanged.

No interfaces were modified. No new systems were introduced. No business logic was altered.

---

## 5. Final Execution Flow (simplified)

```
                ┌──────────────────────────────┐
                │ Hangfire Recurring Scheduler │  ← owns scheduling
                └──────────────┬───────────────┘
                               │ (every minute, cron-fired)
                               ▼
                ┌──────────────────────────────┐
                │ FakeSyncRecurringTrigger     │  ← thin: pick (module, direction), mint CorrelationId, call dispatcher
                └──────────────┬───────────────┘
                               │
                               ▼
                ┌──────────────────────────────┐
                │ ISyncDispatcher              │  ← thin: enqueue Hangfire job, log JobId once
                └──────────────┬───────────────┘
                               │ IBackgroundJobClient.Enqueue
                               ▼
                ┌──────────────────────────────┐
                │ Hangfire Storage + Worker    │  ← owns storage, retry, state, cancellation
                └──────────────┬───────────────┘
                               │
                               ▼
                ┌──────────────────────────────┐
                │ SyncModuleExecutor           │  ← thin: resolve → invoke → translate result
                │ [AutomaticRetry(Attempts=0)] │
                └──────────────┬───────────────┘
                               │ Pull / Push
                               ▼
                ┌──────────────────────────────┐
                │ ISyncModule (FakeSyncModule) │  ← pure business logic
                └──────────────────────────────┘
```

### Observed log trace (single tick, verified at runtime)

```
Sync job enqueued. Module=fake Direction=Pull TriggeredBy=scheduled JobId=2db09b51-…
Sync execution started. Module=fake Direction=Pull TriggeredBy=scheduled
FakeSyncModule.Pull tick. Module=fake
Sync execution succeeded. Module=fake Direction=Pull Processed=5 Failed=0 Duration=00:00:00.2560071
```

Four lines per tick, down from six pre-refinement. `CorrelationId` is no longer rendered as a positional argument because it lives in the MEL log scope (`SyncCorrelationId` key) and is emitted by the configured log provider — moving it out of the message template removed repetition without losing the trace.

HTTP probes (re-verified after refinement): `/` → 200, `/healthz` → 200, `/hangfire` → 200.

---

## 6. Removed Responsibilities

| Component | Responsibility removed | Where it lives now |
|---|---|---|
| `SyncRunMetadata` | Carrying `RunId` | n/a — folded into `CorrelationId` |
| `SyncRunMetadata` | Carrying `CreatedAt` | n/a — Hangfire records job creation time |
| `FakeSyncRecurringTrigger` | Generating `RunId` | n/a — removed entirely |
| `FakeSyncRecurringTrigger` | Logging "trigger fired" | n/a — redundant with dispatcher's enqueue log |
| `FakeSyncRecurringTrigger` | Holding an `ISyncLogger` reference | n/a — trigger no longer logs |
| `SyncDispatcher` | Pre-enqueue "Dispatching" log line | n/a — collapsed into the single post-enqueue line |
| `SyncDispatcher` | Logging `RunId` and `TriggeredBy` twice | n/a — logged once with `JobId` |
| `SyncModuleExecutor` | Rendering `RunId` in log messages | n/a — `CorrelationId` in MEL scope is sufficient |
| `FakeSyncModule` | Rendering `RunId` in log messages | n/a — same reason |

No responsibility was **added** to any component.

---

## 7. Confirmation Statements

- **No domain logic was changed.** `FakeSyncModule` returns the same `SyncResult` values with the same simulated delays. Pull → `Ok(5, ~250ms)`. Push → `Ok(3, ~150ms)`.
- **No interfaces were modified.** `ISyncModule`, `ISyncDispatcher`, `ISyncModuleRegistry`, `ISyncCheckpointStore`, `ISyncLogger` are byte-for-byte the same in their `.cs` files.
- **No new systems were introduced.** No event bus, no message broker, no pipeline, no orchestration layer, no new abstraction. The refinement is exclusively deletions and a one-line log collapse.
- **Hangfire remains the only execution engine.** Storage, queues, retry, scheduling, dashboard — all owned by Hangfire. The Sync abstractions layer has zero Hangfire references.
- **Public API surface is unchanged.** `ISyncDispatcher.DispatchAsync(string, SyncDirection, SyncRunMetadata, CancellationToken) → Task<string>` is identical to its Phase 1 original.

---

## 8. Build & Runtime Status

| Check | Result |
|---|---|
| `dotnet build` — Debug, `TreatWarningsAsErrors=true` | ✅ 0 warnings, 0 errors |
| Sync.Host boots, Hangfire server announces | ✅ |
| Recurring job `fake-sync-pull` registered | ✅ |
| End-to-end tick: trigger → dispatcher → Hangfire → executor → module → success | ✅ |
| `JobId` logged at dispatch | ✅ (`2db09b51-…`) |
| Dashboard at `/hangfire` | ✅ 200 |
| Pre-existing unrelated build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged from prior phases. Not introduced here. |

---

**Phase 1 refinement complete. Stopping. Awaiting explicit instruction to proceed to Phase 2 (Durable Hangfire Infrastructure).**
