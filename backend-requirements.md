# Backend Requirements & Issues Discovered During Frontend Audit

Items in this file are observations, missing endpoints, malformed responses, CORS
issues, or auth gaps discovered while wiring the frontend.

---

## Phase 2 — Gap Analysis

### Issue 1: Missing `GET /api/auth/me` Endpoint

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Endpoint
- **Context:** Frontend `authService.getCurrentUser()` calls `GET /api/auth/me` to load the currently authenticated user on page refresh. This endpoint does not exist in `AuthController.cs`.
- **Required Action:** Add a `[HttpGet("me")]` endpoint to `AuthController` that returns the current user's info (user details, permissions, authorized scopes) based on the JWT claims.
- **Files affected:** `src/1.API/CapitalUniversity.API/Controllers/AuthController.cs`

### Issue 2: Role Response Missing `createdAt` Field

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Bad Data Shape
- **Context:** Frontend `RolesPage.jsx` displays `role.createdAt` for each role. The backend `RoleResponse` DTO only has `Id`, `Name`, `IsSystemRole`.
- **Required Action:** Add `CreatedAt` (DateTime) and optionally `CreatedBy` fields to the `RoleResponse` DTO.
- **Files affected:** `src/2.Core/CapitalUniversity.Core.Infrastructure/Services/Roles/Queries/GetRoleByIdQuery.cs`

### Issue 3: Structure Lookup Route Mismatch

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Bad Data Shape / Missing Endpoint
- **Context:** Frontend `userService.getPrograms(facultyId)` attempted to call `/api/structure/lookups/faculties/{facultyId}/programs` which does not exist.
- **Resolution:** Frontend updated to use `/{parentId}/children/Program`. No backend changes required.

### Issue 4: Frontend Core Services URL Mismatch

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Inconsistency
- **Context:** Core frontend services used paths like `/students` instead of `/api/students`.
- **Resolution:** Frontend updated. No backend changes required.

### Issue 5: Dashboard Static Data

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Frontend Integration
- **Context:** AdminDashboard displays hardcoded mock/static data.
- **Required Action (Optional):** Consider adding `GET /api/dashboard/stats` or similar endpoint.

### Issue 6: Bulk Import Endpoints Lack Frontend UI

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Missing Frontend
- **Context:** Backend has bulk-import endpoints but no frontend UI exists.
- **Required Action:** Frontend-only concern. Planned for future iteration.

### Issue 7: No Notification Count Unread Endpoint

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Enhancement
- **Required Action (Optional):** Add `GET /api/notifications/unread/count`.

### Issue 8: Semester `Order` Field Used Ambiguously

- **Date/Iteration:** Phase 2 - Analysis
- **Issue Type:** Documentation/Clarification
- **Required Action:** Clarify in Swagger/docs.

### Issue 9: Academic Year/Semester Missing Dedicated Frontend Response

- **Date/Iteration:** Phase 2 - Gap Analysis
- **Issue Type:** Enhancement
- **Resolution:** All verified as working with the new Academic Calendar page.

---

## Phase 3 — Frontend Implementation (issues surfaced while wiring)

### 1. Invoices — no "list invoices" endpoint (only `by-student`)

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Required Action:** Add paginated `GET /api/invoices` endpoint with filters.

### 2. Payment transactions — no list-by-status / global ledger endpoint

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Required Action:** Add paginated `GET /api/payments/transactions` endpoint.

### 3. Notifications — no "mark all read" endpoint

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Required Action:** Add `PUT /api/Notifications/read-all`.

### 4. Notifications controller route casing

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Inconsistent Naming
- **Required Action:** Align route casing with other controllers.

### 5. Student profile records — no audit-friendly verifier resolution

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** UX/Bad Data Shape
- **Required Action:** Embed `VerifiedByName` in response DTO or expose batch staff resolution.

### 6. Authorization permission tree — pagination + assignment context

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Bad Data Shape (mild)
- **Required Action:** Document contract; consider user-specific permission variant.

### 7. Bulk import / Excel import endpoints are unused

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Documentation / Scope question
- **Context:** Endpoints exist but no frontend invokes them.

### 8. Auth — `change-password` / `refresh` have no UI yet

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Documentation / Scope question
- **Required Action:** Logged for follow-up "Account Settings" page.
