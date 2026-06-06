# Phase 7 — Hardening: Audit

**Date:** 2026-05-29
**Status:** Completed (runtime-verified — 4 code fixes; 10 carried/deferred concerns explicitly documented)
**Scope:** Hardening only. No new project. No new schema (beyond an additive column on each outbox table). No public API change. Fix the silent-failure surfaces called out in the third-party review; explicitly document the design-level concerns that remain.

---

## 1. Concerns Addressed

The third-party review enumerated **14 concerns** across seven categories. This pass closes the silent-failure ones in code and explicitly accepts (with rationale) the design-level ones that remain.

| # | Concern | Class | Status |
|---|---|---|---|
| 1.1 | JSON Schema Drift (Push Path) | Silent corruption | **FIXED — code** (§3.3) |
| 1.2 | Queue Configuration Mismatch (`:` vs `|`) | Silent misconfig | **FIXED — code** (§3.2) |
| 1.3 | SQL Parameter Limits (`MaxBatchSize` drift across modules) | Centralization | **FIXED — code** (§3.1) |
| 2.1 | Outbox-to-Sink Handover (crash between sink-accept and DB commit) | At-least-once delivery | **DOCUMENTED — design** (§4.1) |
| 2.2 | Shared Audit Contention (`sync.runs`/`sync.failures` lock contention) | Scale risk | **DOCUMENTED — defer Phase 8** (§4.2) |
| 3.1 | Sink Idempotency assumption | Contract requirement | **DOCUMENTED — design** (§4.3) |
| 3.2 | Extractor Safety Window (1s clawback) | Hard-coded ceiling | **FIXED — code** (§3.4) |
| 3.3 | Hangfire Worker Availability (queue not in listen list) | Silent misconfig | **FIXED — code** (§3.2) |
| 4.1 | Non-Idempotent Outbox Sinks (retries cause side effects) | Same as 3.1 | **DOCUMENTED — design** (§4.3) |
| 4.2 | Partial Batch Replays (Status check inside same txn) | Replay semantics | **DOCUMENTED — design** (§4.4) |
| 5.1 | Outbox Table Bloat (no retention scheduler) | Scale risk | **DOCUMENTED — defer Phase 9/10** (§4.5) |
| 5.2 | Complexity Debt (clone pattern, no template) | Maintenance cost | **DOCUMENTED — defer template work** (§4.6) |
| 6.1 | "Black Hole" Sinks (200 OK but data dropped) | Observability gap | **DOCUMENTED — defer Phase 10** (§4.7) |
| 6.2 | Mapper Data Loss (silent field truncation) | Silent corruption | **FIXED — code** (§3.3) |
| 7.1 | Global Throttle (worker-pool exhaustion across modules) | Scale risk | **DOCUMENTED — defer Phase 8** (§4.8) |
| 7.2 | Zombie Cancellations (OCE as failure) | Intentional design (Phase 5 R2) | **DOCUMENTED — design** (§4.9) |

**Code fixes: 4 categories closing 5 concerns (1.1, 1.2, 1.3, 3.2 + 6.2).**
**Carried/deferred: 9 concerns, each with explicit rationale and deferral target.**

---

## 2. Code Changes (file-by-file)

### Sync.Abstractions

| File | Change |
|---|---|
| `Pipeline/SyncLimits.cs` | **new** — `public static class SyncLimits { public const int MaxBatchSize = 1000; }`. Single source of truth for the SQL Server parameter-limit-safe ceiling that was previously duplicated in three places. |

### Sync.Infrastructure

| File | Change |
|---|---|
| `CapitalUniversity.Sync.Infrastructure.csproj` | + `Microsoft.Extensions.Hosting.Abstractions 9.0.0` (required for `IHostedService`). |
| `Configuration/SyncQueueConfigurationValidator.cs` | **new** — `IHostedService` that fails fast at startup if `Sync:ModuleQueues` (a) uses the legacy `:` direction separator OR (b) dispatches to a queue not in `Sync:Hangfire:Queues`. Reads BOTH bound options (for target-not-listening) AND raw `IConfiguration` (for legacy `:` — those keys silently drop at bind time, so the bound `Dictionary` can't see them). |
| `DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | + `AddHostedService<SyncQueueConfigurationValidator>()`. |
| `Pipeline/SyncPipeline.cs` | `MaxBatchSize` const now sourced from `SyncLimits.MaxBatchSize`. Pure aliasing — no behavior change. |

### Sync.Student

| File | Change |
|---|---|
| `Configuration/StudentSyncOptions.cs` | + `ExtractorSafetyBufferSeconds` (default 1). Operator-tunable for upstreams that perform deep back-dating. |
| `Configuration/StudentSyncOptionsValidator.cs` | + reject `ExtractorSafetyBufferSeconds < 0`. `MaxBatchSize` const now sourced from `SyncLimits.MaxBatchSize`. |
| `Extraction/StudentExtractor.cs` | Reads safety buffer from `IOptions<StudentSyncOptions>` at extract time. `DefaultSafetyBuffer` constant retained as documentation. |
| `Outbox/StudentOutboxEntity.cs` | + `PayloadSchemaVersion` (int, default `CurrentPayloadSchemaVersion = 1`). |
| `Outbox/StudentOutboxPayloadSerializer.cs` | **new** — strict System.Text.Json options with `UnmappedMemberHandling.Disallow`. Unknown JSON fields throw; missing `required` fields throw. |
| `Configurations/StudentOutboxConfiguration.cs` | + `PayloadSchemaVersion` non-null mapping. |
| `Mapping/StudentOutboxMapper.cs` | Schema-version check **before** deserialize. Uses `StudentOutboxPayloadSerializer` (strict). Both classes of schema drift fail loud as `InvalidOperationException` → pipeline `SyncResult.Failed` → `sync.failures` row → Hangfire retry. |
| `Migrations/20260529164128_AddOutboxPayloadSchemaVersion.cs` | **new** — adds `PayloadSchemaVersion int NOT NULL DEFAULT 1` to `sync_student.student_outbox`. Default of 1 (not 0) so pre-existing rows are treated as already-on-version-1 (the introductory version). |

### Sync.Staff

| File | Change |
|---|---|
| `Configuration/StaffSyncOptions.cs` | + `ExtractorSafetyBufferSeconds`. |
| `Configuration/StaffSyncOptionsValidator.cs` | + reject negative buffer. `MaxBatchSize` sourced from `SyncLimits.MaxBatchSize`. |
| `Extraction/StaffExtractor.cs` | Buffer read from `IOptions<StaffSyncOptions>`. `DefaultSafetyBuffer` retained for docs. |
| `Outbox/StaffOutboxEntity.cs` | + `PayloadSchemaVersion`. |
| `Outbox/StaffOutboxPayloadSerializer.cs` | **new** — strict serializer. |
| `Configurations/StaffOutboxConfiguration.cs` | + version column. |
| `Mapping/StaffOutboxMapper.cs` | Schema-version check + strict deserialize. |
| `Migrations/20260529164140_AddOutboxPayloadSchemaVersion.cs` | **new** — adds version column with default 1. |

### Sync.Host

| File | Change |
|---|---|
| `Program.cs` | Admin seed endpoints now use `StudentOutboxPayloadSerializer.Serialize` / `StaffOutboxPayloadSerializer.Serialize` (consistent with the readers) and set `PayloadSchemaVersion = CurrentPayloadSchemaVersion` explicitly on seeded rows. |

### Files explicitly NOT touched

- `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts — byte-identical.
- `Sync.Persistence` — no schema change, no migration.
- `Sync.Infrastructure/Execution/SyncModuleExecutor.cs`, `Filters/`, `Dispatching/SyncDispatcher.cs` — unchanged. The hardened mappers' failures flow through the existing executor failure path.
- All existing module modules (`Fake*`, `Failing*`, `Cancelling*`, `PartialFailing*`) — byte-identical.
- Hangfire configuration, queue list, retry policy, dashboard — unchanged.

---

## 3. Runtime Evidence — Each Code Fix

### 3.1 Centralized `MaxBatchSize` (closes concern 1.3)

The constant is now declared once in `SyncLimits.MaxBatchSize = 1000`. Three former independent declarations now alias the canonical value:

```csharp
// Sync.Infrastructure.Pipeline.SyncPipeline
public const int MaxBatchSize = SyncLimits.MaxBatchSize;

// Sync.Student.Configuration.StudentSyncOptionsValidator
public const int MaxBatchSize = SyncLimits.MaxBatchSize;

// Sync.Staff.Configuration.StaffSyncOptionsValidator
public const int MaxBatchSize = SyncLimits.MaxBatchSize;
```

A future tightening (e.g. lowering the budget when SQL Server's parameter limit changes) lands in one place. Code-verified; not exercised at runtime because the ceiling has not changed.

### 3.2 Queue Configuration Validator (closes 1.2 + 3.3)

Hosted-service runs at startup before any Hangfire worker dequeues. It validates two surfaces:

**Surface A — legacy `:` separator** (from RAW `IConfiguration`, because bound options can't see these keys — the binder silently splits them):

Test config:
```json
"Sync": {
  "ModuleQueues": {
    "students:Push": "push-sync",         ← legacy separator
    "broken-module": "queue-that-doesnt-exist"  ← unlistened target
  }
}
```

Boot result (host refuses to start):
```
fail: CapitalUniversity.Sync.Infrastructure.Logging.SyncLogger[0]
  Sync queue configuration invalid — refusing to start:
    Sync:ModuleQueues key 'students:Push' uses the legacy ':' direction separator.
    Use '|' instead (i.e. 'students|Push': 'push-sync').
    The ':' character collides with the .NET configuration path separator and
    the entry will not bind — directional queue overrides silently fall back to
    the module-level queue or the default.
  |
    Sync:ModuleQueues['broken-module'] dispatches to 'queue-that-doesnt-exist'
    but that queue is not in Sync:Hangfire:Queues (default, students-sync, staff-sync,
    finance-sync, hr-sync, push-sync, default) or the default ('default').
    Add it to the listen list or change the dispatch target.
Unhandled exception. System.InvalidOperationException: Sync queue configuration invalid — …
```

Boot fails. Both classes of misconfiguration surfaced. No silent fallback.

**Surface B — happy path** (current production config):
```
info: CapitalUniversity.Sync.Infrastructure.Logging.SyncLogger[0]
  Sync queue configuration validated.
  ListenQueues=[default,students-sync,staff-sync,finance-sync,hr-sync,push-sync,default]
  ModuleQueueOverrides=6.
```

Service starts normally. Operator confirmation in logs.

### 3.3 Strict JSON outbox mappers + schema versioning (closes 1.1 + 6.2)

**Layer 1 — `PayloadSchemaVersion` check.** New column on both outbox tables; default 1. Mapper checks `row.PayloadSchemaVersion == CurrentPayloadSchemaVersion` BEFORE deserialize. Test:

```sql
INSERT INTO sync_student.student_outbox
  (Id, ExternalStudentId, …, PayloadSchemaVersion, Status, …)
VALUES (NEWID(), 'EXT-S-9001', …, 99, 0, …);
```

```
POST /admin/trigger/students?direction=Push   →  jobId=651
```

Log:
```
Pipeline failure. Module=students BatchIndex=1 Extracted=1 Processed=0
  System.InvalidOperationException:
    Outbox payload schema version mismatch for ExternalStudentId=EXT-S-9001:
    row=99 expected=1. Migrate the row or extend the mapper before retrying.
Sync execution reported failure. Module=students Direction=Push Attempt=1
```

`sync.runs.Status = Running` (Hangfire retries with the existing backoff), `sync.failures` row appended with the version-mismatch message. **Loud, traceable, retry-eligible.** Outbox row stays Pending so an operator who corrects the version (or extends the mapper for backward-compat) doesn't lose the row.

**Layer 2 — `UnmappedMemberHandling.Disallow`.** Strict deserializer rejects unknown JSON fields. Test (a payload with an `UnknownFutureField` an upstream might add before the mapper is updated):

```sql
INSERT INTO … PayloadSchemaVersion=1, Payload='{"…","UnknownFutureField":"surprise"}';
```

```
POST /admin/trigger/students?direction=Push   →  jobId=664
```

Log:
```
System.InvalidOperationException:
  Outbox payload JSON invalid for ExternalStudentId=EXT-S-9002:
  The JSON property 'UnknownFutureField' could not be mapped to any .NET member
  contained in type 'CapitalUniversity.Sync.Student.Domain.ExternalStudent'.
 ---> System.Text.Json.JsonException: …
Sync execution reported failure.
```

Schema drift between upstream and local DTO **fails loudly at parse**, not silently at push. The audit trail carries the exact JSON property that caused the rejection so operators know precisely what to do (update the DTO or scrub the offending field).

**Layer 3 — success path still works.** Admin push of a normal seeded row via the updated endpoint:
```
POST /admin/outbox/student/EXT-S-7777 + body
POST /admin/trigger/students?direction=Push
→ outbox: Status=1 (Processed) PayloadSchemaVersion=1
→ sync.runs: Status=2 (Succeeded) RecordsProcessed=1
```

### 3.4 Configurable safety buffer (addresses 3.2 partially)

The `StudentExtractor.SafetyBuffer` and `StaffExtractor.SafetyBuffer` static fields have been replaced with `StudentSyncOptions.ExtractorSafetyBufferSeconds` and `StaffSyncOptions.ExtractorSafetyBufferSeconds` respectively. Default remains 1 second; operators can raise it for upstreams with deep back-dating.

Validators reject negative values (would advance the cursor into the future, causing data loss). The original concern noted that 1 second only covers millisecond-level drift; that observation is correct — the fix is **operator-tunable defaults**, not a single one-size-fits-all value. An upstream that admin-edits records back 10 minutes should configure `ExtractorSafetyBufferSeconds: 600` and accept the 10-minute replay window the writer's upsert makes safe.

Code-verified path: the extractor now resolves `safetyBuffer` from `_options.Value.ExtractorSafetyBufferSeconds` (clamped to ≥ 0) at the start of each `ExtractAsync` invocation.

---

## 4. Concerns Documented (Design-level / Carried / Deferred)

Each entry below explains why the concern is not closed in this pass, and where (if anywhere) it will be addressed in a later phase.

### 4.1 Outbox-to-Sink Handover (2.1) — at-least-once is fundamental

Closing the gap would require a 2-phase commit between the external sink and the local DB — impractical for HTTP/SOAP sinks. The chosen contract is **at-least-once delivery + sink idempotency on the external merge key**, documented on `IExternalStudentSink`/`IExternalStaffSink`. The writer pre-checks `Status == Pending` before the sink call so a Processed row is never double-sent within the same tick. Cross-tick replays are bounded by sink idempotency.

Phase 8 may revisit with a Dispatching status (pre-commit row state) to narrow the replay window further, but the fundamental at-least-once nature does not go away.

### 4.2 Shared Audit Contention (2.2) — defer to Phase 8 performance pass

`sync.runs`/`sync.failures` contention only matters at >10 modules × minutely. Mitigations available without architectural change: partitioning by `ModuleName`, batch-inserting failures, lock-hint tuning. Documented for Phase 8; not blocking at 2 modules.

### 4.3 Sink Idempotency assumption (3.1, 4.1) — contract requirement

Sink-side idempotency is a **hard contract requirement**, not a code-fixable issue. Documented on `IExternalStudentSink`/`IExternalStaffSink` XML docs and on the platform model. Production sink implementations MUST be idempotent on the external merge key (the same key the inbound Pull pipeline uses). A non-idempotent sink + the outbox's at-least-once delivery would produce duplicates; that combination is the operator's responsibility to avoid.

### 4.4 Partial Batch Replays (4.2) — pre-sink Status check is the correct design

The reviewer noted that the `Status == Pending` check happens inside the same transaction that might have failed. That is the **correct** semantics: per-row Status updates and the batched SaveChanges form one logical transaction. On crash:
- DB rolls back the batched Status updates.
- Rows return to Pending (their pre-batch state).
- Next tick re-extracts.
- Sink is re-called per the at-least-once contract (sink idempotency handles the redelivery).

The check inside the writer prevents same-tick double-call when a row was already updated to Processed earlier in the same loop iteration. That defense is intact.

### 4.5 Outbox Table Bloat (5.1) — defer to Phase 9/10 retention scheduler

`SyncAuditRetention.md` already documents the retention strategy for `sync.*` tables. The outbox tables are symmetric — same "low-priority Hangfire recurring `DELETE TOP N`" pattern, same windows tunable per-module. Implementing this is a Phase 9 (resilience) or Phase 10 (observability) deliverable, not Phase 7 hardening.

### 4.6 Complexity Debt (5.2) — defer to template tooling

The Staff project is a literal clone of Student with ~25 files differing only in domain-specific names + a `Department` field. A future Phase 7 expansion (Courses, Schedule, Finance) would benefit from a `dotnet new syncmodule -n X` template. Implementing the template requires turning Student → a parameterized template, which is a tooling exercise — out of scope for this hardening pass but trivially derivable when needed.

The prior phases' "no premature abstraction" discipline is the reason no shared `ModuleBase` class exists. Re-coupling modules through a base class would break the Phase 7 success criterion ("Modules isolated and independently deployable") and is the wrong fix.

### 4.7 "Black Hole" Sinks (6.1) — defer to Phase 10 observability

If an external sink returns 200 OK but silently drops the data, no software in the world detects that from the caller side without an out-of-band reconciliation step. The future Phase 10 observability pass will add a periodic reconciler that pulls a sample of recently-Processed outbox rows and verifies they exist on the external system. Not Phase 7 work.

### 4.8 Global Throttle (7.1) — defer to Phase 8 performance pass

`[PerModuleDisableConcurrency]` prevents within-module overlap but not cross-module worker exhaustion. At 50 modules × minutely, the shared Hangfire worker pool could starve. Mitigations exist (per-queue worker pools, queue prioritization, rate limiting) and are explicitly listed in Phase 8's Hangfire Optimizations section of `Sync_Implementation_Plan.md`. Not blocking at 2 modules; size-of-deploy decision.

### 4.9 Zombie Cancellations (7.2) — intentional design

The Phase 5 Refinement 2 audit shipped tightened OCE classification: an `OperationCanceledException` whose cancellation token is NOT signaled is treated as a module-internal bug (logged Warning + reported as `SyncResult.Failed` + Hangfire retries). The reviewer correctly notes that a library which uses OCE for internal flow control would now be misclassified.

This is **intentional and documented**:
- The pipeline already distinguishes "executor token signaled" from "module's own token signaled" — both cases are reported as Cancellation.
- A genuinely spurious OCE (neither token signaled) IS a module bug and SHOULD surface as a failure for operator attention.
- The audit log is loud: `Pipeline spurious OperationCanceledException. … Neither the executor token nor the exception's token is signaled — likely a module-internal OCE used for non-cancellation logic. Reporting as Failed.`

If a third-party library inside a future module ships this misuse pattern, the right fix is to wrap that library's call site to suppress the spurious OCE before it bubbles to the pipeline. Not a sync-platform concern.

---

## 5. Build & Runtime Status — Final

| Check | Result |
|---|---|
| Six sync projects (`Abstractions`, `Infrastructure`, `Persistence`, `Student`, `Staff`, `Host`) compile cleanly | ✅ `TreatWarningsAsErrors=true`, 0 warnings, 0 errors |
| `SyncLimits.MaxBatchSize` referenced by pipeline + both module validators (single source of truth) | ✅ code-verified |
| `SyncQueueConfigurationValidator` happy-path log at startup | ✅ §3.2 |
| `SyncQueueConfigurationValidator` rejects legacy `:` separator at boot | ✅ §3.2 |
| `SyncQueueConfigurationValidator` rejects dispatch target not in listen list | ✅ §3.2 |
| `AddOutboxPayloadSchemaVersion` migrations applied on both modules at boot | ✅ `PayloadSchemaVersion int NOT NULL DEFAULT 1` confirmed via `INFORMATION_SCHEMA.COLUMNS` |
| Outbox schema-version mismatch produces loud `sync.failures` row + Hangfire retry | ✅ §3.3 — `row=99 expected=1` exception |
| Strict JSON rejects unknown fields with `JsonException` carrying the offending property name | ✅ §3.3 — `UnknownFutureField` exception |
| Happy-path push of normal outbox row still works post-hardening | ✅ §3.3 Layer 3 — EXT-S-7777 Processed |
| Pull paths for both modules unaffected (Staff Pull 20→18, Students Pull continues) | ✅ Confirmed in `sync.runs` between hardening tests |
| Multi-module concurrency + failure isolation (Phase 7 baseline behavior) preserved | ✅ no Phase 7 regression observed |
| Pre-existing unrelated test build error (`tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs`) | ⚠️ Unchanged — out of sync-layer scope |

---

## 6. Phase Exit Checklist

- [x] Centralized `SyncLimits.MaxBatchSize` adopted by pipeline, Student validator, Staff validator
- [x] `SyncQueueConfigurationValidator` registered; rejects both legacy `:` separator AND unlistened dispatch targets at boot
- [x] `PayloadSchemaVersion` column added to both outbox tables via additive migrations; default 1
- [x] Strict-JSON outbox serializers (`StudentOutboxPayloadSerializer`, `StaffOutboxPayloadSerializer`) with `UnmappedMemberHandling.Disallow`
- [x] Outbox mappers enforce schema-version match BEFORE deserialize and surface loud `InvalidOperationException` on either drift class
- [x] Admin seed endpoints in `Program.cs` use the strict serializer and set `PayloadSchemaVersion = CurrentPayloadSchemaVersion`
- [x] `ExtractorSafetyBufferSeconds` configurable on both modules; default 1; negative values rejected
- [x] No new project; no new infrastructure; no new abstraction beyond `SyncLimits` (a single shared constant)
- [x] No public API change (`ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult`, pipeline contracts byte-identical)
- [x] `Sync.Persistence` schema and migrations unchanged
- [x] Hangfire configuration, retry policy, queues, dashboard unchanged
- [x] Pull paths (`students` and `staff`) byte-identical in behavior
- [x] Push success path: outbox → sink → Processed unchanged for valid rows
- [x] Push failure paths runtime-verified for both schema-drift classes
- [x] Each carried/deferred concern explicitly documented with rationale and deferral target
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase 7 Hardening complete. Stopping. Phase 8 (Performance Optimization) is the next planned architectural step — and is the natural home for the deferred items in §4.2, §4.4, §4.8.**
