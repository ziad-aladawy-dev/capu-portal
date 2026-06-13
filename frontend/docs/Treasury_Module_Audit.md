# Treasury Module Audit Report

## 1. State, Context & Persistence
- **Pinned Student Persistence**: The module uses `useStickySelection` to persist the active student across page navigations. While effective for UX, it creates a tight coupling with the sidebar/pinning mechanism.
- **Recent Students**: `recentStudents.js` uses `localStorage` to store the last 5 searched students.
    - **Deficiency**: There is no validation of the data structure upon retrieval beyond a basic `Array.isArray` check. Corruption in `localStorage` could lead to runtime errors in `StudentRow`.
    - **Deficiency**: The `catch` blocks in `recentStudents.js` are empty, providing no feedback if `localStorage` is full or disabled.
- **Order Polling**: `OrderDetailDrawer.jsx` relies on `useOrder` (React Query) which likely handles polling.
    - **Risk**: If the browser is refreshed during a `PendingPayment` state, the UI relies entirely on the server-side state. The logic to "recover" the payment link is present, which is a good resilience measure.

## 2. Error Handling & Resilience
- **API Call Wrappers**: Most mutations use `apiErrorMessage` to format errors, which is good for consistency.
- **Component-Level Catching**: `OrderDetailDrawer` has dedicated `actionError` state for initiate/cancel operations.
- **Missing Error Boundaries**: There are no explicit React Error Boundaries around complex components like `StudentPicker` or the `DataTable` in `BillingSetupPage`. A failure in a render helper (like `fmtAmount` with an invalid input) would crash the entire page.
- **Copy to Clipboard**: `useCopy` hook handles clipboard failures gracefully with a toast.

## 3. Architecture & Wiring
- **Component Reuse**: High reuse of `Drawer`, `StatusBadge`, and `DataTable`.
- **Prop Drilling**: `OrderDetailDrawer` receives `receiptIndex` as a prop.
    - **Optimization**: This index could be derived via a custom hook or selector to avoid passing it through multiple layers if the hierarchy grows.
- **Dead Code / Mock Data**: 
    - `GATEWAYS` in `TreasuryHubPage.jsx` is hardcoded. While these might be fixed integrations, it's technically "configuration as code" that might belong in a service or config file.
    - `ORDER_FILTERS` are hardcoded in the page component.

## 4. Code Quality & Tech Debt
- **Hardcoded Fallbacks**: 
    - `EGP` is hardcoded as a fallback currency in `sumByCurrency` and `TreasuryHubPage`. This should be fetched from a global configuration or the active organization settings.
    - `API_ORIGIN` in `profileUtils.js` defaults to `http://localhost:5256` if the env variable is missing. This is dangerous for production builds if the environment isn't perfectly configured.
- **Render-time State Adjustment**: `TreasuryHubPage.jsx` uses the pattern `if (builderStudentId !== activeStudentId) { setBuilderStudentId(activeStudentId); ... }`. While valid React, it can be confusing.
- **Performance**: `sumByCurrency` and other calculations in `TreasuryHubPage` run on every render.
    - **Optimization**: These should be wrapped in `useMemo` (though currently some are, others could be tighter).
- **CSS**: Relies on a module-specific `treasury.css`.
    - **Risk**: Potential for class name collisions with other modules as it doesn't appear to use CSS Modules.
