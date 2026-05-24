# Phase 3 — Consistency and cleanup

**Tier:** P3
**Status:** all items landed (3.11 is a recorded policy decision, not code). Build clean.
**Goal:** retire duplicate endpoints, fill out the B-tier bulk operations, and apply the conventions established in Phase 1 & 2 across the rest of the surface.

## Item table

| # | Item | Resource | Status | Owner | Notes |
|---|---|---|---|---|---|
| 3.1 | Delete unfiltered `GET /api/students` | Students | Done | | Removed — callers migrate to `GET /api/students/search` |
| 3.2 | Delete unfiltered `GET /api/staff` | Staff | Done | | Removed — callers migrate to `GET /api/staff/search` |
| 3.3 | Bulk permission assignment | Permissions | Done | | `POST /api/permissions/assignment/batch` — all-or-nothing |
| 3.4 | Bulk permission revocation | Permissions | Done | | `PUT /api/permissions/assignment/batch` — same envelope, supports add/remove via `UpdatePermissionAssignmentRequest` |
| 3.5 | Bulk upsert student profile records | StudentProfileRecords | Done | | `PUT /api/students/{id}/profile-records/batch` |
| 3.6 | Bulk verify student profile records | StudentProfileRecords | Done | | `POST /api/students/{id}/profile-records/verify` |
| 3.7 | Bulk set status — Students | Students | Done | | `POST /api/students/status` `{ ids, isActive }` (explicit value, idempotent) |
| 3.8 | Bulk set status — Staff | Staff | Done | | `POST /api/staff/status` `{ ids, isActive }` |
| 3.9 | Bulk soft-delete on Students/Staff/Courses/Plans | Multiple | Done | | `POST /{resource}/delete` per resource |
| 3.10 | Adopt shared `PagedQueryRequest` base across existing query DTOs | Cross-cutting | Done | | StudentQueryRequest, StaffQueryRequest, GetRolesRequest, StudentServiceRequestListQuery |
| 3.11 | Decide on soft-delete restore endpoints | Cross-cutting | Done | | **Decision: Option A** — see below |

## Per-item context (what landed)

### 3.1 / 3.2 Unfiltered list endpoints removed
- Removed the `GET` action from [StudentsController.cs](../../src/1.API/CapitalUniversity.API/Controllers/StudentsController.cs) and [StaffController.cs](../../src/1.API/CapitalUniversity.API/Controllers/StaffController.cs).
- Service `GetAllAsync` retained for internal/test paths — only the controller surface is gone.
- **Frontend impact:** `frontend/src/core/services/studentService.js` and `staffService.js` and `frontend/src/modules/users/services/userService.js` call `/api/students` and `/api/staff` directly. They will receive 404. Migrate them to `/search` which already accepts the same params and returns `PagedResult<>`.

### 3.3 / 3.4 Bulk permission assignment / revocation
- `POST /api/permissions/assignment/batch` body `{ assignments: CreatePermissionAssignmentRequest[] }` — bulk create.
- `PUT /api/permissions/assignment/batch` body `{ updates: UpdatePermissionAssignmentRequest[] }` — bulk update / revoke (the existing `UpdatePermissionAssignmentRequest` already carries `RolesToRemove` and `PermissionsToRemove` so revoke and add ship together).
- **All-or-nothing:** wrapped in a single `IDbContextTransaction` (relational only — InMemory provider falls back to direct calls, matching existing patterns). Any per-row failure rolls back the entire batch.
- Service: `IPermissionManagementService.BatchCreateAssignmentsAsync` and `BatchUpdateAssignmentsAsync`.
- Each batch item routes through the existing single-row methods so manifest synchronization, scope validation, and audit logging stay identical.

### 3.5 / 3.6 Profile records bulk
- `PUT /api/students/{studentId}/profile-records/batch` body `{ records: UpsertStudentProfileRecordRequest[] }` — bulk upsert.
- `POST /api/students/{studentId}/profile-records/verify` body `{ recordIds: Guid[], verifiedBy: Guid }` — bulk verify.
- Service: `IStudentProfileService.BatchUpsertAsync` and `BatchVerifyAsync`. Both route through the single-row paths so scope + validation + cache invalidation + re-verification clear stay identical.
- Per-row commits with `BulkActionResult` failure map.
- Upsert failures use a synthetic per-index id (no real entity id exists pre-upsert) so callers can correlate failures with input rows.

### 3.7 / 3.8 Bulk set status
- `POST /api/students/status` and `POST /api/staff/status` body `{ ids: Guid[], isActive: bool }`.
- **Idempotent** at the batch level via explicit value semantics (not a flip): replaying lands on the same target state.
- Sidesteps the deferred Phase 0 item 0.5 — the per-row `PATCH /toggle-status` endpoint stays untouched. Adding a single-row idempotent `PUT /status` is left as a follow-up.
- Service: `IStudentService.SetStatusManyAsync` and `IStaffService.SetStatusManyAsync` — load entity, set `IsActive`, update; already-at-target rows recorded as `succeeded` without an UPDATE.

### 3.9 Bulk soft-delete
Per-resource endpoints (per-resource keeps audit/permission clean):
- `POST /api/students/delete` — `IStudentService.DeleteManyAsync`
- `POST /api/staff/delete` — `IStaffService.DeleteManyAsync`
- `POST /api/courses/delete` — `ICourseService.DeleteManyAsync` (routes through single-row Delete so `EnsureMutable` runs)
- `POST /api/academic-plans/delete` — `IAcademicPlanService.DeleteManyAsync` (scope check + EnsureMutable per row)

All use the shared bulk envelope. Failure codes: `NotFound`, `Conflict` (record closed).

### 3.10 Shared `PagedQueryRequest` base adopted
- [StudentQueryRequest.cs](../../src/2.Core/CapitalUniversity.Core.Abstractions/Students/DTOs/StudentQueryRequest.cs) — now inherits `PagedQueryRequest`. Historical default `PageSize=10` preserved via constructor.
- [StaffQueryRequest.cs](../../src/2.Core/CapitalUniversity.Core.Abstractions/StaffManagement/DTOs/StaffQueryRequest.cs) — same shape.
- `GetRolesRequest` — already migrated in Phase 2 work.
- [StudentServiceRequestListQuery](../../src/4.Modules/CapitalUniversity.Module.StudentServices.Abstractions/StudentServices/DTOs/StudentServiceRequestDtos.cs#L136) — now inherits the base; keeps its resource-specific `SortBy`/`SortAscending` pair because the service's pending-vs-non-pending default flip depends on the simpler shape. The base `Sort` string is available for forward compatibility.
- Non-breaking: defaults preserved everywhere, server-side cap (`MaxPageSize=100`) silently clamps oversized requests.

### 3.11 Soft-delete restore decision

**Decision: Option A — intentional.** No API surface for restoring soft-deleted entities. Rationale:

- Six entities use `ISoftDeletable` (Invoice, PaymentTransaction, CourseOffering, StudentService, StudentServiceRequest, StudentProfileRecord). Each has a distinct "what does undo mean here" answer — invoice unde-cancellation has financial implications, course-offering undelete may revive an enrollment hole, etc.
- Undelete that survives audit is rare and high-stakes; gating it through ops (DBA) preserves the audit chain.
- Bulk-delete (item 3.9) is now exposed, raising the surface area for mistakes — but a restore endpoint would invite "delete it; we can always restore" thinking that erodes the safety property soft-delete is meant to provide.
- If a specific entity later proves to need per-row business undo (e.g. invoice "we cancelled by mistake before it was paid"), add a narrow `POST /{resource}/{id}/restore` for that resource only — not a generic restore endpoint. Document the use case before adding.

This decision is reviewed when an entity hits the "we keep needing DBAs to undelete this" threshold.

## Notes / follow-ups discovered while implementing

- **Pre-existing bug in `InvoiceService.CancelAsync`:** doesn't reject `Refunded` invoices — they fall through and get re-set to `Cancelled`. Out of scope for Phase 3 but worth flagging for a follow-up.
- **Frontend migration for 3.1/3.2:** see context above. Frontend services file paths included.
- **0.5 still open:** single-row idempotent `PUT /status` for Students/Staff is not in scope of any Phase. The bulk endpoints (3.7/3.8) are explicit-value and therefore idempotent regardless.
- **Restore policy (3.11):** recorded as Option A. Revisit if an entity needs business-undo.

## Open questions
- _Add as you go._
