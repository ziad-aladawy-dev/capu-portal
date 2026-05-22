# Backend Requirements & Issues Discovered During Frontend Audit

Items in this file are observations, missing endpoints, malformed responses, CORS
issues, or auth gaps discovered while wiring the frontend. **No backend code has
been modified** — this is a structured request to the backend team.

---

## Phase 1 — Discovery (audit only)

No backend changes were required during this read-only mapping phase. The audit
inventory of the API surface is captured in `frontend-changes.md`.

---

## Phase 3 — Frontend Implementation (issues surfaced while wiring)

The following observations came up while building the seven new pages. None
blocked Phase 3, but each represents either a documented backend gap or an
ergonomic improvement that would noticeably improve the frontend experience.

### 1. Invoices — no "list invoices" endpoint (only `by-student`)

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Context:** `frontend/src/modules/invoices/pages/InvoicesPage.jsx` — the
  Invoices page can only list invoices once a student is selected, because
  `InvoicesController` only exposes `GET /api/invoices/{id}` and
  `GET /api/invoices/by-student/{studentId}`. There is no
  cross-student list endpoint (e.g. for a finance officer browsing all unpaid
  invoices).
- **Required Action:** Add a paginated `GET /api/invoices` endpoint with
  filters (`status`, `dueBefore`, `currency`, `studentId?`) returning a
  `PaginatedResult<InvoiceResponse>` so the page can render a real finance
  dashboard instead of forcing a per-student drill-down.

### 2. Payment transactions — no list-by-status / global ledger endpoint

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Context:** Frontend invoice details page already lists transactions
  per-invoice (`GET /api/payments/invoices/{id}/transactions`), but there is no
  way to surface a finance-wide payment journal (e.g. "all successful payments
  this month").
- **Required Action:** Add a paginated `GET /api/payments/transactions`
  endpoint with filters (`status`, `provider`, `from`, `to`,
  `invoiceId?`) for finance officers' reporting needs.

### 3. Notifications — no "mark all read" endpoint

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Missing Endpoint
- **Context:** `frontend/src/modules/notifications/pages/NotificationsPage.jsx`
  implements "Mark all read" by issuing one `PUT /api/Notifications/{id}/read`
  per unread item. With many unread notifications this becomes a fan-out.
- **Required Action:** Add `PUT /api/Notifications/read-all` that flips every
  unread notification for the caller to read in a single call.

### 4. Notifications controller route casing

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Inconsistent Naming
- **Context:** Every other controller is mounted under the lowercase plural
  resource path (`/api/students`, `/api/invoices`, `/api/courses`, etc.),
  but `NotificationsController` uses `[Route("api/[controller]")]` which
  produces `/api/Notifications` (Pascal-cased). The frontend service uses the
  Pascal form to match the actual route. ASP.NET routing is case-insensitive by
  default so it works either way, but the inconsistency stands out in network
  panels and tests.
- **Required Action:** Either change the route to `[Route("api/notifications")]`
  for consistency, or leave a comment on `NotificationsController` explaining
  why it differs. Frontend will follow whichever form is chosen.

### 5. Student profile records — no audit-friendly verifier resolution

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** UX/Bad Data Shape
- **Context:**
  `frontend/src/modules/studentProfileRecords/pages/StudentProfileRecordsPage.jsx`
  shows verification status (`Verified` / `Unverified`), but the
  `StudentProfileRecordResponse.VerifiedBy` is a raw GUID. There is no way to
  display *who* verified a record without a second lookup against the staff
  endpoint, and no batch endpoint to resolve user names.
- **Required Action:** Either embed a `VerifiedByName` (and optionally
  `VerifiedAtUtc`) in the response DTO, or expose `GET /api/staff?ids=…` for
  batch resolution. The first option is cheaper and avoids an N+1 on the
  frontend.

### 6. Authorization permission tree — pagination + assignment context

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Bad Data Shape (mild)
- **Context:** `GET /api/authorization/permissions/tree` returns the complete
  tree at once. For the full system this is fine, but the same endpoint serves
  the per-role variant (`GET /api/authorization/roles/{id}/permissions`) where
  `PermissionActionDto.IsAssigned` is only populated on the role-context call.
  The two shapes are merged but only the role variant actually populates
  `IsAssigned`. The full-tree call returns `IsAssigned: null` for every node,
  which the frontend correctly renders as "no chip state".
- **Required Action:** No code change required, but document this contract on
  the controller so future consumers don't assume `IsAssigned` is reliable on
  the full-tree call. Bonus: a `GET /api/authorization/users/{id}/permissions`
  variant for the staff details panel would let the "Roles & Permissions" tab
  in `UserDetails.jsx` switch from the current free-text resource list to a
  proper tree picker.

### 7. Bulk import / Excel import endpoints are unused

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Documentation / Scope question
- **Context:** `StudentsController.BulkImport`,
  `StudentsController.ImportExcel`, and the staff counterparts are exposed but
  nothing in the frontend invokes them. The current `UserManagement` page does
  a *client-side* CSV export instead of calling the backend `export/csv` route.
- **Required Action:** Either remove the unused endpoints, or confirm they are
  intentional and we should build the import UI next. Logged here so it does
  not get lost.

### 8. Auth — `change-password` / `refresh` have no UI yet

- **Date/Iteration:** 2026-05-19 (Phase 3)
- **Issue Type:** Documentation / Scope question
- **Context:** `POST /api/auth/refresh` and `POST /api/auth/change-password`
  exist on the backend but have no frontend hook today. Out of scope for this
  pass.
- **Required Action:** No action required. Logged for a follow-up "Account
  Settings" page.

---
