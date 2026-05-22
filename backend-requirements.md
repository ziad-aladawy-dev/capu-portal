# Backend Requirements & Issues

> **Purpose:** Document backend issues discovered during frontend audit that block or impair frontend integration.

---

## Issue 1: Missing `GET /api/auth/me` Endpoint

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Endpoint
- **Context:** Frontend `authService.getCurrentUser()` calls `GET /api/auth/me` to load the currently authenticated user on page refresh. This endpoint does not exist in `AuthController.cs`.
- **Required Action:** Add a `[HttpGet("me")]` endpoint to `AuthController` that returns the current user's info (user details, permissions, authorized scopes) based on the JWT claims. The expected response shape should mirror `LoginResponseDto`.
- **Files affected:** `src/1.API/CapitalUniversity.API/Controllers/AuthController.cs`

---

## Issue 2: Role Response Missing `createdAt` Field

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Bad Data Shape
- **Context:** Frontend `RolesPage.jsx` displays `role.createdAt` for each role. The backend `RoleResponse` DTO only has `Id`, `Name`, `IsSystemRole`.
- **Required Action:** Add `CreatedAt` (DateTime) and optionally `CreatedBy` fields to the `RoleResponse` DTO in `GetRoleByIdQuery.cs` and populate them from the database.
- **Files affected:** `src/2.Core/CapitalUniversity.Core.Infrastructure/Services/Roles/Queries/GetRoleByIdQuery.cs`

---

## Issue 3: Structure Lookup Route Mismatch

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Bad Data Shape / Missing Endpoint
- **Context:** Frontend `userService.getPrograms(facultyId)` attempts to call `/api/structure/lookups/faculties/{facultyId}/programs` which does not exist. The correct existing route is `/api/structure/lookups/{parentId}/children/Program`. However, a dedicated `/faculties/{id}/programs` convenience route would simplify frontend code.
- **Required Action (Option A):** Add `GET /api/structure/lookups/faculties/{facultyId}/programs` to `StructureLookupController` as a convenience shortcut.
- **Required Action (Option B):** Frontend will adapt to use `/{parentId}/children/Program` (already fixed in frontend).
- **Resolution:** Frontend has been updated to use the correct route `{parentId}/children/Program`. No backend changes strictly required.

---

## Issue 4: Frontend Core Services URL Mismatch

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Inconsistency
- **Context:** Core frontend services (`studentService.js`, `staffService.js`, etc.) were using paths like `/students` instead of `/api/students`. The backend Controllers` are at the `[Route("api/students")]` level.
- **Required Action:** Either add a path prefix middleware that routes `/students` → `/api/students`, or this is a frontend-only fix (already resolved in the frontend audit).
- **Resolution:** Frontend has been updated. No backend changes required.

---

## Issue 5: Dashboard Static Data

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Frontend Integration
- **Context:** The AdminDashboard (`StatsCards`, `RecentActivities`, `QuickActions`) currently displays hardcoded mock/static data. To make the dashboard dynamic, backend endpoints for a dashboard summary would be helpful.
- **Required Action (Optional):** Consider adding a dedicated `GET /api/dashboard/stats` or `GET /api/dashboard/summary` endpoint that returns aggregate counts, recent user activities, etc. This would unify the multiple independent statistics calls currently made by the frontend.
- **Current Workaround:** Frontend uses existing per-entity statistics (students, staff) individually.

---

## Issue 6: Bulk Import Endpoints Lack Frontend UI

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Frontend
- **Context:** Backend has `POST /api/students/bulk-import`, `POST /api/students/import-excel`, `POST /api/staff/bulk-import`, and `POST /api/staff/import-excel` endpoints but no frontend UI currently exists to consume them. The import-excel endpoints accept `IFormFile` (multipart form data).
- **Required Action:** This is a frontend-only concern. No backend changes required. Frontend bulk import UI is planned for a future iteration.

---

## Issue 7: No Notification Count Unread Endpoint

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Enhancement
- **Context:** For a notification bell badge in the navbar, a lightweight endpoint to get the unread count (as an integer) would be more efficient than fetching the full list.
- **Required Action (Optional):** Add `GET /api/notifications/unread/count` returning `{ count: number }`.
- **Current Workaround:** Frontend fetches `GET /api/notifications/unread` (full list) and counts client-side.

---

## Issue 8: Semester `Order` Field Used Ambiguously

- **Date/Iteration:** Phase 2 - Analysis
- **Issue Type:** Documentation/Clarification
- **Context:** The `CreateSemesterRequest` has an `Order` field. It's unclear if `Order` refers to the semester number within the academic year (1, 2, 3) or a custom display order.
- **Required Action:** Clarify in Swagger/docs that `Order` represents the semester's sequence number within the academic year (e.g., 1 = Fall, 2 = Spring, 3 = Summer).

---

## Issue 9: Academic Year/Semester Missing Dedicated Frontend Response

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Enhancement
- **Context:** The frontend now has a full Academic Calendar management page. The backend endpoints for CRUD operations on both academic years and semesters are present and functional. No changes required.
- **Resolution:** All verified as working.

---

> **Legend:**
> - ❌ = Missing / Blocking
> - ⚠️ = Impairing functionality
> - ✅ = Working correctly
> - *Italic* = Optional enhancement
