# Frontend Changelog - capu-portal

## Phase 1: Repository Discovery & Audit

### Architecture Overview
- **Framework:** React 18 with Vite build tool
- **Routing:** React Router v6 (manual + manifest-driven)
- **API Client:** Axios with JWT interceptor (`core/api/apiClient.js`)
- **State Management:** React Context (ScopeContext, AuthContext, PermissionContext, AcademicContext, DomainContext, StickySelectionContext)
- **Styling:** Plain CSS with CSS custom properties (Navy #1a1f5e + Gold #c9a84c scheme), DM Sans body font, Space Mono for headings
- **Module System:** Manifest-driven modules with routes, menu items, and permissions defined in `manifest.json` per module
- **Icon Library:** Lucide React

### Module Inventory
| Module | ID | Pages | Status |
|--------|----|-------|--------|
| Landing | landing | LandingPage | Complete |
| Admin | admin | AdminDashboard | Connected (uses static data) |
| Users | users | UserManagement, AddStudent/Staff, EditStudent/Staff, UserDetails | Connected to backend |
| Students | students | StudentDirectory (wraps UserManagement) | Connected |
| Staff | staff | StaffDirectory (wraps UserManagement) | Connected |
| University | university | UniversityStructurePage | Full CRUD connected |
| Permissions | permissions | PermissionsPage, RolesPage | Connected |

### Core Services Inventory
| Service | File | Connected Endpoints |
|---------|------|---------------------|
| authService | `core/auth/authService.js` | `POST /api/auth/login`, `POST /api/auth/logout` |
| studentService | `core/services/studentService.js` | `GET/POST/PUT/DELETE /students` (missing `/api` prefix) |
| staffService | `core/services/staffService.js` | `GET/POST/PUT/DELETE /staff` (missing `/api` prefix) |
| structureService | `core/services/structureService.js` | Full structure & lookup endpoints (missing `/api` prefix) |
| academicService | `core/services/academicService.js` | Academic years + semesters read (missing `/api` prefix) |
| permissionService | `core/services/permissionService.js` | Permissions + roles CRUD (missing `/api` prefix) |
| userService | `modules/users/services/userService.js` | Students + staff CRUD + statistics (correct `/api` prefix) |

### Initial Audit Findings
1. All core services (studentService, staffService, structureService, academicService, permissionService, authService) use API paths **missing the `/api` prefix** (e.g., `/students` instead of `/api/students`), while userService correctly uses `/api/students`. This is a critical bug.
2. Dashboard displays static/mock data instead of connecting to backend endpoints.
3. Backend `GET /api/auth/me` endpoint does not exist but frontend `authService.getCurrentUser()` calls it.

---

## Phase 2: Gap Analysis

### Mapped Backend Endpoints vs Frontend Coverage

#### Auth (`/api/auth`)
| Endpoint | Frontend Service | Status |
|----------|-----------------|--------|
| POST `/api/auth/login` | authService.login | Done |
| POST `/api/auth/logout` | authService.logout | Done |
| POST `/api/auth/refresh` | **Missing** | Not done |
| POST `/api/auth/change-password` | **Missing** | Not done |
| GET `/api/auth/me` (non-existent) | authService.getCurrentUser calls it | Backend bug |

#### Students (`/api/students`)
| Endpoint | Frontend Service | Status |
|----------|-----------------|--------|
| GET /search, GET /, GET /{id}, POST, PUT, DELETE, PATCH toggle-status, GET statistics, GET export/csv, GET export-excel | studentService + userService | Done |
| POST `/api/students/bulk-import` | **Missing** | Not done |
| POST `/api/students/import-excel` | **Missing** | Not done |

#### Staff (`/api/staff`) - same gaps as students
| Endpoint | Status |
|----------|--------|
| POST `/api/staff/bulk-import` | Not done |
| POST `/api/staff/import-excel` | Not done |

#### Roles (`/api/roles`)
| Endpoint | Status |
|----------|--------|
| GET `/api/roles`, GET /{id}, POST, PUT, DELETE | Done |
| Role response missing `createdAt` field | Backend bug (frontend reads it) |

#### Authorization (`/api/authorization`)
| Endpoint | Status |
|----------|--------|
| GET `/api/authorization/permissions/tree` | Not done |
| GET `/api/authorization/roles/{roleId}/permissions` | Not done |

#### Academic Years (`/api/academic-years`)
| Endpoint | Status |
|----------|--------|
| GET, GET /{id}, GET /{id}/semesters | Done (academicService) |
| POST, PATCH /{id}, DELETE /{id}, POST /resolve | Not done |

#### Semesters (`/api/semesters`)
| Endpoint | Status |
|----------|--------|
| GET /{id}, GET /current | Done (academicService) |
| POST, PATCH /{id}, DELETE /{id}, POST /resolve | Not done |

#### Notifications (`/api/notifications`)
| Endpoint | Status |
|----------|--------|
| GET, GET /unread, PUT /{id}/read | No service exists, no UI |

#### Structure Lookups
| Endpoint | Status |
|----------|--------|
| GET /systems, GET /specializations | Not done |

### Missing Pages
| Page | Route | Priority |
|------|-------|----------|
| Academic Calendar Management | `/admin/academic-calendar` | High |
| Notifications Panel/Page | `/admin/notifications` | High |
| Change Password UI | `/admin/change-password` | Medium |

---

## Phase 3: Implementation

### Files Created (8 new files)
| File | Description |
|------|-------------|
| `frontend/src/core/services/notificationService.js` | New notification API service (fetch, mark read, unread) |
| `frontend/src/core/components/ChangePasswordModal.jsx` | Change password modal with validation |
| `frontend/src/modules/academic/manifest.json` | Academic Calendar module manifest (route + menu) |
| `frontend/src/modules/academic/pages/AcademicCalendarPage.jsx` | Full CRUD page for academic years and semesters |
| `frontend/src/modules/academic/styles/academicCalendar.css` | CSS for AC page (matches navy/gold scheme) |
| `frontend/src/modules/notifications/manifest.json` | Notifications module manifest (route + menu) |
| `frontend/src/modules/notifications/pages/NotificationsPage.jsx` | Notifications list with filter, mark read |
| `frontend/src/modules/notifications/styles/notifications.css` | CSS for notifications page |

### Files Modified (12 modified files)
| File | Changes Made |
|------|-------------|
| `core/auth/authService.js` | Added `changePassword()`, `refreshToken()`, fixed `/api` prefix |
| `core/services/studentService.js` | Fixed `/api` prefix, added `bulkImportStudents()`, `importStudentExcel()` |
| `core/services/staffService.js` | Fixed `/api` prefix, added `bulkImportStaff()`, `importStaffExcel()` |
| `core/services/structureService.js` | Fixed `/api` prefix, added `fetchSystems()`, `fetchSpecializations()` |
| `core/services/academicService.js` | Fixed `/api` prefix, added semester + academic-year full CRUD + resolve |
| `core/services/permissionService.js` | Fixed `/api` prefix, added `fetchPermissionTree()`, `fetchRolePermissions()` |
| `core/router/AppRouter.jsx` | Added routes for Academic Calendar + Notifications |
| `core/router/routeRegistry.js` | Registered AcademicCalendarPage, NotificationsPage components |
| `core/manifests/manifestLoader.js` | Added academic + notifications module manifests |
| `core/navigation/navbar/Navbar.jsx` | Added notification bell with unread count, user avatar dropdown with change password + sign out |
| `core/styles/navbar.css` | Added numeric badge styles for notification count |
| `modules/users/services/userService.js` | Fixed `getPrograms()` URL: `/faculties/{id}/programs` -> `/{parentId}/children/Program` |

### Service Endpoints Added
| Service | Endpoints Added |
|---------|----------------|
| authService | `changePassword()`, `refreshToken()` |
| studentService | `bulkImportStudents()`, `importStudentExcel(File)` |
| staffService | `bulkImportStaff()`, `importStaffExcel(File)` |
| permissionService | `fetchPermissionTree()`, `fetchRolePermissions(roleId)` |
| academicService | `createSemester()`, `updateSemester()`, `deleteSemester()`, `resolveCurrentSemester()`, `createAcademicYear()`, `updateAcademicYear()`, `deleteAcademicYear()`, `resolveCurrentAcademicYear()` |
| structureService | `fetchSystems()`, `fetchSpecializations()` |
| notificationService (new) | `fetchNotifications()`, `fetchUnreadNotifications()`, `markNotificationRead(id)` |

### New Routes Added
| Route | Component | Description |
|-------|-----------|-------------|
| `/admin/academic-calendar` | AcademicCalendarPage | Manage academic years and semesters |
| `/admin/notifications` | NotificationsPage | View system notifications |

### Bugs Fixed
| Bug | Severity | Description |
|-----|----------|-------------|
| Core service missing `/api` prefix | Critical | All 6 core services used `/students` instead of `/api/students` — now fixed |
| userService.getPrograms wrong URL | High | Called `/faculties/{id}/programs` instead of `/{parentId}/children/Program` — now fixed |

### Backend Issues Logged
See `backend-requirements.md` for full details on:
- Missing `GET /api/auth/me` endpoint
- Role response missing `createdAt` field
- Dashboard lacks aggregate stats endpoint (optional)
- Unread notification count convenience endpoint (optional)
