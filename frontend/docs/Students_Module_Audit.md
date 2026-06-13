# Students Module Audit

## 1. State, Context & Persistence
- **React Query Usage:** The module heavily relies on `@tanstack/react-query` for state management, which is good for caching and synchronization.
- **Sticky Selection:** `StudentDetailPage` uses `useStickySelection` to keep the sidebar in sync. This is a good practice for UX in management interfaces.
- **Tab State:** `StudentDetailPage` uses local `useState` for `activeTab`. This state is lost on hard refresh, resetting the user to the "Overview" tab. Consider using URL search params or path segments for persistent tab state.
- **Manual Payload Building:** `buildUpdatePayload` in `StudentDetailPage.jsx` manually constructs the payload for updates. This is fragile; adding a new field to the backend requires updating this frontend mapping, or data might be lost during a PUT request that expects the full entity.

## 2. Error Handling & Resilience
- **Pending Endpoints:** `StudentAcademicHubPage.jsx` has a `isPendingEndpoint` helper that treats 404 or 501 as "feature pending". While helpful during development, this might mask real 404 errors (e.g., if a student is actually deleted but the UI still tries to fetch their academic data).
- **Graceful Failures:** Side-queries (fees, orders, records) in `StudentDetailPage` use `retry: false` and handle errors by defaulting to empty arrays. This prevents the whole page from crashing if a secondary service is down.
- **Toast Feedback:** Mutations (update, delete, toggle status) use `useToast` for user feedback, which is consistent.
- **Validation:** The "Quick Edit" form in `OverviewTab` lacks client-side validation before submission. It relies on the backend to return error messages, which are then displayed via toasts.

## 3. Architecture & Wiring
- **Component Bloat:** `StudentDetailPage.jsx` is quite large (over 500 lines) and contains many sub-components (`OverviewTab`, `FinanceTab`, etc.) within the same file. These should be extracted into separate files in a `components/` directory for better maintainability.
- **Hardcoded Maps:** `RESULT_STATUS`, `ORDER_STATUS_PILL`, `ORDER_STATUS_LABEL`, etc., are hardcoded in the component files. These should be moved to a constants file or shared service.
- **Dependency on Core:** The module has deep dependencies on `../../../core/components/...`. While this is expected for a shared design system, it makes the module less portable.

## 4. Code Quality & Tech Debt
- **Manual Formatting:** Formatting functions like `fmt` in `PeopleDashboardPage.jsx` and various `fmt...` utils in `StudentDetailPage.jsx` are used. Ensure these are consistent with a global localization/formatting strategy (e.g., `Intl` API).
- **Magic Strings:** Labels like `"navy"`, `"green"`, `"gold"`, etc., for tones are used throughout. These should ideally be part of a theme or enum.
- **Echoing Values:** The comment in `StudentDetailPage.jsx` notes that the update endpoint "requires a valid Level structure node" and "partial edit must echo current values". This suggests a "God Object" update pattern on the backend which is a known source of race conditions and technical debt.
- **Hardcoded Field Lists:** The `fields` array in `OverviewTab` save function is hardcoded. This duplicates the logic of what is editable.
- **Formatting Fallbacks:** `localizedName || "S"` is a common pattern for initials. Ensure this handles cases where `localizedName` might be an empty string or contain only whitespace.

## Summary of Critical Issues
1. **Maintainability:** `StudentDetailPage.jsx` needs refactoring to separate concerns.
2. **Persistence:** Tab state in `StudentDetailPage` is not persistent across refreshes.
3. **Fragility:** `buildUpdatePayload` is a maintenance bottleneck.
4. **Error Handling:** `isPendingEndpoint` logic should be reviewed for production to avoid masking real errors.
