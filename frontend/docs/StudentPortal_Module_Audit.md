# Student Portal Module Audit - Production Readiness Report

## 1. Executive Summary
The `studentPortal` module is a complex, feature-rich area of the application. It demonstrates good use of modern patterns like TanStack Query and Framer Motion. However, it suffers from brittle session management, inconsistent error handling, and several hardcoded business rules that should be abstracted.

## 2. File-by-File Analysis

### `routes.js`
- **Architecture:** Solid use of lazy loading.
- **Deficiencies:** Routing logic is heavily coupled with `permission` strings that are hardcoded throughout the array.

### `hooks/useDashboardData.js`
- **Brittle Scope Resolution:** `resolveScopeIds` relies on `sessionStorage` keys (`capu_selected_scope_node`, `capu_selected_semester`). If these keys change in the core, the portal dashboard breaks silently.
- **Silent Failures:** Multiple hooks (`useOpenRequests`, `useGradesSummary`, `useFinancialSnapshot`, `useUnreadNotifications`) swallow errors using `try/catch` and return empty arrays or `null`. This prevents the UI from showing error states and can mislead students (e.g., showing a balance of 0 when the API failed).
- **Magic Numbers:** `useOpenRequests` contains hardcoded status IDs: `const OPEN = new Set([2, 3, 4, 7, 10]);`. These should be imported from a central `REQUEST_STATUS` constant.

### `pages/RequestSubmission.jsx`
- **Complex State Management:** This file is over 20KB and manages massive local state for the wizard. It is a prime candidate for refactoring into a custom `useRequestWizard` hook.
- **Best-Effort Attachment Logic:** `seedAttachments` is documented as "Best effort", which is concerning for a production application where file uploads are critical.
- **Booting Logic:** The `useEffect` for booting the wizard is highly complex and handles both resuming and creating drafts. It relies on a `bootNonce` for retries, which is a bit of a hack.
- **Mock Gateway:** Hardcoded "Test gateway" logic in the payment step.

### `components/dashboard/DashboardHero.jsx`
- **Inconsistent Data Sources:** Attempts to use an aggregated dashboard API but falls back to multiple legacy queries if it fails. This logic is complex and prone to race conditions if not handled carefully by React Query.
- **Formatting Logic:** Inline `egp` formatter and `greetingKey` logic should be moved to utility functions.

### `components/widgets/DashboardGrid.jsx`
- **Layout Persistence:** Uses `useDashboardLayoutStore`. While good, the persistence layer (LocalStorage/SessionStorage) isn't explicitly audited for collision with other users on the same machine.

## 3. State, Context & Persistence
- **State Management:** Good use of TanStack Query for server state and Zustand (via `useDashboardLayoutStore`) for UI state.
- **Persistence:** Relies heavily on `sessionStorage` for academic context, which is lost on tab close. This may be intentional but can be frustrating for users.
- **Hard Refresh:** Most data survives a hard refresh because it's either in `sessionStorage` or refetched by React Query.

## 4. Error Handling & Resilience
- **Deficiency:** Large lack of explicit Error Boundaries.
- **API Resilience:** `useAggregatedDashboard` has `retry: 0`, which might be too aggressive for mobile users on unstable connections.
- **Silent Errors:** As noted in `useDashboardData.js`, swallowing errors is a pattern here that needs to be replaced with proper error state propagation to the UI components.

## 5. Code Quality & Tech Debt
- **Hardcoded Values:** Many status IDs, permission strings, and EGP currency strings are hardcoded.
- **Performance:** `RequestSubmission.jsx` is a large component that might benefit from code splitting or logic extraction to improve maintainability and render performance.
- **CSS Modules:** Good use of CSS Modules for styling, which prevents leakage.

## 6. Recommendations
1. **Abstract Hardcoded Constants:** Move all status IDs and permission strings to a shared constants layer.
2. **Improve Error Visibility:** Stop swallowing errors in hooks. Allow React Query to bubble them up to components that can render `PortalEmptyState` with error details.
3. **Refactor Wizard Logic:** Break down `RequestSubmission.jsx` into smaller, more manageable components and a dedicated hook for state management.
4. **Centralize Scope Logic:** Move `resolveScopeIds` into a core service or hook so the portal doesn't need to know about `sessionStorage` keys.
