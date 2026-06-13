# Sync Module Audit

## 1. State, Context & Persistence
- **Manual State Fetching:** Like the Student Services module, this module uses manual `useState` and `useEffect` for data fetching. It lacks the robust caching and revalidation features of React Query.
- **Missing Polling:** Syncing is typically a long-running background process. The current implementation only refreshes the history once immediately after triggering a sync. There is no background polling to update the status while a sync is "running".
- **Volatile State:** Errors and syncing status are held in local state and lost on refresh.

## 2. Error Handling & Resilience
- **Silent 404s:** `loadHistory` and `handleSync` both ignore 404 errors. While this may be a developer-centric way to handle "not implemented" endpoints, it can confuse users if the sync service is legitimately missing or misconfigured.
- **Lack of Feedback:** Successful actions (Unlock, Delete) don't provide any user feedback (like a toast) other than the table refreshing.
- **No Confirmation:** The "Delete" and "Trigger Sync" actions lack a confirmation dialog, which is dangerous for administrative tasks.

## 3. Architecture & Wiring
- **Permission Model Inconsistency:** This module uses a `PermissionGate` component with a `minLevel` prop (integer-based). Other modules use a string-based permission check via `usePermission().can('string.permission')`. This inconsistency in authorization logic is a significant technical debt.
- **Direct API Client Usage:** The page calls `api.get` and `api.post` directly instead of using a dedicated service file (e.g., `syncService.js`). This violates the project's pattern of isolating API logic.
- **Inline Logic:** Summary statistics (total, succeeded, failed) are calculated in the render body. For larger datasets, this should be memoized or handled by the backend.

## 4. Code Quality & Tech Debt
- **Hardcoded Styling:** Status colors and icons are hardcoded in the component. These should be centralized in a theme or constants file.
- **Incomplete Features:** The "Edit Config" button has an empty `onClick` handler, indicating unfinished functionality.
- **Spinning Animation:** The "spinning" class for the sync icon is used but its CSS definition is likely in an external file (`sync.css`).
- **Date Handling:** Uses `new Date().toLocaleString()` which can lead to "Hydration Mismatch" errors in SSR environments (though this seems to be a pure SPA) and is generally less flexible than a library like `date-fns` or the project's own `fmt...` utils.

## Summary of Critical Issues
1. **Authorization:** Standardize the permission model to match the rest of the application.
2. **Service Layer:** Move API calls to a `syncService.js` file.
3. **UX Resilience:** Implement background polling for active sync processes and add confirmation dialogs for destructive actions.
4. **Consistency:** Migrate to React Query for state management.
