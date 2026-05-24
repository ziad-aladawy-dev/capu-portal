# Phase 1 — High-value bulk actions

**Tier:** P1
**Status:** all items landed (see per-item notes below). Build clean.
**Goal:** add the bulk endpoints admins/staff hit operational pain on today. Per-row API works but creates click-storms during semester rollover, dunning, queue review, and notification triage.

> **Read first:** [Cross-cutting design](#cross-cutting-design) at the bottom. Items below all use the shared envelope.

## Item table

| # | Item | Resource | Status | Owner | Notes |
|---|---|---|---|---|---|
| 1.1 | Bulk mark notifications read | Notifications | Done | | `PUT /api/notifications/read` + `PUT /api/notifications/mark-all-read` |
| 1.2 | Bulk transition course offerings (Draft → Open) | CourseOfferings | Done | | `POST /api/course-offerings/publish` |
| 1.3 | Bulk cancel course offerings | CourseOfferings | Done | | `POST /api/course-offerings/cancel` (reason required) |
| 1.4 | Bulk transition student service requests | StudentServiceRequests | Done | | `POST /api/student-service-requests/transition` — staff queue |
| 1.5 | Bulk cancel invoices | Invoices | Done | | `POST /api/invoices/cancel` (reason required) |
| 1.6 | Bulk add/remove courses on academic plan | AcademicPlans | Done | | `POST /api/academic-plans/{id}/courses/batch` — atomic all-or-nothing |
| 1.7 | Bulk create schedule slots for an offering | ScheduleSlots | Done | | `POST /api/schedule-slots/batch` — atomic, intra-batch overlap check |

## Per-item context (what landed)

### Shared envelope (cross-cutting prep — landed first)
Files created under [src/2.Core/CapitalUniversity.Core.Abstractions/Shared/BulkActions/](../../src/2.Core/CapitalUniversity.Core.Abstractions/Shared/BulkActions/):
- `BulkActionRequest.cs` — generic `<TPayload>` and parameterless variants.
- `BulkActionResult.cs` — `{ SucceededIds, Failures }`.
- `BulkActionFailure.cs` — `{ Id, Code, Message }`. `BulkFailureCodes` enum-like statics: `NotFound`, `Forbidden`, `InvalidTransition`, `Conflict`, `Validation`, `Unknown`.
- `BulkConstants.cs` — `MaxBulkSize = 500`.

### 1.1 Bulk mark notifications read
- `PUT /api/notifications/read` with body `{ ids: Guid[] }` — returns `{ marked: N }`.
- `PUT /api/notifications/mark-all-read` — no body, returns `{ marked: N }`.
- Idempotent: replays return `marked: 0`. Ids belonging to another user are silently skipped (no existence leak).
- Service: `INotificationService.MarkManyAsReadAsync` and `MarkAllAsReadAsync`.

### 1.2 / 1.3 Course offering bulk publish / cancel
- `POST /api/course-offerings/publish` — body `{ ids: Guid[] }`. Activates Draft → Open per row.
- `POST /api/course-offerings/cancel` — body `{ ids, payload: { reason } }`. Reason required.
- Service: `ICourseOfferingService.BulkPublishAsync` and `BulkCancelAsync`. Both delegate to a private `BulkApplyAsync` driver that reuses the existing `LoadForWriteAsync` (scope + mutability guards) per row and commits independently.
- Reason is plumbed through but not persisted on the entity yet — Phase-2 audit work owns the storage decision.

### 1.4 Bulk transition student service requests
- `POST /api/student-service-requests/transition` — body `{ ids, payload: MoveRequestWorkflowStateRequest }`.
- Service: `IStudentServiceRequestService.BulkTransitionAsync` — wraps the existing `MoveStateAsync` per row, so workflow validation, scope checks, and audit logging are preserved unchanged.
- Failure codes: `NotFound`, `InvalidTransition` (workflow-rejected), `Validation`.
- Single-row staff transition endpoints were already absent on the controller; this is the first staff-facing entry point. Staff id resolves from the JWT `NameIdentifier` claim (shared `ResolveCallerId` helper added).

### 1.5 Bulk cancel invoices
- `POST /api/invoices/cancel` — body `{ ids, payload: { reason } }`. Reason required.
- Service: `IInvoiceService.BulkCancelAsync` — delegates to the existing `CancelAsync` per row so the cache-invalidation + Open/Close + Paid-check paths stay identical.
- Already-Cancelled rows are treated as a successful no-op (idempotent replay). Paid/record-closed land in `failures` with `Code = Conflict`.

### 1.6 Bulk add/remove courses on a plan
- `POST /api/academic-plans/{id}/courses/batch` — body `{ add: AddPlanCourseRequest[], remove: Guid[] }`.
- **All-or-nothing**: a single failure rejects the whole batch (no partial application). Implemented as a single transaction.
- Allows "add + remove the same course in one batch" — duplicate detection runs against the post-removal composition.
- Service: `IAcademicPlanService.BatchUpdateCoursesAsync`.

### 1.7 Bulk create schedule slots for an offering
- `POST /api/schedule-slots/batch` — body `{ courseOfferingId, slots: BatchScheduleSlotItem[] }`.
- **All-or-nothing**, wrapped in `ExecuteInSerializableTransactionAsync` (same M1 protection as the single-row create).
- Intra-batch overlap check (Cartesian within the request, half-open intervals — adjacency stays legal).
- Per-slot DB conflict check against existing siblings.
- Service: `IScheduleSlotService.BatchCreateAsync` returns the new slot ids in input order.

## Cross-cutting design

### Bulk operation envelope

The shape that ships under [Core.Abstractions/Shared/BulkActions/](../../src/2.Core/CapitalUniversity.Core.Abstractions/Shared/BulkActions/):

```csharp
public class BulkActionRequest<TPayload>
{
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
    public TPayload? Payload { get; init; }
}

public sealed class BulkActionResult
{
    public int Succeeded => SucceededIds.Count;
    public int Failed => Failures.Count;
    public IReadOnlyList<Guid> SucceededIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<BulkActionFailure> Failures { get; init; } = Array.Empty<BulkActionFailure>();
}

public sealed class BulkActionFailure
{
    public Guid Id { get; init; }
    public string Code { get; init; } = BulkFailureCodes.Unknown;
    public string Message { get; init; } = string.Empty;
}
```

**Rules adopted:**
- HTTP `200 OK` always when the request itself is well-formed — clients consume the result payload to triage failures. `400` reserved for malformed input (missing ids, count over cap, missing required payload).
- `MaxBulkSize = 500`. Endpoints check explicitly and 400 on overflow.
- All-or-nothing items (1.6, 1.7) do NOT use the envelope — they throw on the first invalid step and rely on framework exception → status code mapping.

### Authorization pattern for bulk endpoints

- One `[HasPermission(...)]` check at the controller for the verb (`EditClose` for transitions/cancels, `Insert` for batch-create).
- Per-id scope check inside the handler — reuses each module's existing `LoadForWriteAsync` helper so the bulk path cannot diverge from the single-row path's scope semantics.
- Failed scope checks land in the failure map with `Code = NotFound` (not `Forbidden`) — matches the project-wide "no existence leak" rule already in force on single-row endpoints.

### Deferred / NOT implemented in this phase

- **Idempotency-Key header infrastructure.** The phase doc called for it, but full server-side dedupe is a separate feature (persistence + TTL + race-safe lookup). The operations themselves are naturally idempotent — replaying any bulk endpoint with the same ids is safe. Idempotency-Key plumbing can be added cross-cutting in a follow-up without touching individual endpoints.
- **Reason persistence on cancelled offerings/invoices.** Today reason is required on the request but only available in correlation logs. Phase-2 audit work owns the column choice.

### Open questions
- _Add as you go._
