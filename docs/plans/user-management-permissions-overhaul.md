# User Management & Permissions Overhaul — Architectural Plan

Status: **awaiting approval** (Phase 1 sweep complete, no code changed yet)
Date: 2026-06-11

## 1. Files audited in Phase 1

### Permissions / RBAC
- `frontend/src/modules/permissions/pages/RolesPage.jsx` (931 lines)
- `frontend/src/modules/permissions/pages/PermissionsPage.jsx` (1190 lines)
- `frontend/src/modules/permissions/components/RolePermissionsModal.jsx` (198 lines)
- `frontend/src/modules/permissions/routes.js`
- `frontend/src/modules/authorization/pages/PermissionTreePage.jsx` (236 lines)
- `frontend/src/core/services/permissionService.js`, `authorizationService.js`
- `frontend/src/core/auth/PermissionContext.jsx`, `PermissionGate.jsx`, `usePermission.js`
- `frontend/src/core/hooks/useNodeTypePermission.js`

### Students / profile records
- `frontend/src/modules/students/pages/StudentDetailPage.jsx` (460 lines)
- `frontend/src/modules/students/pages/StudentAcademicHubPage.jsx` (303 lines)
- `frontend/src/modules/students/routes.js`
- `frontend/src/modules/studentProfileRecords/pages/StudentProfileRecordsPage.jsx` (419 lines)
- `frontend/src/modules/studentProfileRecords/pages/StandaloneProfileRecordsPage.jsx` (484 lines)
- `frontend/src/modules/studentProfileRecords/routes.js`
- `frontend/src/core/services/studentService.js`, `studentProfileService.js`
- `frontend/src/modules/users/pages/EditStudent.jsx`, `UserDetails.jsx`, `userService.js`

### Core layout / context / navigation
- `frontend/src/core/navigation/navbar/Navbar.jsx` (scope selector)
- `frontend/src/core/navigation/sidebar/Sidebar.jsx` + `menuAggregator`
- `frontend/src/core/navigation/secondarySidebar/SecondarySidebar.jsx` (directory utility)
- `frontend/src/core/components/directory/DirectoryPage.jsx` (774 lines) + `directoryConfigs.js`
- `frontend/src/core/layouts/DashboardLayout.jsx`
- `frontend/src/core/contexts/DomainContext.jsx`, `ScopeContext.jsx`, `AcademicContext.jsx`, `StickySelectionContext.jsx`
- `frontend/src/core/stores/useScopeStore.js`
- `frontend/src/core/hooks/useUserScope.js`, `useScopeAwareUI.js`, `useServerTable.js`
- `frontend/src/core/router/AppRouter.jsx`, `ProtectedRoute.jsx`, `RouteGuard`, `routeRegistry.js`
- `frontend/src/core/query/queryClient.js`
- `frontend/src/core/api/apiClient.js` (scope header interceptor)

### Shared infra
- `frontend/src/core/components/`: VirtualList, DataTable, BulkActionBar, Skeleton, EmptyState, Toast, Drawer, ConfirmDialog, ErrorBoundary
- `frontend/package.json`, `vite.config.js`, `frontend/src/test/setup.js`, all 7 existing test files

## 2. How the global machinery actually works

- **Scope selector (navbar):** writes `{id, name, type}` to **DomainContext** + localStorage `capu_selected_scope_node`. ScopeModal also writes academic year/semester to AcademicContext. `useScopeStore` (zustand) is a **dead duplicate** — hydrated from the same localStorage key but written by nobody; only `GlobalContextPills` reads it.
- **Scope → server:** `apiClient` interceptor auto-attaches `X-StructureNode-Id`, `X-AcademicYear-Id`, `X-Semester-Id` headers to every request. This means **any cached response whose query key omits scope can serve wrong-scope data after a scope switch**.
- **Sidebar menu:** `buildMenu(can)` filters `routeRegistry` menu items via PermissionContext, which re-evaluates `permissionMap` when scope/year/semester change. Unmemoized (`buildMenu` runs every render). Permissions come from AuthContext at login — mutating someone's permissions does not refresh the *current admin's* own permission set.
- **Directory utility (secondary sidebar):** searches staff+students with `ScopeNodeId` param, pins selection via StickySelectionContext (localStorage `capu_pinned_user`), navigates students to `/admin/students/:id`. StudentDetailPage writes the pinned selection on load but **never after rename**.
- **RBAC pages:** zero React Query — all manual `useState`/`useEffect` fetching with explicit re-fetch after mutations, no AbortController, no scope in any dependency array → **changing scope while on RolesPage/PermissionsPage refetches nothing** (Scenario B failure).

## 3. Confirmed defects (verified first-hand or by line-referenced audit)

| # | Defect | Where |
|---|--------|-------|
| D1 | Directory row click navigates to `/admin/users/:id` for students instead of `/admin/students/:id` | DirectoryPage.jsx:428, 655 |
| D2 | Pinned sidebar selection goes stale after student rename (no write-back after `updateStudent`) | StudentDetailPage.jsx:44 + PersonalInfoTab |
| D3 | RBAC pages don't react to scope change; scope headers + scope-less manual caches can desync | RolesPage, PermissionsPage |
| D4 | `useScopeStore` duplicates DomainContext; GlobalContextPills reads the dead store | useScopeStore.js, GlobalContextPills.jsx |
| D5 | PermissionsPage user search includes **students**, but `/authorization/users/{id}/permission-tree` is staff-only → 404 `NotFoundException("User")` | PermissionsPage.jsx:121, 137 |
| D6 | `ACTION_LEVELS`/`ACTION_NAME_TO_LEVEL` tripled across 3 files | RolesPage:19, PermissionsPage:21, RolePermissionsModal:7 |
| D7 | Missing `key` on permission-level buttons | RolesPage:687, PermissionsPage:1040 |
| D8 | Dep-array bug `[permTree.length]` | RolesPage:162 |
| D9 | Debounce timer not cleaned on unmount | PermissionsPage:113–128 |
| D10 | `window.confirm` for bulk delete; no ConfirmDialog | DirectoryPage.jsx:256 |
| D11 | StudentProfileRecordsPage / StandaloneProfileRecordsPage ~95% duplicated | both pages |
| D12 | Sidebar `buildMenu(can)` unmemoized; own-permission mutations never refresh the menu | Sidebar.jsx:42 |
| D13 | No virtualization anywhere in RBAC/directory surfaces; role list hardcodes `pageSize: 200` | RolesPage:98 |
| D14 | PermissionGate **hides** controls instead of disabled-with-tooltip (violates Defensive UI standard) | throughout |

## 4. Strategy: binding Scope Selector + Directory Utility to the CRUD pages

1. **One scope source of truth.** Delete `useScopeStore`; migrate `GlobalContextPills` to `useDomain()`/`useAcademic()`. DomainContext stays authoritative (matches existing project decision).
2. **Scope-aware query key factory.** New `frontend/src/core/query/scopedKeys.js`: `useScopedKey(...parts)` → `[...parts, { scope: scopeNode?.id ?? null, year: selectedYearObj?.id ?? null, sem: selectedSemesterObj?.id ?? null }]`. Every list-shaped query (roles, role members, permission trees, student directory, user search) is built on it, so a scope change flips the key and React Query refetches automatically — matching the scope headers the interceptor sends. Entity-by-id queries (`["student", id]`) stay scope-free.
3. **React Query everywhere on these surfaces.** New hook modules `core/query/usePermissionsData.js` (useRoles, useRole, useRoleMembers, useRolePermissions, useUserPermissionTree, usePermissionAssignment + mutations with targeted `invalidateQueries`) and `core/query/useStudents.js` (useStudent, useStudentSearch, useStudentsDirectory + mutations). Mutations invalidate by key prefix; optimistic updates for bulk/toggle actions.
4. **Directory ↔ profile write-through.** After `updateStudent` succeeds: `queryClient.setQueryData(["student", id], updated)`, invalidate the scoped directory list, and re-call `select({id, name, code, type})` so the pinned card renames instantly (D2). Directory row click uses a new `config.routes.detail(id)` (students → `/admin/students/:id`) fixing D1; active row highlighted when `selected?.id === item.id`.
5. **Out-of-scope guardrails.** In PermissionsPage role assignment, compare the assignment's structural scope against the admin's allowed scope (`useUserScope`) and the active DomainContext node; show a warning banner + confirm step when assigning outside the active scope.
6. **Sidebar sync (Scenario C).** Memoize `buildMenu`; after `updatePermissionAssignment`/role-permission mutations, invalidate permission queries, and when the target user is the **current** user, refresh AuthContext permissions so PermissionContext → Sidebar → RouteGuard update immediately. (Other users pick changes up on next session/refresh — honest limit without a websocket push.)

## 5. Workstreams (implementation order)

### WS0 — Safety net (small, first)
Fix D7, D8, D9; extract `core/constants/permissionLevels.js` (D6). Pure low-risk cleanups.

### WS1 — Scope unification + query layer
- Delete `useScopeStore`, migrate GlobalContextPills (D4).
- Add `scopedKeys.js`, `usePermissionsData.js`, `useStudents.js`.
- Migrate DirectoryPage from manual JSON-string pseudo-key to `useQuery` on the scoped key (keeps server pagination).

### WS2 — Directory→Profile pipeline (Scenario A)
- `directoryConfigs.js`: add `routes.detail`; DirectoryPage uses it (D1).
- StudentDetailPage: skeleton loaders (SkeletonStats/SkeletonCard) instead of full-page spinner; `useStudent(id)` query; sticky-selection write-through on rename (D2); reset tab on id change.
- SecondarySidebar: highlight active result/pinned card from route param; student-type guard so pinned students never call staff-only endpoints (D5 surface).

### WS3 — RBAC overhaul (Scenarios B + C)
- Split RolesPage into `RoleList`, `RoleDetailPanel`, `RoleMembersTab`, `RoleCreateModal` (react-hook-form + zod), all on the new hooks.
- Split PermissionsPage: extract `useScopeModal` hook, `TemporalScopePicker`, `RoleAssignmentList`, `EffectivePermissionsMatrix`.
- New `PermissionMatrix` component: dense scrollable grid (modules × levels) with sticky header row + sticky resource column, virtualized rows via existing `VirtualList` for >100 resources (D13).
- PermissionsPage user search: restrict to staff or gracefully handle the student 404 with an explanatory empty state (D5).
- Out-of-scope assignment warning (Strategy §5).
- Defensive UI pass: replace hide-only PermissionGate usages on action buttons with `disabled` + tooltip (`title`/Radix tooltip) explaining the missing permission; keep hiding for whole sections (D14).
- Sidebar memoization + self-permission refresh (D12, Scenario C).

### WS4 — Profile records dedup
Extract shared `ProfileRecordCard`, `ProfileRecordFormModal` (react-hook-form + zod incl. JSON payload validation), `prettifyJson` util into `modules/studentProfileRecords/components/`; both pages become thin shells (D11).

### WS5 — High-volume + bulk (Scenario D)
- DirectoryPage: ConfirmDialog for bulk delete (D10), optimistic bulk activate/deactivate with rollback + toasts, BulkActionBar permission-gated (disabled+tooltip).
- Role members list + permission matrix rows virtualized.
- Keep server-side pagination as primary defense for 20k students (virtualize within page).

### WS6 — Tests & verification
- Vitest: directoryConfig detail-route resolution; scoped-key refetch on scope change (render hook, flip DomainContext, assert new fetch); sticky-selection rename write-through; permission matrix level cycling; role create modal zod validation; bulk action optimistic rollback on error.
- Browser verification of Scenarios A–D via preview tools against the dev API (port from `.env.development`; verify 5256 vs 5257 at run time).

## 6. Risks / notes
- React 19 + React Compiler babel plugin is enabled — avoid manual memo hacks that fight the compiler; prefer plain derived values.
- `frontend/src/test/setup.js` has an uncommitted Node-22 localStorage fix — must be preserved.
- Drawer already has the onCloseRef focus fix; reuse Drawer/ConfirmDialog rather than ad-hoc modals.
- Backend endpoints unchanged; everything here is frontend-only. Scope headers remain the server contract.
