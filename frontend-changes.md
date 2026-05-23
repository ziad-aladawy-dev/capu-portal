# Frontend Changelog — Audit, Wiring & Expansion

This document tracks every audit, integration check, and feature expansion performed
against the React frontend of the `capu-portal` repository. It is appended to at the
end of every phase / iteration.

---

## Phase 1 — Repository Discovery & Audit

### Tech Stack & Conventions

- **Framework:** React 19 + Vite 8 (no TypeScript — pure JSX).
- **Routing:** `react-router-dom@7` (`<BrowserRouter>` in `frontend/src/main.jsx`,
  central registry in `frontend/src/core/router/AppRouter.jsx` + `routeRegistry.js`).
- **State:** React Context (`AuthContext`, `PermissionContext`, `DomainContext`,
  `AcademicContext`, `StickySelectionContext`) plus per-page `useState` / custom hooks.
- **API client:** `frontend/src/core/api/apiClient.js` — a small `fetch` wrapper that
  exposes `api.get/post/put/patch/delete`, an `ApiError` class, JWT token persistence
  in `localStorage`, and a global `setOnUnauthorized` handler used by `AuthContext`
  to log the user out on 401.
- **Auth:** JWT bearer issued by `POST /api/auth/login`; user/permissions hydrated by
  `GET /api/auth/me`. `RouteGuard` + `usePermission().can()` gate every protected
  route by manifest permission.
- **Styling:** Vanilla CSS per module (no Tailwind, no CSS-in-JS). Each page or
  module owns a scoped CSS file under `modules/<x>/styles/*.css` or
  `core/styles/*.css`. Brand tokens: navy `#1a1f5e`, gold `#c9a84c`, fonts
  `"DM Sans"` and `"Outfit"`.
- **Modular manifests:** Every module under `modules/<x>/` ships a `manifest.json`
  declaring `routes`, `menuItems` and `permissions.resources`. Manifests are
  aggregated by `core/manifests/manifestLoader.js`, routes are materialised in
  `core/router/routeRegistry.js` (`COMPONENT_MAP`), and the sidebar is built from
  the menu items via `core/navigation/menuAggregator.js`.
- **Icons:** `lucide-react`.

### Folder Map (frontend/src)

```
core/
  api/apiClient.js              # fetch wrapper + ApiError
  auth/                         # AuthContext, PermissionContext, RouteGuard, authService, login pages
  contexts/                     # DomainContext, AcademicContext, StickySelectionContext
  i18n/
  layouts/DashboardLayout.jsx   # Navbar + Sidebar + SecondarySidebar shell
  manifests/manifestLoader.js   # imports every modules/*/manifest.json
  navigation/                   # navbar, sidebar, secondarySidebar, menuAggregator
  router/                       # AppRouter, routeRegistry (COMPONENT_MAP)
  services/                     # academicService, permissionService, staffService, structureService, studentService
  styles/                       # navbar.css, sidebar.css
modules/
  admin/      # AdminDashboard + components/data/styles
  landing/    # public landing
  permissions/# PermissionsPage, RolesPage
  staff/      # StaffDirectory (wraps UserManagement)
  students/   # StudentDirectory (wraps UserManagement)
  university/ # UniversityStructurePage + TreeNode
  users/      # UserManagement, AddStudent, EditStudent, AddStaff, EditStaff, UserDetails
              # + components, hooks/useUsers.js, services/userService.js, styles, utils
```

### Existing Core Service Wiring

| Service file                  | Backend surface covered                                       |
|------------------------------|---------------------------------------------------------------|
| `core/services/academicService.js` | `/api/academic-years` (CRUD + semesters), `/api/semesters/{id|current}` |
| `core/services/staffService.js`    | `/api/staff` (CRUD, search, stats, csv/excel, toggle-status)  |
| `core/services/studentService.js`  | `/api/students` (CRUD, search, stats, csv/excel, toggle-status) |
| `core/services/permissionService.js` | `/api/permissions`, `/api/permissions/assignment`, `/api/roles` |
| `core/services/structureService.js` | `/api/structure/lookups/*`, `/api/university-structure/*`      |
| `core/auth/authService.js`         | `/api/auth/login`, `/auth/logout`, `/auth/me`                  |

### Backend Surface Inventory (read-only scan)

Controllers under `src/1.API/CapitalUniversity.API/Controllers/`:

1. `AuthController`               → `/api/auth/{login,logout,refresh,change-password}`
2. `AcademicYearsController`      → `/api/academic-years` (CRUD + semesters + resolve)
3. `SemestersController`          → `/api/semesters/{id,current,resolve}` + CRUD
4. `StudentsController`           → `/api/students` (CRUD, search, stats, exports, bulk import, import excel)
5. `StaffController`              → `/api/staff` (same shape as students)
6. `PermissionsController`        → `/api/permissions` + `/api/permissions/assignment`
7. `RolesController`              → `/api/roles` (CRUD)
8. `AuthorizationController`      → `/api/authorization/permissions/tree`, `/api/authorization/roles/{id}/permissions`
9. `UniversityStructureController`→ `/api/university-structure/*`
10. `StructureLookupController`   → `/api/structure/lookups/*`
11. `CoursesController`           → `/api/courses` (CRUD)
12. `AcademicPlansController`     → `/api/academic-plans` (CRUD + courses)
13. `InvoicesController`          → `/api/invoices` (get, by-student, create, cancel)
14. `PaymentsController`          → `/api/payments/transactions` + `/api/payments/invoices/{id}/transactions`
15. `StudentProfileRecordsController` → `/api/students/{studentId}/profile-records/*`
16. `NotificationsController`     → `/api/Notifications`, `/unread`, `/{id}/read`

### Phase 1 Files

- **Files Created:** `frontend-changes.md`, `backend-requirements.md`
- **Files Modified:** _none_
- **Actions Taken:** Mapped the React app's manifest/routing/service architecture
  and enumerated the .NET API surface, identifying which backend endpoints are
  already wired in the frontend and which are orphaned. No source files touched.

---

## Phase 2 — Gap Analysis

### Cross-Reference: Backend Endpoint → Frontend Status

| Endpoint                                         | Wired? | Where                                       |
|--------------------------------------------------|:------:|---------------------------------------------|
| `POST /api/auth/login`                           | yes    | `authService.login`                         |
| `POST /api/auth/logout`                          | yes    | `authService.logout`                        |
| `POST /api/auth/refresh`                         | **no** | —                                           |
| `POST /api/auth/change-password`                 | **no** | —                                           |
| `GET /api/auth/me`                               | yes    | `authService.getCurrentUser`                |
| `GET/POST/PATCH/DELETE /api/academic-years`      | yes    | `academicService`                           |
| `POST /api/academic-years/resolve`               | **no** | —                                           |
| `GET /api/semesters/{id,current}` + CRUD         | yes    | `academicService`                           |
| `POST /api/semesters/resolve`                    | **no** | —                                           |
| `GET/POST/PUT/DELETE/PATCH /api/students`        | yes    | `studentService` + `userService`            |
| `POST /api/students/bulk-import`, `import-excel` | **no** | (exists in API, no UI hook)                 |
| `GET/POST/PUT/DELETE/PATCH /api/staff`           | yes    | `staffService` + `userService`              |
| `POST /api/staff/bulk-import`, `import-excel`    | **no** | —                                           |
| `GET /api/permissions`, `/assignment`            | yes    | `permissionService`                         |
| `POST/PUT /api/permissions[/assignment]`         | yes    | `permissionService`                         |
| `GET/POST/PUT/DELETE /api/roles`                 | yes    | `permissionService`                         |
| `GET /api/authorization/permissions/tree`        | **no** | —                                           |
| `GET /api/authorization/roles/{id}/permissions`  | **no** | —                                           |
| `GET /api/university-structure/*` + writes       | yes    | `structureService`                          |
| `GET /api/structure/lookups/*`                   | yes    | `structureService` (minus `systems`)        |
| `GET /api/structure/lookups/systems`             | **no** | —                                           |
| `GET/POST/PATCH/DELETE /api/courses`             | **no** | **ORPHAN — no UI**                          |
| `GET/POST/PATCH/DELETE /api/academic-plans`      | **no** | **ORPHAN — no UI**                          |
| `POST/DELETE /api/academic-plans/{id}/courses`   | **no** | **ORPHAN — no UI**                          |
| `GET/POST /api/invoices` + `/cancel`             | **no** | **ORPHAN — no UI**                          |
| `GET /api/invoices/by-student/{id}`              | **no** | **ORPHAN — no UI**                          |
| `POST /api/payments/transactions`                | **no** | **ORPHAN — no UI**                          |
| `GET /api/payments/invoices/{id}/transactions`   | **no** | **ORPHAN — no UI**                          |
| `GET/PUT/POST/DELETE /api/students/{id}/profile-records/*` | **no** | **ORPHAN — no UI**            |
| `GET /api/Notifications`, `/unread`, `/{id}/read`| **no** | **ORPHAN — no UI**                          |

### Critical Orphaned Endpoints (must be addressed)

These are core to a university portal's domain and have **no** frontend surface today:

1. **Course Catalog** — `/api/courses` (CRUD).
2. **Academic Plans** — `/api/academic-plans` + plan-courses (CRUD).
3. **Invoices** — `/api/invoices`, `by-student`, `cancel`.
4. **Payment Transactions** — `/api/payments/transactions` + per-invoice view.
5. **Student Profile Records** — `/api/students/{id}/profile-records/*` (military,
   vaccination, emergency contact, disability, housing, custom).
6. **Notifications inbox** — `/api/Notifications` + `unread` + `mark-as-read`.
7. **Authorization permission tree (per role)** — `/api/authorization/*` (used to
   replace the free-text resource entry currently in `PermissionsPage`/`UserDetails`).

### Lower-priority orphans (logged but deferred)

- `auth/refresh`, `auth/change-password` — there is no "Change Password" UI yet.
  Logged in `backend-requirements.md` only if frontend implementation surfaces
  any contract gap.
- `students/bulk-import`, `students/import-excel`, same for staff — UI currently
  shells out a client-side CSV; bulk import is a future feature.
- `academic-years/resolve`, `semesters/resolve` — used for scope coercion, not
  required by any current screen.
- `structure/lookups/systems` — not consumed by any current screen.

### Phase 2 Files

- **Files Created:** _none_
- **Files Modified:** `frontend-changes.md` (this section)
- **Actions Taken:** Built the endpoint-coverage matrix above and prioritised the
  seven critical orphans for Phase 3 implementation. Lower-priority deltas are
  recorded but deferred to keep this PR focused on what is observably missing
  from the user experience.

---

## Phase 3 — Frontend Implementation

Seven previously-orphaned API surfaces now have a complete React UI, wired into
the existing manifest/routing/permissions system. Each new module follows the
established convention: `modules/<name>/manifest.json` + `pages/` + `styles/`,
service repositories live under `core/services/`, and global icons / sidebar
categories are extended in `core/navigation`.

### Data layer (new core services)

- **`frontend/src/core/services/courseService.js`** — wires `/api/courses` (CRUD)
  and exports a `COURSE_CATEGORIES` enum mirror.
- **`frontend/src/core/services/academicPlanService.js`** — wires
  `/api/academic-plans`, `/by-structure/{id}`, and plan-course add/remove.
- **`frontend/src/core/services/invoiceService.js`** — wires `/api/invoices`,
  `/by-student/{id}`, `/cancel`, plus an `InvoiceStatus` mirror.
- **`frontend/src/core/services/paymentService.js`** — wires
  `/api/payments/transactions` and `/payments/invoices/{id}/transactions`, plus
  a `PaymentTransactionStatus` mirror.
- **`frontend/src/core/services/notificationService.js`** — wires
  `/api/Notifications` (all + unread + mark-read), plus a `NotificationType`
  mirror.
- **`frontend/src/core/services/studentProfileService.js`** — wires
  `/api/students/{id}/profile-records/*` (list, by-category, get, upsert,
  verify, delete), plus a `StudentProfileCategory` mirror.
- **`frontend/src/core/services/authorizationService.js`** — wires
  `/api/authorization/permissions/tree` and
  `/api/authorization/roles/{id}/permissions`.

Every service file mirrors backend DTOs precisely (Pascal-cased enum values
stay numeric ints; field names match controller-bound DTO properties), uses the
shared `core/api/apiClient.js` (so JWT, 401 handling and error shape are
consistent), and stays free of UI concerns (Separation of Concerns).

### New modules

| Route                                                | Module                        | Component                       | Permission                                  |
|------------------------------------------------------|-------------------------------|---------------------------------|---------------------------------------------|
| `/admin/courses`                                     | `courses`                     | `CoursesPage`                   | `courses.courses.view`                      |
| `/admin/academic-plans`                              | `academic-plans`              | `AcademicPlansPage`             | `courses.academic-plans.view`               |
| `/admin/invoices`                                    | `invoices`                    | `InvoicesPage`                  | `payments.invoices.view`                    |
| `/admin/invoices/:invoiceId`                         | `invoices`                    | `InvoiceDetailsPage`            | `payments.invoices.view`                    |
| `/admin/notifications`                               | `notifications`               | `NotificationsPage`             | _(any authenticated user)_                  |
| `/admin/students/:studentId/profile-records`         | `student-profile-records`     | `StudentProfileRecordsPage`     | `student-information.profile-records.view`  |
| `/admin/authorization`                               | `authorization`               | `PermissionTreePage`            | `permissions.permissions.view`              |

Every page handles the **three required render states**:
- **Loading:** scoped CSS spinner with a contextual label.
- **Error:** error banner with retry, or a full-card error state for catastrophic
  failures. Errors use the existing `ApiError` thrown by `apiClient`.
- **Success:** full content, with explicit empty states when the API returns an
  empty list.

### Files Created (Phase 3)

**Services (core)**
- `frontend/src/core/services/courseService.js`
- `frontend/src/core/services/academicPlanService.js`
- `frontend/src/core/services/invoiceService.js`
- `frontend/src/core/services/paymentService.js`
- `frontend/src/core/services/notificationService.js`
- `frontend/src/core/services/studentProfileService.js`
- `frontend/src/core/services/authorizationService.js`

**Courses module**
- `frontend/src/modules/courses/manifest.json`
- `frontend/src/modules/courses/pages/CoursesPage.jsx`
- `frontend/src/modules/courses/styles/courses.css`

**Academic Plans module**
- `frontend/src/modules/academicPlans/manifest.json`
- `frontend/src/modules/academicPlans/pages/AcademicPlansPage.jsx`
- `frontend/src/modules/academicPlans/styles/academicPlans.css`

**Invoices / Payments module**
- `frontend/src/modules/invoices/manifest.json`
- `frontend/src/modules/invoices/pages/InvoicesPage.jsx`
- `frontend/src/modules/invoices/pages/InvoiceDetailsPage.jsx`
- `frontend/src/modules/invoices/styles/invoices.css`

**Notifications module**
- `frontend/src/modules/notifications/manifest.json`
- `frontend/src/modules/notifications/pages/NotificationsPage.jsx`
- `frontend/src/modules/notifications/styles/notifications.css`

**Student Profile Records module**
- `frontend/src/modules/studentProfileRecords/manifest.json`
- `frontend/src/modules/studentProfileRecords/pages/StudentProfileRecordsPage.jsx`
- `frontend/src/modules/studentProfileRecords/styles/studentProfileRecords.css`

**Authorization (Permission Tree) module**
- `frontend/src/modules/authorization/manifest.json`
- `frontend/src/modules/authorization/pages/PermissionTreePage.jsx`
- `frontend/src/modules/authorization/styles/authorization.css`

### Files Modified (Phase 3)

- `frontend/src/core/manifests/manifestLoader.js` — registered the seven new
  module manifests so their routes + menu items + permissions are picked up
  by the existing `getAllRoutes` / `getGroupedMenuItems` pipeline.
- `frontend/src/core/router/routeRegistry.js` — added `CoursesPage`,
  `AcademicPlansPage`, `InvoicesPage`, `InvoiceDetailsPage`,
  `NotificationsPage`, `StudentProfileRecordsPage`, `PermissionTreePage` to
  `COMPONENT_MAP`. Also: route entries declared without a `permission` now skip
  the `RouteGuard` permission check (still require authentication) — needed so
  the Notifications page is reachable for any signed-in user.
- `frontend/src/core/auth/RouteGuard.jsx` — guards now short-circuit the
  `can(...)` check when `resource` is null/undefined, preserving the existing
  auth-only gate.
- `frontend/src/core/navigation/menuAggregator.js` — added `BookOpen`,
  `ClipboardList`, `Receipt`, `Bell`, `FolderTree`, `FileText` to `ICON_MAP` so
  the new manifest menu items resolve their icons.
- `frontend/src/core/navigation/sidebar/Sidebar.jsx` — added `Academic` and
  `Finance` category icons (`BookOpen`, `Receipt`).
- `frontend/src/modules/users/pages/UserDetails.jsx` — added a "Profile
  Records" action for student profiles linking to the new
  `/admin/students/:studentId/profile-records` page.

### Implementation Notes — engineering standards

- **Separation of Concerns:** every page imports only `core/services/*`
  modules; no `fetch` calls live in components. Domain enum mirrors are kept
  alongside the service that owns them.
- **DRY:** existing `RouteGuard`, `apiClient`, `getGroupedMenuItems`,
  `useAuth`, `usePermission`, and the established CSS design tokens (navy
  `#1a1f5e`, gold `#c9a84c`, Outfit/DM Sans/Space Mono fonts) are reused
  across every new page — no duplicate primitives.
- **State management:** every page has explicit loading, error, and success
  branches. Modals are local-state controlled, list refresh after mutation is
  always explicit.
- **Type / interface symmetry:** request bodies match backend DTO shapes
  exactly (`InvoiceResponse.totalAmount` decimal, enum values stay numeric
  ints, profile-record `dataJson` is sent as a stringified JSON document, the
  payment idempotency key field is required, etc.).
- **Design adherence:** every CSS file matches the conventions in
  `modules/permissions/styles/roles.css` and `modules/users/styles/users.css`
  — header band, gradient gold primary buttons, navy table header, badge pill
  patterns, modal overlay/header/body/footer scaffolding. New pages drop into
  the existing `DashboardLayout` with no visual seams.

### Phase 3 verification

- `npm run build` (Vite 8) — succeeds with no errors:
  `dist/assets/index-…js  1,020 kB │ gzip: 175 kB`, all 1861 modules transformed.
- Manifest aggregation: 13 modules total now visible to `getAllRoutes`.

---

---

## Phase 0/1 — Remediation (2026-05-23)

After the initial audit, critical and high-priority fixes were applied:

### P0 Critical Fixes
- Double `/api` prefix removed from `userService.js` and `universityStructureService.js`
- `ForgotPasswordModal.jsx` — added missing `authService` import
- `authService.js` — implemented `forgotPassword()` function

### P1 High Fixes
- Created `modules/academicYears/` — Full CRUD page with semester sub-manager, manifest & route registration

## Phase 2 — Missing Flows (2026-05-23)

- **Student Portal** — 6 pages: Dashboard, Profile, Courses, CourseRegistration, Grades, Schedule
- **Role Permission Assignment** — Modal for toggling permissions per role
- **Bulk Import UI** — Drag-and-drop Excel/CSV import for students/staff
- **Token Refresh** — Auto-refresh logic in apiClient with request queue
- **Global Search** — Navbar search wired to `/admin/users?search=...`
- **Landing Page Footer** — 4-column footer with contact info + social links

## Phase 3 — Polish (2026-05-23)

- **Toast system** — `ToastProvider` + `useToast` replacing 13 `alert()` calls
- **Loading skeletons** — `Skeleton`, `SkeletonTable`, `SkeletonStats` components
- **Error boundary** — `ErrorBoundary` component wrapping the app

## Phase 4 — Quality & Performance (2026-05-23)

### Lazy Loading / Code Splitting
All route components in `routeRegistry.js` and public routes in `AppRouter.jsx` switched to `React.lazy()` + `Suspense` for on-demand loading.

### Frontend Tests
- Installed `vitest` + `@testing-library/react` + `jsdom`
- `src/test/basic.test.jsx` — 4 tests (authService login/forgotPassword, permissionService, ToastProvider)
- `npm test` — all passing

### Module Count: 16 modules now registered in manifest loader.
