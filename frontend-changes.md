# Frontend Changelog — capu-portal

This document tracks every audit, integration check, and feature expansion performed against the React frontend.

---

## Phase 1 — Repository Discovery & Audit

### Tech Stack & Conventions
- **Framework:** React 19 + Vite 8 (no TypeScript — pure JSX).
- **Routing:** `react-router-dom@7` (`<BrowserRouter>` in `main.jsx`, central registry in `AppRouter.jsx` + `routeRegistry.js`).
- **State:** React Context (`AuthContext`, `PermissionContext`, `DomainContext`, `AcademicContext`, `StickySelectionContext`) plus per-page `useState` / custom hooks.
- **API client:** `core/api/apiClient.js` — Axios wrapper with JWT interceptor.
- **Auth:** JWT bearer; `RouteGuard` + `usePermission().can()` gate every protected route.
- **Styling:** Vanilla CSS per module (navy `#1a1f5e`, gold `#c9a84c`, "DM Sans" / "Outfit" fonts).
- **Icons:** `lucide-react`.

### Module Inventory
| Module | ID | Pages | Status |
|--------|----|-------|--------|
| Landing | landing | LandingPage | Complete |
| Admin | admin | AdminDashboard | Connected (static data) |
| Users | users | UserManagement, AddStudent/Staff, EditStudent/Staff, UserDetails | Connected to backend |
| Students | students | StudentDirectory | Connected |
| Staff | staff | StaffDirectory | Connected |
| University | university | UniversityStructurePage | Full CRUD connected |
| Permissions | permissions | PermissionsPage, RolesPage | Connected |

### Initial Audit Findings
1. Core services missing `/api` prefix (e.g., `/students` instead of `/api/students`) — critical bug.
2. Dashboard displays static/mock data.
3. Backend `GET /api/auth/me` endpoint missing but frontend calls it.

---

## Phase 2 — Gap Analysis

### Key Gaps Identified
- Missing `GET /api/auth/me` endpoint
- Role response missing `createdAt` field
- 7 critical orphaned endpoints (courses, academic plans, invoices, payments, notifications, student profile records, authorization tree)
- Core service URL mismatch (missing `/api` prefix)
- Dashboard uses static data

---

## Phase 3 — Frontend Implementation

### New Services
- `courseService.js`, `academicPlanService.js`, `invoiceService.js`, `paymentService.js`, `notificationService.js`, `studentProfileService.js`, `authorizationService.js`

### New Modules
| Route | Module | Component |
|-------|--------|-----------|
| `/admin/courses` | courses | CoursesPage |
| `/admin/academic-plans` | academicPlans | AcademicPlansPage |
| `/admin/invoices` | invoices | InvoicesPage |
| `/admin/invoices/:invoiceId` | invoices | InvoiceDetailsPage |
| `/admin/notifications` | notifications | NotificationsPage |
| `/admin/students/:studentId/profile-records` | studentProfileRecords | StudentProfileRecordsPage |
| `/admin/authorization` | authorization | PermissionTreePage |
| `/admin/academic-calendar` | academic | AcademicCalendarPage |

### Files Modified
- `manifestLoader.js` — registered new module manifests
- `routeRegistry.js` — added new page components
- `RouteGuard.jsx` — short-circuits `can()` when `resource` is null
- `menuAggregator.js` — added new icons
- `Sidebar.jsx` — added Academic/Finance categories
- `UserDetails.jsx` — added Profile Records action for students
- Core services — all updated with correct `/api` prefix

### Bugs Fixed
| Bug | Severity | Description |
|-----|----------|-------------|
| Core service missing `/api` prefix | Critical | Fixed across 6 services |
| `userService.getPrograms` wrong URL | High | Fixed to use correct route |

---

## Infrastructure Hardening

### Cross-Cutting Improvements
- `ISessionVersionService` / `IUnitOfWork` integration across all services
- `ILocalizationService` — unified bilingual string resolution
- `IPermissionCacheInvalidator` — cache busting on structure moves
- `OutboxMessage` — added `CorrelationId`, `Culture`, `IsPoisoned`, `PoisonedAt`
- `StructureNodeDto` — added `LocalizedName` and `TypeNameLocalized` fields
- Module localization manifests for CourseOfferings, Payments, Schedule, StudentInformation
