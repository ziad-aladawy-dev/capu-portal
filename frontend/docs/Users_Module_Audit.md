# Users Module Audit Report

## 1. State, Context & Persistence
- **Technical Debt - Inconsistent State Management**: 
    - `useUsers.js` uses manual `useState` and `useEffect` for fetching student/staff lists.
    - `UserDetails.jsx` uses **React Query** (`useQuery`).
    - **Result**: Inconsistent behavior regarding caching and state synchronization. If a user is updated in the "Edit" page, the "Management" list won't reflect the change until a manual refresh or a re-mount, because the manual hook doesn't share a cache with React Query.
- **Filters Persistence**: Filters in `useUsers` are lost on hard refresh. They should ideally be synced with URL search parameters.

## 2. Error Handling & Resilience
- **Service Mocks**: `userService.js` contains multiple "mock" functions that always return `{ isUnique: true }` or `{ success: true }`.
    - **Critical**: This is a major blocker for production. Real uniqueness checks for Email and National ID are missing.
- **Bulk Actions**: `bulkActivateUsers` uses `Promise.allSettled`, which is good for resilience, but the UI feedback is minimal (just a toast showing counts).
- **Import Errors**: `BulkImportModal.jsx` has an `error` state but doesn't provide a way to download an "error log" if only 5 out of 100 rows fail.

## 3. Architecture & Wiring
- **Code Duplication**: `AddStaff`/`AddStudent` and `EditStaff`/`EditStudent` share ~80% of their logic and JSX.
    - **Refactoring Needed**: These should be unified into a single `UserForm` component.
- **Confusing Routing**: `UserDetails.jsx` contains `StaffDetailContent` but then probes for students to redirect them.
    - **Architecture Flaw**: This suggests the "Users" module isn't truly polymorphic. There should be a unified `User` entity or more distinct separation from the start.
- **ProfileKit**: The use of a "Kit" for profile building blocks is a positive architectural pattern, promoting visual consistency.

## 4. Code Quality & Tech Debt
- **Hardcoded Mocks**: `userService.getRoles` returns a hardcoded array. Roles should be managed via a dedicated API/Module.
- **Env Variable Fallbacks**: `resolvePhotoUrl` and `EditStaff`/`EditStudent` hardcode `http://localhost:5256`. This will break in any environment where the API isn't on that specific port.
- **Magic Strings**: Extensive use of magic strings for roles (e.g., `"Professor"`, `"AdminStaff"`) and user types (`"Student"`, `"Staff"`).
- **Template Generation**: `BulkImportModal.jsx` generates CSV templates by hardcoding headers in a JavaScript string. This is fragile and hard to maintain as the schema evolves.
- **Password Generation**: `ResetPasswordModal` uses `crypto.getRandomValues` which is good, but the `PASSWORD_CHARS` set is hardcoded.
