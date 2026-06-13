# Capu Portal — Frontend Architecture Interview Guide

## 1. Project Overview

Capu Portal is a university management SPA (React + Vite) with two distinct portals:

- **Admin portal** (`/admin/*`) — dashboard layout with sidebar/header for staff roles
- **Student portal** (`/student/*`) — portal layout for student self-service

Both share the same core infrastructure (auth, API client, scoping, permissions, React Query).

---

## 2. Tech Stack

| Layer | Library | Rationale |
|-------|---------|-----------|
| UI | React 18 | Stable, ecosystem |
| Build | Vite | Fast HMR, ESM-native |
| Routing | react-router-dom v6 | Nested routes, lazy loading |
| Server state | @tanstack/react-query v5 | Caching, stale-while-revalidate, mutation invalidation |
| Client state | Zustand (one store) + React Context | Minimal boilerplate vs Redux; Context for auth/scope |
| Forms | react-hook-form + zod | Performant (uncontrolled), type-safe validation |
| Styling | CSS Modules + custom properties | Scoped styles + token-based theming |
| i18n | i18next + react-i18next | Translation + RTL support |
| HTTP | Axios | Interceptors for token refresh + scope attachment |
| Tables | TanStack Table (headless) | Flexible, render-prop-based data tables |

---

## 3. State Management Architecture

The app uses **four distinct state management strategies**, chosen by concern:

| Concern | Tool | Why |
|---------|------|-----|
| **Server data** (students, courses, plans, etc.) | React Query | Caching, dedup, background refetch, mutation invalidation |
| **Auth & permissions** | useReducer + Context | Complex state with multiple transitions, globally needed |
| **Scope selection** (node/year/semester) | useState + Context | UI state persisted to sessionStorage, consumed globally |
| **Dashboard layout** (widget order) | Zustand | Pure client preference, no server counterpart |
| **Form state** | react-hook-form | Local, scoped to component tree |
| **Column visibility** | Custom hook + localStorage | Per-table UI preference |

### React Query — Cache Key Strategy

Scope-dependent queries use `useScopeKeyPart()` to append the active structural/temporal scope to the query key:

```js
const scopePart = useScopeKeyPart();
useQuery({
  queryKey: ["students", scopePart, filters],
  queryFn: () => studentService.list(filters),
});
```

This prevents stale data from one scope leaking into another. On scope change, all dependent queries automatically refetch.

Mutations systematically invalidate related query keys via `onSuccess`:

```js
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: ["students"] });
  queryClient.invalidateQueries({ queryKey: ["courses"] });
};
```

---

## 4. Authentication System

### 4.1 AuthContext (useReducer)

Actions: `AUTH_START`, `AUTH_SUCCESS`, `AUTH_FAILURE`, `AUTH_LOGOUT`, `AUTH_LOADED`

State shape:
```js
{
  user, permissions[], authorizedScopes, activeScope,
  isAuthenticated: bool, isLoading: bool, error,
  passwordExpiryDate, requiresPasswordChange,
}
```

### 4.2 Login Flow

1. `LoginForm` calls `authService.login(identifier, password)`
2. POST `/auth/login` → receives `{ token, refreshToken, user, permissions, ... }`
3. `api.setToken()` stores `accessToken` in localStorage
4. `api.setRefreshToken()` stores `refreshToken` in localStorage
5. `queryClient.clear()` — fresh login must not inherit previous user's cached data
6. `localStorage.removeItem("capu_pinned_user")` — clear sticky selection
7. Dispatches `capu:auth-changed` custom event (StickySelection listens)
8. Dispatches `AUTH_SUCCESS`

### 4.3 Logout Flow

1. POST `/auth/logout` (fire-and-forget, error-safe)
2. `api.clearTokens()` removes `accessToken`, `refreshToken`, `REFRESH_LOCK_KEY`
3. `queryClient.clear()`
4. Removes `capu_pinned_user`
5. Dispatches `capu:auth-changed`
6. Navigates to login page with optional `?session=<reason>`

### 4.4 Bootstrap (Page Load)

1. On mount, check `api.getToken()` (reads localStorage)
2. No token → dispatch `AUTH_LOADED` (unauthenticated)
3. Token exists → GET `/auth/me`
4. Success → dispatch `AUTH_SUCCESS`
5. Failure → clear tokens, dispatch `AUTH_LOADED`

`api.setOnUnauthorized(handleLogout)` wires the 401 interceptor to terminate the session.

### 4.5 Token Refresh

- Axios response interceptor on 401
- Uses localStorage lock (`capu_refresh_lock` with 10s TTL) for cross-tab synchronization
- POST `/auth/refresh` with stored refresh token
- On success: store new tokens, retry original request
- On failure: clear everything, call `onUnauthorizedCallback`
- Special case `session_revoked`: immediate logout without refresh attempt

---

## 5. Authorization & Permissions

### 5.1 Permission Level Model

| Level | Name | Meaning |
|-------|------|---------|
| 0 | None | No access |
| 1 | View | Read-only |
| 2 | Insert | Can create |
| 3 | Edit/EditClose | Can modify |
| 4 | Open | Can open/approve |
| 5 | Delete | Can delete |

### 5.2 Permission Resolution

`authService.resolvePermissions()` transforms API response:

1. `transformApiPermissions()` maps `module.resource.action` → `{ resource: "module.resource.action", level: 0-5, scope }`
2. **Student special case**: If permissions list is empty AND JWT has `"Student"` role claim, synthesizes level-5 permissions for all student portal resources (`student.dashboard`, `student.profile`, etc.)

### 5.3 PermissionContext

Consumes `useAuth()` + `useDomain()` + `useAcademic()`.

**`matchScope(permission)`** — A permission is only active if:
- Structural: no scope, `isGlobalStructural`, or `structureNodeId === currentScopeNode.id`
- Year: no scope, `isGlobalYear`, or `academicYearId === selectedYearObj.id`
- Semester: same pattern

**`can(resource, minLevel = 1)`**:
- Student and resource starts with `"student."` → true
- Otherwise checks `hasRequiredLevel(getLevel(resource), minLevel)`

### 5.4 PermissionGate

```jsx
<PermissionGate resource="users.users.edit" minLevel={3} fallback={<Restricted />}>
  <EditButton />
</PermissionGate>
```

### 5.5 Route Protection

- **RouteGuard** (component-level): If unauthorized → redirect to login; if permission missing → redirect to fallback
- **ProtectedRoute** (older, route-group-level): Checks `isAuthenticated` and `allowedRoles`, renders `<Outlet />`

---

## 6. Routing System

### 6.1 Route Registration

Each module exports a route config array:

```js
{
  path: "/admin/dashboard",
  component: AdminDashboard, // lazy-loaded via React.lazy
  permission: "dashboard.dashboard.view",
  pageType: "management",
  menuItem: { category: "Admin", label: "Dashboard", icon: "LayoutDashboard" },
}
```

### 6.2 Route Assembly

`buildProtectedRoutes()` in `routeRegistry.js`:
1. Iterates all module route configs
2. Wraps each `component` in `<RouteGuard resource={permission}>`
3. Returns configs consumed by `AppRouter`

### 6.3 Lazy Loading

All page components use `React.lazy()` + `<Suspense>` — each module is a separate chunk.

### 6.4 Provider Hierarchy

```
App
└── AppRouter (react-router-dom v6)
     ├── Public routes (landing, login, reset-password)
     └── DashboardLayout (admin/*)
          └── AuthProvider
          └── AcademicProvider (year/semester)
          └── DomainProvider (structural scope)
          └── PermissionProvider (depends on Auth + Domain + Academic)
          └── StickySelectionProvider (pinned user)
               └── SessionTimeoutWarning
               └── Sidebar / Navbar / <Outlet />

     └── StudentPortalLayout (student/*)
          └── (same providers)
          └── StudentBlockerGate
               └── <Outlet />
```

---

## 7. API Client (apiClient.js)

Axios instance with `VITE_API_BASE_URL`.

**Request interceptor** (every request):
- `Authorization: Bearer <token>`
- `Accept-Language: <i18nextLng>`
- Scope context from sessionStorage as query params + headers:
  - `ScopeNodeId`, `AcademicYearId`, `SemesterId` (query)
  - `X-StructureNode-Id`, `X-AcademicYear-Id`, `X-Semester-Id` (headers)

**Response interceptor**:
- Token refresh on 401 with cross-tab lock coordination
- Queues concurrent requests during refresh

---

## 8. Scoping Model (Three Dimensions)

| Dimension | Context | Storage | Key |
|-----------|---------|---------|-----|
| Structural (university node) | DomainContext | sessionStorage | `capu_selected_scope_node` |
| Temporal — Year | AcademicContext | sessionStorage | `capu_selected_academic_year` |
| Temporal — Semester | AcademicContext | sessionStorage | `capu_selected_semester` |

### 8.1 Scope Selection

- **ScopeModal**: UI for all three dimensions → tree picker for structural, dropdowns for year/semester
- **ScopeTreeModal**: Lighter version for structural only

### 8.2 Scope Persistence

- All three use **sessionStorage** (lost on tab close)
- AcademicContext has a two-step hydrate: read saved IDs into refs, then match against fetched API data

### 8.3 StickySelection — Fourth Dimension

- `StickySelectionContext` — "working on behalf of" pinned user
- Uses **localStorage** `capu_pinned_user` (survives page refresh)
- Auto-clears on login/logout via `capu:auth-changed` custom event
- Used in user/staff management pages

### 8.4 Scope-Aware Hooks

- `useScopeKeyPart()` → generates `{ scope, year, sem }` for React Query cache keys
- `useScopeAwareUI()` → derives node capabilities, preferred tabs, relevant actions

---

## 9. Frontend Storage Inventory

### localStorage (survives sessions)

| Key | Purpose |
|-----|---------|
| `accessToken` | JWT bearer token |
| `refreshToken` | JWT refresh token |
| `capu_refresh_lock` | Cross-tab refresh coordination |
| `capu_refresh_error` | Cross-tab refresh failure signal |
| `capu_pinned_user` | StickySelection pinned user |
| `capu_dashboard_layout` | Dashboard widget order/visibility |
| `capu_secondary_sidebar_open` | Sidebar collapsed state |
| `capu_portal_drawer_collapsed` | Student portal drawer state |
| `capu_colvis_<tableId>` | Per-table column visibility |
| `i18nextLng` | Language preference |
| `secPinnedModuleUsage` | Module usage counts for sidebar sorting |
| `capu_cmd_recent` | Recent command palette searches |
| `capu_pending_order` | In-progress payment order ID |
| `treasury.recentStudents` | Recent treasury student lookups |
| `capu_wishlist` | Student registration wishlist |

### sessionStorage (cleared on tab close)

| Key | Purpose |
|-----|---------|
| `capu_selected_scope_node` | Current structural scope |
| `capu_selected_academic_year` | Current academic year |
| `capu_selected_semester` | Current semester |

---

## 10. Zustand Usage

Only one store in the entire app: `useDashboardLayoutStore`

```js
{ widgetOrder, hiddenWidgets, customizing }
```

Why Zustand here and not Context or React Query:
- Pure **client-side** UI preference (no server state)
- Needs localStorage persistence
- Used by multiple non-nested components (no Context needed)
- Too small for Redux, but global enough to justify a store

---

## 11. Internationalization (i18next)

- **Setup**: `i18next` + `react-i18next` + `LanguageDetector`
- **Detection**: Browser language + localStorage (`i18nextLng` key)
- **RTL**: Arabic triggers RTL layout via `dir="rtl"` attribute
- **API integration**: `Accept-Language` header attached by apiClient interceptor
- **Module structure**: Each module has `locales/ar.json` and `locales/en.json`
- **Pattern**: `useTranslation()` hook → `t("key")` or `<Trans>` for JSX-within-translations

---

## 12. Key Design Patterns

### 12.1 Module Isolation

Each module (students, treasury, academic, admin) is self-contained with:
- `routes.js` — route configs for `routeRegistry`
- `pages/` — page-level components (lazy loaded)
- `components/` — shared within module
- `locales/` — i18n translation files
- `services/` — API service functions
- `hooks/` — module-specific hooks

### 12.2 Error Handling

- **ErrorBoundary**: React class component wrapping route content, sends to Sentry
- **SessionTimeoutWarning**: Idle detection with countdown modal, calls `logout("timeout")`
- **StudentBlockerGate**: Blocks student routes if password change or profile completion is required
- **API errors**: Handled per-mutation with `onError` callbacks and toast notifications

### 12.3 Tables

TanStack Table (headless) with:
- Server-side pagination, sorting, filtering
- Per-table column visibility persisted to localStorage (`capu_colvis_<tableId>`)
- Permission-aware column rendering (hide edit/delete columns if user lacks level)

### 12.4 Forms

- `react-hook-form` for uncontrolled inputs (avoids re-renders)
- `zod` schemas for type-safe validation
- `useForm` + `zodResolver` pattern

### 12.5 Custom Events for Cross-Context Communication

The app uses `window.dispatchEvent(new CustomEvent(...))` as a lightweight pub/sub mechanism:
- `capu:auth-changed` — emitted on login/logout, consumed by StickySelection to clear pinned user
- Avoids adding a full event bus library for a single cross-cutting concern
