# Frontend Changes — Merge1Branch vs ui-flow

> **162 files changed, 15,201 lines added, 4,098 lines removed**
>
> This document explains every change in simple terms — what was added, what was removed, what was redesigned, and why.

---

## Table of Contents

1. [🌍 Internationalization (i18n / Multi-Language Support)](#1-internationalization-i18n--multi-language-support)
2. [🧭 Navigation Overhaul (Sidebar + Navbar)](#2-navigation-overhaul-sidebar--navbar)
3. [🆕 New Core Components](#3-new-core-components)
4. [📐 Layout & Shell Changes](#4-layout--shell-changes)
5. [🧩 Route System Changes](#5-route-system-changes)
6. [🛠️ New Module: Student Services (Admin Side)](#6-new-module-student-services-admin-side)
7. [🎓 New Module: Student Portal Pages](#7-new-module-student-portal-pages)
8. [👤 New Module: Student Detail Page](#8-new-module-student-detail-page)
9. [🔐 Permissions System Rewrite](#9-permissions-system-rewrite)
10. [👥 Users Module Restructure](#10-users-module-restructure)
11. [🗑️ Deleted: Invoices Module](#11-deleted-invoices-module)
12. [📦 Package & Build Changes](#12-package--build-changes)
13. [🗺️ Route Registry Cleanup](#13-route-registry-cleanup)
14. [🎨 Style & CSS Changes](#14-style--css-changes)
15. [🔧 Config & Misc Changes](#15-config--misc-changes)
16. [🪄 New Features Summary](#16-new-features-summary)

---

## 1. 🌍 Internationalization (i18n / Multi-Language Support)

**This is the biggest new feature.** The app now supports **English and Arabic** (full RTL support).

### What was added:

| File | What it does |
|------|-------------|
| `src/core/i18n/i18n.js` | The main i18n setup — configures i18next with language detection and translation loading |
| `src/core/i18n/locales/en/*.json` | **12 English translation files** (auth, common, dashboard, landing, navigation, notifications, permissions, staff, structure, studentServices, students, validation) |
| `src/core/i18n/locales/ar/*.json` | **12 Arabic translation files** (same categories) |

### How it works:

- The i18n system is initialized in `main.jsx` (line: `import "./core/i18n/i18n"`)
- It uses **i18next** + **react-i18next** + **browser language detector**
- The language detector auto-detects the user's browser language
- A language switcher was added in the **Navbar** (top bar) so users can toggle between English and Arabic
- Every text label in the UI now uses `{ t("key") }` instead of hardcoded strings

### New dependencies added for i18n:

```json
"i18next": "^24.2.3",
"i18next-browser-languagedetector": "^8.2.1",
"i18next-http-backend": "^3.0.2",
"react-i18next": "^15.4.1"
```

### Translation categories:

| File | What it covers |
|------|---------------|
| `common.json` | Buttons, form labels, statuses, general UI text (~230 keys) |
| `auth.json` | Login, register, password reset, permissions |
| `navigation.json` | Sidebar & navbar menu labels |
| `dashboard.json` | Dashboard widgets and stats |
| `landing.json` | Landing page text |
| `notifications.json` | Notification messages |
| `permissions.json` | Permission names and descriptions |
| `staff.json` | Staff-related labels |
| `structure.json` | University structure labels |
| `students.json` | Student-related labels |
| `studentServices.json` | Student services module labels |
| `validation.json` | Form validation messages |

### RTL (Right-to-Left) Support:

- The `<html>` tag now includes `dir="ltr"` by default
- The Sidebar component conditionally sets `dir="rtl"` when Arabic is active
- User names that come as `{ en: "...", ar: "..." }` objects are now resolved based on the active language
- Many CSS transitions now use `margin-inline-start` instead of `margin-left` for RTL compatibility

---

## 2. 🧭 Navigation Overhaul (Sidebar + Navbar)

### Sidebar (`src/core/navigation/sidebar/Sidebar.jsx`)

**Before:** A basic sidebar with hardcoded "UniAdmin" branding, simple user avatar (first letter), and basic menu rendering.

**After:** A fully redesigned sidebar with:

- **New branding:** Uses a university logo image (`/images/UniLogo2.png`) instead of an SVG icon + "UniAdmin" text. Shows "Capital University" as the app name (from translation `t("app_name")`).
- **User card redesign:** Shows user avatar (first letter of name), full name (resolved for current language), and role name (translated to Arabic if needed).
- **i18n integration:** All menu labels use `t()` for translation.
- **Category icons:** Menu categories now have dynamic icons from `getCategoryIcon()`.
- **Logout handling:** Improved logout that awaits the function before redirecting.
- **RTL support:** The sidebar sets `dir="rtl"` when Arabic is selected.
- **Background decoration:** Kept the geometric SVG decoration (circles, lines) but improved the visual design.

### Navbar (`src/core/navigation/navbar/Navbar.jsx`)

**Before:** Basic navbar with user dropdown, notification bell, and scope selector.

**After:** Enhanced with:

- **Language switcher:** Users can now toggle between English and Arabic.
- **Command Palette button:** A new button that opens the Command Palette (see below).
- **Localized user names:** Handles `{ en, ar }` object names properly.
- **Improved notification display:** Better time formatting ("just now", "Xm ago", "Xh ago").
- **Cleaner code:** Removed stale comments, improved structure.

### Secondary Sidebar (`secondarySidebar/SecondarySidebar.jsx`)

- Minor changes to integrate with the new translation system.

### New CSS files:

| File | Description |
|------|-------------|
| `src/core/styles/sidebar.css` | **173 lines changed** — Major redesign for the sidebar |
| `src/core/styles/navbar.css` | **153 lines changed** — Enhanced navbar styling |

---

## 3. 🆕 New Core Components

Six new reusable components were added to `src/core/components/`:

### Breadcrumbs (`Breadcrumbs.jsx` + `breadcrumbs.css`)

- **111 lines** of component code + **44 lines** of CSS
- Shows a breadcrumb trail on every page (e.g., Home > Students > Details)
- Uses the current URL path to automatically generate breadcrumbs
- Maps URL segments to human-readable labels (e.g., `/admin/students` → "Students")
- Integrates with the `useLocation()` hook from React Router
- Renders in the DashboardLayout, above the page content

### Command Palette (`CommandPalette.jsx` + `commandPalette.css`)

- **352 lines** of component code + **277 lines** of CSS
- Triggered by **Cmd+K** (Mac) or **Ctrl+K** (Windows/Linux)
- A Spotlight/Alfred-style fuzzy search popup
- Lets you search and navigate to any page in the app
- Shows keyboard shortcut hints
- Launched from DashboardLayout with lazy loading (`React.lazy`)

### EmptyState (`EmptyState.jsx`)

- **30 lines** — A simple reusable component for "no data" states
- Shows an icon, title, and description when lists/tables are empty
- Used in StudentDirectory and other list pages

### ForbiddenPage (`ForbiddenPage.jsx`)

- **28 lines** — A nice 403 "Access Denied" page
- Shows a lock icon with "Forbidden" message
- Used when users try to access pages they don't have permission for

### KeyboardShortcutsModal (`KeyboardShortcutsModal.jsx`)

- **73 lines** — Triggered by pressing **?** (question mark) key
- Shows a modal listing all available keyboard shortcuts
- Lazy-loaded alongside CommandPalette

### ScopeMultiSelectModal (`ScopeMultiSelectModal.jsx` + `scopeModal.css`)

- **205 lines** of component code + **39 lines** of CSS
- A modal for selecting multiple scope items (university structure nodes)
- Used by the scope selector in the Navbar
- More advanced than the previous single-scope selector

### SessionTimeoutWarning (`SessionTimeoutWarning.jsx`)

- **119 lines** — Shows a warning banner when the user's session is about to expire
- Integrated in DashboardLayout as a fixed banner at the top
- Calls `logout` when the session actually expires

### UserDetailView (`UserDetailView.jsx`) — Modified

- **11 lines changed** — Updated to work with the new user data structure
- Now handles localized names

### Toast (`Toast.jsx`) — Redesigned

**Before:** Simple toast notifications with basic styling.

**After:** Completely redesigned with:

- **Smooth animations:** Toast slides in with a cubic-bezier animation, slides out on dismiss
- **Progress bar:** Shows remaining time before auto-dismiss
- **Hover pause:** Hovering over a toast pauses the dismiss timer
- **Better colors:** Updated color palette for each type (success, error, warning, info)
- **Focus trap:** Each toast is focusable and has `role="alert"` for accessibility
- **Backdrop blur:** Glass morphism effect with backdrop filter

### shellComponents.css

- **333 lines** — New CSS file for shared shell component styles (CommandPalette, KeyboardShortcutsModal, SessionTimeoutWarning)

---

## 4. 📐 Layout & Shell Changes

### DashboardLayout (`src/core/layouts/DashboardLayout.jsx`)

**Before:** Simple layout with sidebar + navbar + content area.

**After:** Significantly enhanced:

- **Session timeout banner** added at the top of the dashboard
- **Breadcrumbs** rendered above the page content
- **Command Palette** (Cmd+K) lazy-loaded and conditionally rendered
- **Keyboard Shortcuts Modal** (?) lazy-loaded and conditionally rendered
- **Global keyboard event listeners** registered for:
  - `Cmd+K` / `Ctrl+K` → toggle Command Palette
  - `?` (not in inputs) → toggle Shortcuts Modal
- **RTL-compatible margins:** Uses `marginInlineStart` instead of `marginLeft`
- **Improved responsive breakpoints:** Better handling of tablet-width screens (between 768px and 1024px) — when the sidebar is closed on tablet, the content margin goes to 0 instead of 64px
- **`openCommandPalette` callback** passed to Navbar as a prop

### `src/core/services/` — New services

These are new API service files for the Student Services module:

| File | Lines | Purpose |
|------|-------|---------|
| `studentServicesService.js` | 209 | All API calls for the student services module |
| `authorizationService.js` | 5 | Minor additions for authorization |

### `permissionService.js` — Modified

- **16 lines changed** — Updated to support the new permission model with action levels (View=1, Insert=2, EditClose=3, Open=4, Delete=5)

---

## 5. 🗺️ Route System Changes

### `routeRegistry.js`

**Changes:**

1. **Replaced invoices routes** with student services routes:
   - Removed: `import invoicesRoutes from "../../modules/invoices/routes"`
   - Added: `import studentServicesRoutes from "../../modules/studentServices/routes"`

2. **Removed legacy regex** for `:invoiceId` parameter matching (since invoices module was deleted)

### `AppRouter.jsx`

**Changes:**

- **404 page redesign:** Replaced the inline-styled 404 page with a proper component using CSS classes
- The old 404 was an inline `<div>` with inline styles; the new one has a nice card layout with an SVG icon, proper heading hierarchy, and a "Back to Dashboard" link instead of "Back to Home"
- This aligns with the new error-page styling approach

### Individual module routes:

See sections below for each module's route changes.

---

## 6. 🛠️ New Module: Student Services (Admin Side)

**This is a brand new module** — a full admin interface for managing student services (requests, workflows, notifications).

### Routes (`src/modules/studentServices/routes.js`) — **76 lines**

| Path | Page | Permission |
|------|------|-----------|
| `/admin/student-services` | Redirect → dashboard | `studentServices.dashboard.view` |
| `/admin/student-services/dashboard` | StaffDashboard | `studentServices.dashboard.view` |
| `/admin/student-services/services` | ServicesManagement | `studentServices.services.view` |
| `/admin/student-services/services/create` | ServiceBuilder | `studentServices.services.insert` |
| `/admin/student-services/services/:id/edit` | ServiceBuilder | `studentServices.services.edit` |
| `/admin/student-services/requests` | RequestsManagement | `studentServices.requests.view` |
| `/admin/student-services/requests/:id` | RequestReview | `studentServices.requests.review` |
| `/admin/student-services/notifications` | NotificationsCenter | `studentServices.notifications.view` |

### Admin Pages (6 new pages):

| File | Purpose |
|------|---------|
| `pages/admin/StaffDashboard.jsx` (119 lines) | Dashboard showing statistics and overview |
| `pages/admin/ServicesManagement.jsx` (84 lines) | List/manage all student services |
| `pages/admin/ServiceBuilder.jsx` (259 lines) | Create/edit a service with its workflow |
| `pages/admin/RequestsManagement.jsx` (117 lines) | View all student requests |
| `pages/admin/RequestReview.jsx` (128 lines) | Review and approve/reject a specific request |
| `pages/admin/NotificationsCenter.jsx` (120 lines) | Send and manage notifications |

### Reusable Components (14 new files):

#### In `src/modules/studentServices/components/`:

| File | Purpose |
|------|---------|
| `DynamicFormRenderer.jsx` (92 lines) | Renders dynamic forms from JSON schema |
| `EmptyState.jsx` (16 lines) | Empty state for lists |
| `FileUploader.jsx` (99 lines) | File upload with progress |
| `FilterBar.jsx` (85 lines) | Filter controls for lists |
| `FormBuilder.jsx` (95 lines) | Build dynamic form schemas |
| `LoadingSpinner.jsx` (15 lines) | Loading indicator |
| `Pagination.jsx` (59 lines) | Page navigation |
| `RequestCard.jsx` (26 lines) | Card view for a request |
| `RequestTimeline.jsx` (65 lines) | Timeline showing request status history |
| `ServiceCard.jsx` (25 lines) | Card view for a service |
| `StatusBadge.jsx` (31 lines) | Colored status indicator |
| `Stepper.jsx` (25 lines) | Multi-step form wizard |
| `WorkflowBuilder.jsx` (119 lines) | Visual workflow builder |

### Custom Hooks (6 new files):

| File | Purpose |
|------|---------|
| `hooks/useFileUpload.js` (71 lines) | File upload logic with progress tracking |
| `hooks/useServices.js` (92 lines) | Fetch and manage services |
| `hooks/useStaffRequests.js` (120 lines) | Fetch and manage staff requests |
| `hooks/useStatistics.js` (88 lines) | Dashboard statistics |
| `hooks/useStudentRequests.js` (114 lines) | Fetch and manage student requests |
| `hooks/useWorkflows.js` (145 lines) | Workflow CRUD operations |

### Service Layer:

| File | Purpose |
|------|---------|
| `src/modules/studentServices/services/studentServicesService.js` (209 lines) | All API calls for the module |

### Styles (18 new CSS files):

- `styles/admin/NotificationsCenter.css` (153 lines)
- `styles/admin/RequestReview.css` (116 lines)
- `styles/admin/RequestsManagement.css` (137 lines)
- `styles/admin/ServiceBuilder.css` (196 lines)
- `styles/admin/ServicesManagement.css` (137 lines)
- `styles/admin/StaffDashboard.css` (161 lines)
- `styles/components/DynamicFormRenderer.css` (48 lines)
- `styles/components/EmptyState.css` (16 lines)
- `styles/components/FileUploader.css` (90 lines)
- `styles/components/FilterBar.css` (95 lines)
- `styles/components/FormBuilder.css` (119 lines)
- `styles/components/LoadingSpinner.css` (25 lines)
- `styles/components/Pagination.css` (38 lines)
- `styles/components/RequestCard.css` (33 lines)
- `styles/components/RequestTimeline.css` (67 lines)
- `styles/components/ServiceCard.css` (44 lines)
- `styles/components/StatusBadge.css` (19 lines)
- `styles/components/Stepper.css` (80 lines)
- `styles/components/WorkflowBuilder.css` (160 lines)

---

## 7. 🎓 New Module: Student Portal Pages

**Expanded the student portal** with 5 new pages for students to interact with services and manage their requests.

### Routes (`src/modules/studentPortal/routes.js`) — New routes added:

| Path | Page | Permission |
|------|------|-----------|
| `/student/payments` | StudentPaymentsPage | `student.dashboard.view` |
| `/student/services/:id` | StudentServiceDetails | `student.services.view` |
| `/student/services/:id/apply` | RequestSubmission | `student.services.request` |
| `/student/requests` | MyRequests | `student.requests.view` |
| `/student/requests/:id` | StudentRequestDetails | `student.requests.view` |
| `/student/notifications` | StudentNotifications | `student.dashboard.view` |

Also added **"My Requests"** and **"Notifications"** to the student sidebar menu.

### New Student Pages (5 new files):

| File | Purpose |
|------|---------|
| `pages/StudentPaymentsPage.jsx` (538 lines) | View and pay fees |
| `pages/StudentServiceDetails.jsx` (82 lines) | Details of a specific service |
| `pages/RequestSubmission.jsx` (149 lines) | Form to submit a new request |
| `pages/MyRequests.jsx` (75 lines) | List of user's requests |
| `pages/StudentRequestDetails.jsx` (64 lines) | Details of a specific request |
| `pages/StudentNotifications.jsx` (108 lines) | User's notifications page |

### Student Portal Components (4 new files):

| File | Purpose |
|------|---------|
| `components/DynamicFormRenderer.jsx` (92 lines) | Same as the admin version — renders dynamic forms |
| `components/FileUploader.jsx` (99 lines) | File upload with preview |
| `components/RequestTimeline.jsx` (65 lines) | Request status timeline |
| `components/ServiceCard.jsx` (25 lines) | Service card display |
| `components/StatusBadge.jsx` (31 lines) | Status badge component |

### Student Portal Hooks (2 new files):

| File | Purpose |
|------|---------|
| `hooks/useFileUpload.js` (71 lines) | File upload with progress |
| `hooks/useStudentRequests.js` (114 lines) | Fetch student requests |

### Student Portal Styles (8 new CSS files):

- `styles/MyRequests.css` (64 lines)
- `styles/RequestSubmission.css` (93 lines)
- `styles/ServiceDetails.css` (138 lines)
- `styles/StudentNotifications.css` (134 lines)
- `styles/StudentRequestDetails.css` (76 lines)
- `styles/studentDashboard.css` (81 lines) — **modified** (179 lines changed total)
- `styles/studentPayments.css` (416 lines)

---

## 8. 👤 New Module: Student Detail Page

### `src/modules/students/pages/StudentDetailPage.jsx` — **NEW (436 lines)**

A comprehensive student detail page with 7 tabbed sections:

| Tab | Content |
|-----|---------|
| Overview | Summary card with key info |
| Personal Info | Detailed personal information |
| Academic History | Courses taken, grades |
| Enrollments | Current and past enrollments |
| Payments | Payment history |
| Documents | Uploaded documents |
| Activity Log | Audit trail |

### `src/modules/students/pages/StudentDirectory.jsx` — **MAJOR REWRITE (+439 lines)**

**Before:** Delegated to `UserManagement` component.

**After:** Full-featured standalone student directory with:

- **Search** with debounced input
- **Filters** (active status, password status, faculty, program, level)
- **Pagination** with page controls
- **Table view** with student data columns
- **Scope integration** — respects the current user's scope
- **Toast notifications** for actions
- **Skeleton loading** states
- **Empty state** component when no results

### `src/modules/students/routes.js` — New route added:

| Path | Page | Permission |
|------|------|-----------|
| `/admin/students/:id` | StudentDetailPage | `users.users.view` |

---

## 9. 🔐 Permissions System Rewrite

### RolesPage (`src/modules/permissions/pages/RolesPage.jsx`)

**Before:** A basic role list with a modal for editing role permissions (pagination, simple CRUD).

**After:** A **complete rewrite** (807 lines changed) with:

- **Inline permission editing** — click on a role to expand its permission tree directly on the page (no modal)
- **Action levels:** Each resource now has a level from 0-5:
  - 0 = No Permission
  - 1 = View
  - 2 = Insert
  - 3 = Edit
  - 4 = Open
  - 5 = Delete
- **Level selector UI:** Visual selector (buttons/radio) to set permission levels
- **Pending changes tracking:** Tracks what permissions were modified before saving
- **Role members tab:** Shows which users are assigned to each role
- **Create role modal:** Inline modal for creating roles
- **Role search:** Filter roles by name
- **i18n integration:** All labels use `t()` for translation
- **Permission tree** from `authorizationService` instead of the old modal approach
- **No pagination** — all roles shown at once with search filter

### PermissionsPage (`src/modules/permissions/pages/PermissionsPage.jsx`)

**Major rewrite** (614 lines changed) — Redesigned to match the new permission model with:
- Inline permission editing similar to RolesPage
- Action level selectors
- i18n support

### RolePermissionsModal (`src/modules/permissions/components/RolePermissionsModal.jsx`)

**Heavily modified** (214 lines changed) — updated to support:
- The new level-based permission model
- i18n translation
- Better UX with visual level selectors

### CSS Changes:

| File | Lines changed |
|------|--------------|
| `permissions.css` | 687 lines changed |
| `roles.css` | 749 lines changed |

Both were completely restyled for the new inline editing UI.

### Permission Tree Page (`authorization/pages/PermissionTreePage.jsx`)

- **24 lines changed** — updated for the new permission model

---

## 10. 👥 Users Module Restructure

### Route Changes (`src/modules/users/routes.js`)

**Before (5 routes):**

| Path | Page |
|------|------|
| `/admin/users/add-student` | AddStudent |
| `/admin/users/edit-student/:id` | EditStudent |
| `/admin/users/add-staff` | AddStaff |
| `/admin/users/edit-staff/:id` | EditStaff |
| `/admin/users/:id` | UserHubPage |

**After (6 routes, restructured paths):**

| Path | Page |
|------|------|
| `/admin/users` | UserManagement |
| `/admin/users/:id` | UserDetails (NEW) |
| `/admin/users/staff/add` | AddStaff |
| `/admin/users/staff/:id/edit` | EditStaff |
| `/admin/users/students/add` | AddStudent |
| `/admin/users/students/:id/edit` | EditStudent |

**Key changes:**
- Added a root `/admin/users` path that renders **UserManagement** directly
- Replaced `UserHubPage` with a new **UserDetails** page
- Standardized URL patterns: `/{type}/add` and `/{type}/:id/edit` instead of `add-{type}` and `edit-{type}/:id`
- **UserManagement now has its own dedicated route** instead of being used as a fallback

### New: UserDetails (`src/modules/users/pages/UserDetails.jsx`)

- **249 lines** — A new unified user detail view
- Shows both student and staff details in a consolidated layout
- Uses `useTranslation()` for i18n
- Loads user data from `userService`

### New: userTypeConfig (`src/modules/users/components/userTypeConfig.jsx`)

- **123 lines** — Configuration for different user types
- Each type has: label (Arabic + English), category (student/staff/admin), color, icon, and sort order
- Types: Student, Professor, AssistantProfessor, TeachingAssistant, Instructor, AdminStaff, LabSupervisor, Technician, Accountant, Secretary, Dean, ViceDean, DepartmentHead, ProgramCoordinator, Advisor, Librarian, Researcher

### Deleted Tabs (`src/modules/users/components/tabs/`):

**These 4 files were deleted** (their functionality moved to the new UserDetails and StudentDetailPage):

| File | Lines | Purpose |
|------|-------|---------|
| `UserCoursesTab.jsx` | 62 | Deleted |
| `UserFinancialsTab.jsx` | 146 | Deleted |
| `UserPermissionsTab.jsx` | 278 | Deleted |
| `UserProfileTab.jsx` | 73 | Deleted |

### Deleted: UserHubPage (`src/modules/users/pages/UserHubPage.jsx`)

- **225 lines** — This was the old user detail page, replaced by `UserDetails.jsx`

### Modified files:

| File | Changes |
|------|---------|
| `UserManagement.jsx` (17 lines) | Minor updates for new routing structure |
| `StaffTable.jsx` (2 lines) | Minor fix |
| `StudentTable.jsx` (2 lines) | Minor fix |

---

## 11. 🗑️ Deleted: Invoices Module

**The entire invoices module was removed** (replaced by the student payments feature in the Student Portal):

| File | Lines | Type |
|------|-------|------|
| `invoices/routes.js` | 26 | Deleted |
| `invoices/pages/InvoiceDetailsPage.jsx` | 477 | Deleted |
| `invoices/pages/InvoicesPage.jsx` | 532 | Deleted |
| `invoices/styles/invoices.css` | 521 | Deleted |

**Total: 1,556 lines removed**

The invoice functionality was replaced by the new **Student Payments** feature (`StudentPaymentsPage.jsx`), which is part of the student portal and has a different approach to handling payments and fees.

The `invoiceService.js` and `paymentService.js` files still exist in `src/core/services/` (they were not deleted), but they are no longer routed through the app.

**Route reference removed from:**
- `routeRegistry.js` — `import invoicesRoutes` was replaced with `import studentServicesRoutes`
- The `:invoiceId` regex pattern was also removed from the route matching logic

---

## 12. 📦 Package & Build Changes

### `package.json` — New dependencies:

**Production dependencies:**

| Package | Version | Purpose |
|---------|---------|---------|
| `i18next` | ^24.2.3 | Internationalization framework |
| `i18next-browser-languagedetector` | ^8.2.1 | Auto-detect browser language |
| `i18next-http-backend` | ^3.0.2 | Load translation files via HTTP |
| `react-i18next` | ^15.4.1 | React bindings for i18next |

**Dev dependencies:**

| Package | Version | Purpose |
|---------|---------|---------|
| `@originjs/vite-plugin-federation` | ^1.4.1 | Micro-frontend federation (was already used but now explicit) |
| `@rolldown/plugin-babel` | ^0.2.3 | Babel integration with Rolldown |
| `@testing-library/dom` | ^10.4.1 | DOM testing utilities |
| `babel-plugin-react-compiler` | ^1.0.0 | React compiler optimization plugin |

### `vite.config.js` — Build configuration changes:

**Before:**
```js
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    federation({...}),
  ],
})
```

**After:**
```js
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation'
import babel from '@rolldown/plugin-babel'

export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] }),
    federation({...}),
  ],
})
```

**Key changes:**
1. Added `@rolldown/plugin-babel` with the **React Compiler** preset for build-time optimizations
2. The `babel` plugin runs the React Compiler during builds (this optimizes re-renders)
3. The federation plugin remains unchanged

---

## 13. 🗺️ Route Registry Cleanup

### `routeRegistry.js` — Changes:

**Before:**
```js
import invoicesRoutes from "../../modules/invoices/routes";

const ALL_ROUTES = [
  // ...
  ...invoicesRoutes,
  // ...
];
```

**After:**
```js
import studentServicesRoutes from "../../modules/studentServices/routes";

const ALL_ROUTES = [
  // ...
  ...studentServicesRoutes,
  // ...
];
```

**Also removed:**
```js
// This line was removed from the regex pattern builder:
.replace(/\/:invoiceId\b/g, "/[^/]+")
```

This was cleanup since the invoice routes were deleted and there's no more `:invoiceId` parameter.

### `index.html` — Tiny change:

- Added `dir="ltr"` to the `<html>` tag for explicit text direction (prepares for RTL support)
- Changed from `<html lang="en">` to `<html lang="en" dir="ltr">`

---

## 14. 🎨 Style & CSS Changes

### Summary of all CSS changes:

| File | Lines | Change |
|------|-------|--------|
| `src/core/styles/sidebar.css` | 173 | Major redesign for new sidebar UI + RTL |
| `src/core/styles/navbar.css` | 153 | Navbar redesign + language switcher styles |
| `src/core/components/breadcrumbs.css` | 44 | **New** — Breadcrumbs styling |
| `src/core/components/commandPalette.css` | 277 | **New** — Command palette styling |
| `src/core/components/scopeModal.css` | 39 | **New** — Scope modal styling |
| `src/core/components/shellComponents.css` | 333 | **New** — Shared shell component styles |
| `src/modules/permissions/styles/permissions.css` | 687 | Complete rewrite for inline editing |
| `src/modules/permissions/styles/roles.css` | 749 | Complete rewrite for inline editing |
| `src/modules/notifications/styles/notifications.css` | 2 | Minor update |
| `src/modules/university/styles/universityStructure.css` | 82 | **New** — University structure page styling |
| Plus all the Student Services CSS files (~19 files, ~2,000+ lines total) | | **All new** |
| Plus all the Student Portal CSS files (~8 files, ~1,000+ lines total) | | New/modified |

### Notifications styles (`notifications.css`):

- **2 lines changed** — Minor style tweaks for the notifications page component

---

## 15. 🔧 Config & Misc Changes

### `src/core/contexts/AuthContext.jsx` — 2 lines changed

- Minor updates for the new authentication flow

### `src/core/contexts/ScopeContext.jsx` — 1 line changed

- Minor scope context update

### `AcademicPlansPage.jsx` — 122 lines changed

- Updated for the new routing/i18n patterns

### `CourseOfferingsPage.jsx` — 38 lines changed + `OfferingForm.jsx` — 44 lines changed

- Updates for course offering management

### `NotificationsPage.jsx` — 34 lines changed

- Updated for i18n

### `SlotForm.jsx` — 24 lines changed + `ScheduleSlotsPage.jsx` — 38 lines changed

- Schedule management updates

### `AcademicYears` routes — 2 lines changed

- Minor route update

### `Admin` routes — 4 lines changed

- Minor route update

### `University` routes — 5 lines changed

- Added new route for university structure

### `StudentDashboard.jsx` — 179 lines changed

- Major update to integrate with the new student services and notifications

### `frontend.zip` — NEW (binary, ~18MB)

- A ZIP archive of the frontend (likely for deployment or distribution)

---

## 16. 🪄 New Features Summary

Here's everything a user will notice that's **new**:

| Feature | How to access it |
|---------|-----------------|
| 🌐 **Arabic Language** | Click language switcher in the top navbar |
| 🔍 **Command Palette** | Press `Cmd+K` / `Ctrl+K` anywhere |
| ⌨️ **Keyboard Shortcuts** | Press `?` anywhere |
| 🍞 **Breadcrumbs** | Auto-shown at the top of every page |
| ⏰ **Session Timeout Warning** | Auto-appears when session is about to expire |
| 📋 **Student Services (Admin)** | Navigate to "Student Services" in the sidebar |
| 🛎️ **Student Requests** | Students can submit and track requests |
| 💳 **Student Payments** | Students can view and pay fees |
| 🔔 **Student Notifications** | Students have their own notification center |
| 👤 **Student Detail Page** | Click a student in the directory |
| 🔐 **Inline Permission Editor** | Click a role to edit permissions directly on the page |
| 🔄 **RTL Support** | Switch to Arabic and the UI flips to right-to-left |

---

## 📊 Overall Statistics

```
162 files changed
15,201 insertions (+)
 4,098 deletions (-)

New files:        ~130 files
Deleted files:     5 files (invoices module + old user tabs)
Modified files:   ~27 files

Most changed files:
  - src/core/i18n/i18n.js                   (+966 lines, NEW)
  - src/modules/permissions/pages/RolesPage.jsx  (+807 lines rewritten)
  - src/modules/permissions/styles/roles.css     (+749 lines rewritten)
  - src/modules/permissions/styles/permissions.css (+687 lines rewritten)
  - src/modules/permissions/pages/PermissionsPage.jsx (+614 lines rewritten)
  - src/modules/studentPortal/pages/StudentPaymentsPage.jsx (+538 lines, NEW)
  - src/modules/students/pages/StudentDirectory.jsx (+439 lines rewritten)
  - src/modules/students/pages/StudentDetailPage.jsx (+436 lines, NEW)
```

---

## 🗺️ Complete File Map

```
frontend/
├── index.html                                    [modified: dir attr added]
├── package.json                                  [modified: +8 deps]
├── vite.config.js                                [modified: babel plugin]
├── frontend.zip                                  [NEW: binary]
│
├── src/
│   ├── main.jsx                                  [modified: +i18n import]
│   │
│   ├── core/
│   │   ├── i18n/                                 [NEW MODULE]
│   │   │   ├── i18n.js                           [NEW: 966 lines]
│   │   │   └── locales/
│   │   │       ├── en/  (12 json files)          [NEW]
│   │   │       └── ar/  (12 json files)          [NEW]
│   │   │
│   │   ├── components/
│   │   │   ├── Breadcrumbs.jsx                   [NEW]
│   │   │   ├── CommandPalette.jsx                [NEW]
│   │   │   ├── EmptyState.jsx                    [NEW]
│   │   │   ├── ForbiddenPage.jsx                 [NEW]
│   │   │   ├── KeyboardShortcutsModal.jsx        [NEW]
│   │   │   ├── ScopeMultiSelectModal.jsx         [NEW]
│   │   │   ├── SessionTimeoutWarning.jsx         [NEW]
│   │   │   ├── Toast.jsx                         [REDESIGNED]
│   │   │   ├── UserDetailView.jsx                [MODIFIED]
│   │   │   ├── breadcrumbs.css                   [NEW]
│   │   │   ├── commandPalette.css                [NEW]
│   │   │   ├── scopeModal.css                    [NEW]
│   │   │   └── shellComponents.css               [NEW]
│   │   │
│   │   ├── contexts/
│   │   │   ├── AuthContext.jsx                   [MODIFIED]
│   │   │   └── ScopeContext.jsx                  [MODIFIED]
│   │   │
│   │   ├── layouts/
│   │   │   └── DashboardLayout.jsx               [MAJOR UPDATE]
│   │   │
│   │   ├── navigation/
│   │   │   ├── navbar/Navbar.jsx                 [MAJOR UPDATE]
│   │   │   ├── sidebar/Sidebar.jsx               [MAJOR UPDATE]
│   │   │   └── secondarySidebar/SecondarySidebar.jsx [MODIFIED]
│   │   │
│   │   ├── router/
│   │   │   ├── AppRouter.jsx                     [MODIFIED: 404 page]
│   │   │   └── routeRegistry.js                  [MODIFIED: invoices→services]
│   │   │
│   │   ├── services/
│   │   │   ├── authorizationService.js           [MODIFIED]
│   │   │   ├── invoiceService.js                 [MODIFIED]
│   │   │   ├── paymentService.js                 [MODIFIED]
│   │   │   ├── permissionService.js              [MODIFIED]
│   │   │   └── studentServicesService.js         [NEW]
│   │   │
│   │   └── styles/
│   │       ├── sidebar.css                       [MAJOR UPDATE]
│   │       └── navbar.css                        [MAJOR UPDATE]
│   │
│   ├── modules/
│   │   ├── studentServices/                      [NEW MODULE: 55+ files]
│   │   │   ├── routes.js
│   │   │   ├── pages/admin/       (6 files)
│   │   │   ├── components/        (14 files)
│   │   │   ├── hooks/             (6 files)
│   │   │   ├── services/          (1 file)
│   │   │   └── styles/            (19 files)
│   │   │
│   │   ├── studentPortal/                        [EXPANDED: +5 pages]
│   │   │   ├── routes.js                         [EXPANDED]
│   │   │   ├── pages/             (+5 files)
│   │   │   ├── components/        (+4 files)
│   │   │   ├── hooks/             (+2 files)
│   │   │   └── styles/            (+8 files)
│   │   │
│   │   ├── students/                             [EXPANDED]
│   │   │   ├── routes.js                         [MODIFIED]
│   │   │   ├── pages/StudentDetailPage.jsx       [NEW]
│   │   │   └── pages/StudentDirectory.jsx        [REWRITTEN]
│   │   │
│   │   ├── users/                                [RESTRUCTURED]
│   │   │   ├── routes.js                         [REWRITTEN]
│   │   │   ├── pages/UserDetails.jsx             [NEW]
│   │   │   ├── pages/UserHubPage.jsx             [DELETED]
│   │   │   ├── components/userTypeConfig.jsx     [NEW]
│   │   │   └── components/tabs/ (4 files)        [DELETED]
│   │   │
│   │   ├── permissions/                          [REWRITTEN]
│   │   │   ├── pages/RolesPage.jsx               [REWRITTEN]
│   │   │   ├── pages/PermissionsPage.jsx         [REWRITTEN]
│   │   │   ├── components/RolePermissionsModal.jsx [REWRITTEN]
│   │   │   └── styles/ (2 css files)             [REWRITTEN]
│   │   │
│   │   ├── invoices/                             [DELETED: 4 files]
│   │   │
│   │   └── (other modules: minor updates)
```

## Backend Changes
1. StudentDirectory.jsx — Filter bug fix (today's change)
What: Fixed IsActive/PasswordExpired filter params being sent as false on initial page load because appliedFilters started as {}.
Impact: ⚡ Critical — The student directory page was sending ?IsActive=false to the API, so it only returned inactive students (zero). That's why you saw "No students found". Now it sends no filter on initial load → shows all students.
2. New database indexes on Student and Staff tables
What: Added indexes on Student.StructureNodeId, Student.Email, Staff.StructureNodeId, Staff.Email.
Impact: 🚀 The directory page and sidebar search were very slow because these columns had no indexes. The sidebar search does JOINs through StructureNodeId, and the directory does filtering by email/name. Now those queries are fast instead of scanning the whole table.
3. Dashboard statistics optimized
What: The dashboard "total students / total staff" numbers were loading ALL rows into memory just to count them. Changed to use CountAsync() (SQL COUNT(*)).
Impact: Dashboard loads faster — it was doing SELECT * FROM Students + SELECT * FROM Staff and counting in memory, now it does SELECT COUNT(*).
4. Database initialization changed (MigrateAsync → EnsureCreatedAsync)
What: The app was crashing on startup with "Pending model changes" error because there were no migration files. Switched to auto-create the database.
Impact: The app actually starts now instead of crashing. No data loss — same database, just different creation mechanism.
5. Search query optimization (deferred .Include())
What: In StudentRepository.SearchAsync and StaffRepository.SearchAsync, the .Include(x => x.StructureNode) was being run before the count query, loading unnecessary data.
Impact: The directory page search results load faster, especially with larger datasets.

Bottom line: Almost all changes were about fixing the app from crashing or being unusably slow. The one change that directly caused "no students showing" was #1 (the filter bug) plus #2 (missing endpoint). All others were performance/startup fixes.

---

> End of document. Total changes: **15,201 additions, 4,098 deletions across 162 files**.
