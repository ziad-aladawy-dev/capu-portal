# University Module Audit Report

## 1. State, Context & Persistence
- **Technical Debt - State Management**: Unlike the `treasury` module, the `university` module uses a custom `useUniversityStructure` hook with manual `useState`/`useEffect` for data fetching instead of **React Query**.
    - **Critical Deficiency**: No automatic caching, no background refetching, and manual management of `loading`/`error` states. This is a significant deviation from the project's established patterns.
- **Selection Persistence**: Uses a `useRef` (`lastSelectedId`) to restore selection after a refresh. This is a manual workaround for the lack of a robust query-based cache.
- **Hard Refreshes**: A hard refresh loses the expanded state of the tree because `expandedNodes` is local component state.

## 2. Error Handling & Resilience
- **Hook Errors**: The `useUniversityStructure` hook catches errors and sets a string message.
    - **Deficiency**: It doesn't distinguish between network errors, 403 Forbidden (important for structure management), or 404 Not Found.
- **Validation**: `AddEditNodeModal.jsx` has basic client-side validation for the Arabic name.
    - **Deficiency**: It lacks validation for the English name or metadata fields beyond HTML5 primitives.
- **Breadcrumb Failure**: If `loadBreadcrumb` fails, it just logs to `console.error` and clears the breadcrumb. The user receives no feedback.

## 3. Architecture & Wiring
- **Recursive Rendering**: `TreeNode.jsx` correctly implements recursion.
- **Dependency Bottleneck**: `UniversityStructurePage.jsx` is quite large (300+ lines) and handles too many responsibilities: search logic, modal state management, breadcrumb loading, and action handling.
- **Contextual Actions**: Uses a `nodeTypeRegistry` for actions, which is a good architectural choice for extensibility.
- **Breadcrumb Loading**: Loading breadcrumbs as a separate `useEffect` call after the node is selected leads to a "popping" UI effect.
    - **Optimization**: Breadcrumbs should be included in the initial tree node payload or the "fetch by ID" response.

## 4. Code Quality & Tech Debt
- **Hardcoded Configuration**: `METADATA_INPUT_CONFIG` in `AddEditNodeModal.jsx` is hardcoded. Metadata fields should ideally be driven by the `nodeTypeRegistry` or a backend schema.
- **Inline Styles**: Multiple instances of inline `style` objects in `UniversityStructurePage.jsx` and `AddEditNodeModal.jsx` (e.g., `style={{ borderTop: ... }}`). This violates the project's styling conventions.
- **Logic Duplication**: `findNodeInTree` is redefined in `UniversityStructurePage.jsx` despite a similar function `findNodeById` existing in `useUniversityStructure.js`.
- **Drag and Drop**: The Drag & Drop implementation in `TreeNode.jsx` uses a `try/catch` block around `JSON.parse` but doesn't provide user feedback on invalid drops (e.g., when `canMoveToParent` returns false).
