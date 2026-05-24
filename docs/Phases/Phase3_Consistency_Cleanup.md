# Phase 3 — Consistency and cleanup

**Tier:** P3
**Goal:** retire duplicate endpoints, fill out the B-tier bulk operations, and apply the conventions established in Phase 1 & 2 across the rest of the surface. Lower urgency, but each item is small.

## Item table

| # | Item | Resource | Status | Owner | Notes |
|---|---|---|---|---|---|
| 3.1 | Delete or alias `GET /api/students` (unfiltered list) | Students | Proposed | | Redirect to `/search` |
| 3.2 | Delete or alias `GET /api/staff` (unfiltered list) | Staff | Proposed | | Symmetric to 3.1 |
| 3.3 | Bulk permission assignment | Permissions | Proposed | | Tier B1 — module onboarding |
| 3.4 | Bulk permission revocation | Permissions | Proposed | | Tier B2 |
| 3.5 | Bulk upsert student profile records | StudentProfileRecords | Proposed | | Tier B3 — onboarding |
| 3.6 | Bulk verify student profile records | StudentProfileRecords | Proposed | | Tier B4 — auditor flow |
| 3.7 | Bulk set status — Students | Students | Proposed | | Tier B5 — depends on 0.5 |
| 3.8 | Bulk set status — Staff | Staff | Proposed | | Tier B5 — depends on 0.5 |
| 3.9 | Bulk soft-delete on Students/Staff/Courses/Plans | Multiple | Proposed | | Tier B6 |
| 3.10 | Adopt shared `PagedQueryRequest` base across existing query DTOs | Cross-cutting | Proposed | | Locked in by Phase 2 design |
| 3.11 | Decide on soft-delete restore endpoints (or document non-restorable as policy) | Cross-cutting | Proposed | | See audit §5 |

## Per-item context

### 3.1 / 3.2 Drop unfiltered list endpoints
`GET /api/students` and `GET /api/staff` (no params) duplicate the `/search` variant but skip pagination. Removing them prevents accidental "fetch entire table" calls. Migrate any caller to `/search` with default page.

### 3.3 / 3.4 Bulk permission assignment
**Use case:** onboarding a new module — seed dozens of role-permission rows in one transaction.
**Proposed:** `POST /api/permissions/assignment/batch` with `{ assignments: [...], mode: "create"|"delete" }` or two endpoints (create/delete).
**Semantics:** whole-or-nothing recommended — partial seeding can leak permissions or create roles without the perms they need.

### 3.5 / 3.6 Bulk upsert/verify student profile records
**3.5 — upsert:** `PUT /api/students/{studentId}/profile-records/batch` `{ records: [...] }` — registrar onboards a student with tens of profile fields at once. Same scope check fires once.
**3.6 — verify:** `POST /api/students/{studentId}/profile-records/verify` `{ recordIds: Guid[] }` — auditor approves a sheaf of records.

### 3.7 / 3.8 Bulk status set
**Depends on:** [Phase 0 item 0.5](Phase0_Security_And_Correctness.md) (replace `toggle-status` with idempotent `PUT .../status`).
**Proposed:** `POST /api/students/status` body `{ ids: Guid[], isActive: bool }`. Same for staff.
**Use case:** term-rollover deactivation, graduation, mass reactivation.

### 3.9 Bulk soft-delete
Soft-delete already wired on most relevant entities. The bulk endpoint is just a loop with scope check per id and cache invalidation. Use the shared bulk envelope.
**Consider:** do you want a single generic endpoint or one per resource? Per-resource keeps audit/permission clean; generic is DRYer. Recommendation: per-resource.

### 3.10 Adopt shared `PagedQueryRequest` base
Migrate existing `StudentQueryRequest`, `StaffQueryRequest`, `GetRolesRequest`, `StudentServiceRequestListQuery` to inherit the base agreed in [Phase 2 cross-cutting design](Phase2_HighValue_Search_Filter.md#cross-cutting-design). Add the sort encoding and cap to the four. Non-breaking if defaults preserved.

### 3.11 Soft-delete restore
Audit observation: no entity exposes restore. Decide:
- **Option A:** intentional — undelete is operational only (DB-level), not API-exposed.
- **Option B:** add `POST /{resource}/{id}/restore` for entities where business undo is real (e.g. invoice cancelled in error, plan retired by accident).

Document the decision either way.

## Open questions
- _Add as you go._
