# Core Directory Audit Report: CAPU Portal Frontend

## 1. State, Context & Persistence
*   **AuthContext.jsx & authService.js**:
    *   **Deficiency**: The initial session restoration in `AuthProvider`'s `useEffect` logs the user out (`api.clearTokens()`) on any failure of `getCurrentUser()`. This includes transient network errors or server downtime, leading to a poor user experience.
    *   **Risk**: Hardcoded `STUDENT_PORTAL_RESOURCES` in `authService.js` creates a maintenance burden. If the student portal expands, permissions must be manually updated in code rather than via the backend.
    *   **Persistence**: Reliance on `localStorage` for tokens and `sessionStorage` for scope is consistent, but direct access to these globals is scattered across components instead of being abstracted through a single storage service.
*   **AcademicContext.jsx**:
    *   **Issue**: `selectedYear` defaults to a string "—" in the provider value, which can cause runtime errors in consumers expecting a null or an object with an `id`.
    *   **Resilience**: `fetchWithRetry` uses hardcoded delays and retry counts. These should be configurable or moved to a global constants file.

## 2. Error Handling & Resilience
*   **apiClient.js**:
    *   **Bug**: The token refresh logic uses module-level variables (`isRefreshing`, `failedQueue`). In a multi-tab environment or under rapid concurrent requests, this can lead to race conditions where the queue is processed incorrectly.
    *   **Deficiency**: Redirection logic in the 401 interceptor is hardcoded to `/admin/login`, potentially bouncing student users to the wrong portal.
*   **ErrorBoundary.jsx**:
    *   **Observation**: Implementation is solid with Sentry integration, but it lacks localized error messages, defaulting to English even when the app is in Arabic mode.

## 3. Architecture & Wiring
*   **Query Hooks (`useStudents.js`, etc.)**:
    *   **Debt**: Hardcoded `staleTime` and `pageSize` values are prevalent. These should be centralized to ensure consistent caching behavior across the app.
    *   **Coupling**: `useStudents.js` (in core) imports from `../../modules/users/services/userService`. This breaks the "core" abstraction by depending on a specific feature module.
*   **PermissionContext.jsx**:
    *   **Technical Debt**: The `permissionMap` is regenerated on every scope change. While functional, for users with hundreds of permissions, this could cause noticeable lag on lower-end devices during navigation.

## 4. Code Quality & Tech Debt
*   **DataTable.jsx**:
    *   **Critical Debt**: Extreme reliance on inline styles (e.g., `background: "#f8f9fc"`, `color: "#1a1f5e"`) instead of CSS variables or tokens makes the component difficult to theme and maintain.
    *   **Accessibility**: While `aria-label` is present, complex interactions like indeterminate checkboxes for selection lack full keyboard navigation support.
*   **authService.js**:
    *   **Unsafe Logic**: Use of `atob()` for JWT decoding is unsafe for non-ASCII characters. A robust library or a more resilient helper should be used.
    *   **Hardcoded Mapping**: `ACTION_LEVEL_MAP` translates backend strings to magic numbers (1-5). This mapping is brittle and hidden inside a service rather than being a core constant.

## Summary of Recommendations
1.  **Refactor Token Refresh**: Move refresh state into a managed singleton or synchronized lock mechanism to prevent race conditions.
2.  **Abstract Storage**: Create a `storageService.js` to wrap `localStorage`/`sessionStorage` with error handling and type safety.
3.  **Styling Modernization**: Transition `DataTable.jsx` and other core components from inline styles to CSS Modules or the project's token system.
4.  **Decouple Core**: Move feature-specific query hooks out of `core/query` if they depend on `modules/`.