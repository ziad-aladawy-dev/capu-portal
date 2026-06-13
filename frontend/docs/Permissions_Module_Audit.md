# Audit Report: Permissions Module
**Status:** 🛑 Critical Tech Debt (Complexity & Maintainability Crisis)
**Date:** 2024-05-22
**Auditor:** Senior Frontend Architect

## 1. State, Context & Persistence
- **Complexity Overload:** `PermissionsEditor.jsx` (41KB) and `RolesPage.jsx` (28KB) manage an immense amount of local state. This makes debugging nearly impossible as state transitions are scattered across dozens of handlers.
- **State Derived from Props:** `buildInitialState` is called only on mount via `useState` initializer. If the `tree` or `assignment` props update without a component remount (facilitated currently by a `key` on the parent), the state will be out of sync. This is a fragile pattern.
- **React Query Usage:** Good use of React Query for data fetching, providing better caching and synchronization than the other audited modules.
- **Persistence:** Selections (active tab, selected role, selected user) are not persisted in the URL, hindering deep-linking and user productivity.

## 2. Error Handling & Resilience
- **Manual Diffing Logic:** `handleSave` in `PermissionsEditor.jsx` manually diffs state to determine what to add/remove. This logic is highly complex and prone to edge-case bugs (e.g., handling overlaps between role-based permissions and direct overrides).
- **Network Resilience:** Good use of mutation hooks with `onSuccess`/`onError` handlers and toast notifications.
- **Validation:** Basic validation is present (e.g., role name length), but deeper business logic validation (e.g., circular role dependencies) is handled only on the backend.

## 3. Architecture & Wiring
- **Monolithic Components:** `PermissionsEditor.jsx` is a "God Component". It handles:
  - Role assignment logic
  - Permission override logic
  - Structural scope selection (modals)
  - Temporal scope selection
  - Search/Filtering
  - Data normalization
  It must be decomposed into at least 5-6 smaller components.
- **Prop Drilling:** High degree of prop drilling. Consider a dedicated context for the Permissions Editor state.
- **Hardcoded Service Calls:** `handleSearchInput` in `PermissionsPage.jsx` is hard-wired to both `staffService` and `studentService`.

## 4. Code Quality & Tech Debt
- **Inline Styles:** `PermissionsPage.jsx` is severely cluttered with inline styles for layout and typography.
- **Hardcoded Constraints:** `pageSize: 10` is hardcoded in multiple search functions.
- **Logic Duplication:** `normalizeTemporal` logic and `needLevelTitle` logic are repeated.
- **Performance:** 
  - `roleScopeNodes` and `outOfScopeAssignments` in `PermissionsEditor` are expensive `useMemo` calculations.
  - The `PermissionMatrix` virtualization is a good performance optimization, but the underlying state management for it (in the parent) is heavy.
- **CSS Fragmentation:** Imports multiple CSS files (`permissions.css`, `roles.css`, `permissionMatrix.css`) which often have overlapping or conflicting styles.

## Final Verdict
**NOT PRODUCTION READY.** While highly functional, the Permissions module is a maintainability nightmare. The 41KB `PermissionsEditor` must be refactored and decomposed. The excessive inline styling violates the project's architectural standards.
