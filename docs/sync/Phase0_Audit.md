# Phase 0 — Minimal Foundation (Hangfire-Aligned): Audit

**Date:** 2026-05-28
**Phase:** 0 — Minimal Foundation (Core Contracts Only)
**Status:** Completed (revised)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`
**Revision reason:** Remove all abstractions that duplicate Hangfire responsibilities (job storage, queueing, retry, scheduling). Hangfire is the **only** job system in the architecture.

---

## 1. Architectural Rule (binding)

Hangfire owns:

- Job storage
- Job queues
- Retry policies
- Recurring schedules
- Job state transitions and dashboard

The Sync abstractions layer therefore **must not** define:

- A custom job store
- A custom queue
- A custom retry engine
- A custom scheduler
- A custom job descriptor that mirrors Hangfire's internal job model

The abstractions layer defines only what Hangfire cannot define for us: **business contracts** (modules, execution context, results, checkpoints, logging).

---

## 2. Implemented Components

### Project

| Item | Path |
|---|---|
| Project | `src/5.Sync/CapitalUniversity.Sync.Abstractions/CapitalUniversity.Sync.Abstractions.csproj` |
| Target framework | `net9.0` |
| Package references | **none** |
| Project references | **none** |
| `TreatWarningsAsErrors` | `true` |
| `Nullable` | `enable` |
| `ImplicitUsings` | `enable` |

### Interfaces (kept — minimum stable set)

| Interface | Namespace | Purpose |
|---|---|---|
| `ISyncModule` | `Modules` | Per-domain Pull/Push business contract |
| `ISyncModuleRegistry` | `Modules` | Module resolution by name |
| `ISyncDispatcher` | `Dispatching` | **Enqueue-only** entry point into Hangfire |
| `ISyncCheckpointStore` | `Persistence` | Per-module incremental-sync checkpoint (business state, not job state) |
| `ISyncLogger` | `Logging` | Correlation-aware structured logging contract |

### Models (execution-context only)

| Model | Purpose |
|---|---|
| `SyncDirection` | Enum: `Pull = 0`, `Push = 1` |
| `SyncRunMetadata` | `RunId`, `CorrelationId`, `TriggeredBy`, `CreatedAt`, `Tags` — travels with the run |
| `SyncContext` | `ModuleName`, `Direction`, `StartedAt`, `Metadata` (+ `CorrelationId` passthrough) |
| `SyncCheckpoint` | `ModuleName`, `LastSyncedAt`, `Cursor`, `LastRowVersion`, `LastExternalVersion` |
| `SyncResult` | `Success`, `RecordsProcessed`, `RecordsFailed`, `Duration`, `ErrorMessage`, `Warnings` + `Ok`/`Failed` factories |

### Errors

| Exception | Purpose |
|---|---|
| `SyncException` | Base; optional `CorrelationId` + `ModuleName` |
| `SyncModuleNotFoundException` | Registry miss |
| `SyncExecutionException` | Execution-time failure with correlation context |

---

## 3. Removed in This Revision (Hangfire misalignment)

| Removed | Reason |
|---|---|
| `ISyncJobStore` | Job persistence is Hangfire's responsibility. A parallel job store would duplicate Hangfire storage (`HangfireDbContext` / SQL Server schema) and create two sources of truth for job state. |
| `SyncJobDescriptor` | Duplicated Hangfire's internal job model (`JobId`, `CreatedAt`, queue, state). Hangfire produces and owns its own job id on `Enqueue`. |
| `ISyncDispatcher.DispatchAsync(SyncJobDescriptor, ...)` signature | Replaced with a Hangfire-native enqueue signature that returns the orchestrator's opaque job identifier. |

`sync_jobs` table from Phase 3 (per spec) is **operational audit metadata**, not a job-store replacement — Hangfire still owns job state; the operational table records run-level audit. That distinction is preserved and is a Phase 3 concern, not Phase 0.

---

## 4. Revised Contracts (essentials)

### `ISyncModule`

```csharp
public interface ISyncModule
{
    string ModuleName { get; }

    Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken);

    Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken);
}
```

### `ISyncModuleRegistry`

```csharp
public interface ISyncModuleRegistry
{
    IReadOnlyCollection<string> RegisteredModules { get; }

    ISyncModule Resolve(string moduleName);

    bool TryResolve(string moduleName, out ISyncModule? module);
}
```

### `ISyncDispatcher` (Hangfire-native)

```csharp
public interface ISyncDispatcher
{
    Task<string> DispatchAsync(
        string moduleName,
        SyncDirection direction,
        SyncRunMetadata metadata,
        CancellationToken cancellationToken);
}
```

- Returns the **background-job identifier** produced by the orchestrator (Hangfire job id).
- The dispatcher **only enqueues**. It does not execute, persist, schedule, or retry.
- The concrete Phase-1 implementation wraps `IBackgroundJobClient.Enqueue(...)` and queues a Hangfire-native method invocation against the `SyncModuleExecutor`.

### `ISyncCheckpointStore`

```csharp
public interface ISyncCheckpointStore
{
    Task<SyncCheckpoint?> GetAsync(string moduleName, CancellationToken cancellationToken);
    Task SaveAsync(string moduleName, SyncCheckpoint checkpoint, CancellationToken cancellationToken);
}
```

Owns **business checkpoint** state (incremental sync cursor), not job state.

### `ISyncLogger`

```csharp
public interface ISyncLogger
{
    void LogDebug(Guid correlationId, string message, params object?[] args);
    void LogInformation(Guid correlationId, string message, params object?[] args);
    void LogWarning(Guid correlationId, string message, params object?[] args);
    void LogError(Guid correlationId, Exception? exception, string message, params object?[] args);
    IDisposable BeginCorrelationScope(Guid correlationId);
}
```

---

## 5. Execution Model (conceptual — Hangfire-native)

```text
Recurring Trigger  ──▶  ISyncDispatcher.DispatchAsync(...)
                              │
                              ▼
                  IBackgroundJobClient.Enqueue<SyncModuleExecutor>(
                      x => x.ExecuteAsync(moduleName, direction, metadata, ...))
                              │
                              ▼
                  Hangfire owns: storage · queue · retry · state
                              │
                              ▼
                  SyncModuleExecutor (Phase 1)
                     → ISyncModuleRegistry.Resolve(moduleName)
                     → ISyncModule.PullAsync / PushAsync(SyncContext)
                              │
                              ▼
                         SyncResult
                              │
                              ▼
                  ISyncLogger (correlation-tagged structured logs)
```

Sync abstractions **do not see Hangfire types**. The dispatcher implementation in Phase 1 is the only Hangfire boundary.

---

## 6. Architectural Decisions (revised)

1. **Hangfire is the only job system.** No parallel job store, queue, retry, or scheduler abstractions exist in Phase 0 or any later phase.
2. **`ISyncDispatcher` returns `Task<string>`** — the opaque job id the orchestrator produces. Abstraction stays portable; the value is whatever the engine returns (Hangfire job id today).
3. **`SyncRunMetadata` carries `CorrelationId`** so it travels with the run through Hangfire's serialization boundary. `SyncContext` exposes a `CorrelationId` passthrough for ergonomic logging.
4. **Zero-dependency abstractions assembly** preserved — no Hangfire reference in `Sync.Abstractions`. Hangfire enters only at the Phase-1 host/infrastructure boundary.
5. **`ISyncCheckpointStore` survives** — checkpoints are business state (incremental sync cursor), not job state. Not a Hangfire concern.
6. **Folder layout unchanged**: `Modules/`, `Dispatching/`, `Persistence/`, `Models/`, `Errors/`, `Logging/`.
7. **All models remain `sealed` + `init` + `required`** — immutability is a contract invariant.

---

## 7. Deviations (from `Sync_Implementation_Plan.md` Phase 0 spec)

| # | Deviation | Rationale | Risk |
|---|---|---|---|
| D1 | Spec lists `ISyncJobStore` in Phase 0 Core Interfaces. **Removed.** | Duplicates Hangfire storage. Architecturally invalid per the binding rule above. | None — operational `sync_runs`/`sync_jobs` audit tables in Phase 3 cover audit needs separately from job state. |
| D2 | Spec lists `SyncJobDescriptor` in Phase 0 Core Models. **Removed.** | Duplicates Hangfire's job model. | None — `SyncRunMetadata` carries every business field that survives serialization through Hangfire arguments. |
| D3 | `ISyncDispatcher` signature redefined to enqueue-only with primitive parameters + `SyncRunMetadata`. | Hangfire-native invocation model; eliminates dispatcher-owned descriptors. | None. |
| D4 | `Sync_Platform_Model.md` proposes `src/5.Sync/` + `src/6.Application/`. Repo has `src/5.Application/`. Sync placed under `src/5.Sync/` as sibling **without** renaming `5.Application`. | Rename is destructive and out of scope. | None for Phase 0. |
| D5 | `ISyncModule` returns `Task<SyncResult>` (spec shows `Task`). | A result is needed to drive logging, audit, and Phase 3 persistence. Strict superset. | None. |
| D6 | `ISyncLogger` is correlation-first (explicit `Guid correlationId` per call) rather than wrapping `ILogger<T>`. | Forces correlation into the contract instead of relying on ambient scopes. Phase 1 adapter wraps MEL trivially. | None. |

No future-phase functionality is introduced.

---

## 8. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `SyncCheckpoint` carries `Cursor`, `LastRowVersion`, `LastExternalVersion` simultaneously — module ambiguity. | Low | Phase 4 `ChangeDetector` / `MergeEngine` codifies per-strategy selection. Phase 0 stays permissive. |
| `SyncRunMetadata.TriggeredBy` is a free-form string. | Low | Constrain in Phase 3 once real trigger sources exist. Avoiding premature enum per Phase 0 rules. |
| `ISyncLogger` divergence from `Microsoft.Extensions.Logging.ILogger<T>`. | Low | Phase 1 adapter wraps MEL. No business code calls MEL directly. |
| `ISyncDispatcher` returning a plain `string` job id ties the abstraction to "engines that expose a string id". | Very low | Hangfire matches this exactly. If a future engine returns a typed handle, an overload can be added without breaking callers. |

---

## 9. Future Concerns (deferred per spec — recorded, not implemented)

- `ISyncEventPublisher`, `ISyncEventHandler`, `ISyncExecutionHooks`, `ISyncNotificationHandler`, `ISyncAuthorizationContext` — deferred until Phase 1 runtime reveals real need.
- Outbox / Inbox abstractions — Phase 3.
- Bulk-write abstractions — Phase 4 / Phase 8.
- DI extension methods (`AddSyncAbstractions(...)`) — Phase 1.
- Operational audit tables (`sync_runs`, `sync_jobs`, `sync_failures`, `sync_dead_letters`, `sync_checkpoints`) — Phase 3. These are **audit/business** tables and do not replace Hangfire job storage.
- Recurring-job registration (Hangfire `RecurringJob.AddOrUpdate`) — Phase 1+.
- Push outbox flow — Phase 6.

---

## 10. Technical Debt Introduced

| Debt | Repaid in |
|---|---|
| `SyncRunMetadata.Tags` is a `Dictionary<,>` exposed as `IReadOnlyDictionary<,>` — not deep-immutable. | Phase 4 / Phase 5 |
| `TriggeredBy` as raw string. | Phase 3 |
| Naming-convention divergence (doc `6.Application` vs repo `5.Application`). | Out-of-scope refactor |

---

## 11. Testability Status

- Pure contract assembly; no behavior to test directly.
- All interfaces mockable.
- Models constructible without a container (`init` + `required`).
- `SyncResult.Ok` / `SyncResult.Failed` factories serve as the canonical test fixtures.
- Architecture tests (in `tests/Architecture.Tests`) can enforce from Phase 1 that:
  - `Sync.Abstractions` references no Hangfire type
  - No business module references Hangfire directly
  - Dispatcher implementation is the only Hangfire boundary

---

## 12. Production-Readiness Evaluation

| Dimension | Status | Notes |
|---|---|---|
| Compiles cleanly | ✅ | Debug + Release, zero warnings, `TreatWarningsAsErrors=true`. |
| Runtime impact | ✅ | Contracts-only assembly; no runtime paths. |
| External deps | ✅ | None. Hangfire not referenced. |
| Backward compat | ✅ | New assembly, no consumers yet. |
| Independently runnable | N/A — contracts only. Phase 1 introduces runtime. |
| Solution health | ✅ | Project registered under `5.Sync` solution folder, nested under `src`. |
| Pre-existing unrelated build error | ⚠️ | `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs` references `NotFoundException` on `main-enforced` — pre-existing, not introduced here. |

---

## 13. Phase Exit Checklist

- [x] Only Phase-0 components implemented
- [x] No custom job store, queue, retry, or scheduler
- [x] No `SyncJobDescriptor`
- [x] `ISyncDispatcher` is enqueue-only and Hangfire-compatible
- [x] No Hangfire reference in `Sync.Abstractions`
- [x] No future-phase leakage
- [x] All deferred items explicitly listed
- [x] Project compiles standalone (Debug + Release)
- [x] Project registered in solution under `5.Sync` folder
- [x] Audit document revised
- [x] Ready for Phase 1 (Hangfire Host Skeleton)

---

**Phase 0 (Hangfire-aligned) complete. Stopping. Awaiting explicit instruction to proceed to Phase 1.**
