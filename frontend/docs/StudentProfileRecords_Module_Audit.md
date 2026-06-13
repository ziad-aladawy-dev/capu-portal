# Student Profile Records Module Audit - Production Readiness Report

## 1. Executive Summary
The `studentProfileRecords` module provides critical functionality for managing sensitive student data. While it utilizes modern validation (Zod) and state management (TanStack Query), it has significant technical debt in the form of hardcoded permission levels and raw JSON manipulation in the UI, which may not be suitable for all administrative users.

## 2. File-by-File Analysis

### `components/ProfileRecordsPanel.jsx`
- **Hardcoded Permissions:** The resource name `"student-information.profile-records"` and permission levels `(2, 3, 5)` are hardcoded. This is a significant maintenance risk if the permission system is refactored.
- **Error Handling:** 
    - `recordsQuery.error?.message` is accessed directly without a fallback in the error banner.
    - Errors from `upsertMutation`, `verifyMutation`, and `deleteMutation` are handled by setting a local `error` or `serverError` state, but there is no mechanism to clear these automatically or handle multiple simultaneous errors.
- **UI/UX:** Sensitive records are marked with a Lock icon, but the data is displayed as raw JSON via `prettifyJson(r.dataJson)`. This is highly technical and prone to rendering issues if the JSON is extremely large.

### `components/ProfileRecordFormModal.jsx`
- **JSON Validation:** Uses a Zod `refine` to validate JSON strings. This is functional but the UX of editing raw JSON in a `textarea` is poor and prone to syntax errors by non-technical staff.
- **Zod Schema:** The schema is defined inside the component on every render. While not a massive performance hit, it's better practice to define it outside.
- **Type Coercion:** Uses `z.coerce.number()` for `category` and `schemaVersion`. This can lead to unexpected '0' or 'NaN' values if not carefully handled.

### `pages/StudentProfileRecordsPage.jsx`
- **Architecture:** This is a simple wrapper page. It correctly uses `useParams` to pass the `studentId` to the panel.
- **Wiring:** Uses `navigate(-1)` for the back button, which can sometimes lead to unexpected behavior if the user arrived via a direct link or external site.

## 3. State, Context & Persistence
- **Server State:** Excellent use of TanStack Query for fetching, caching, and invalidating records.
- **Local State:** Uses standard `useState` for modal management and error tracking.
- **Persistence:** No local persistence beyond TanStack Query's cache. Hard browser refreshes will trigger a full refetch.

## 4. Error Handling & Resilience
- **Deficiency:** No module-level or component-level `ErrorBoundary`. A failure in `prettifyJson` or JSON parsing during render could crash the entire page.
- **Defensive Programming:** `ProfileRecordsPanel` correctly handles the `!studentId` case.

## 5. Code Quality & Tech Debt
- **JSON-as-Data:** The module relies on manual JSON entry and display. This is a "power user" feature that lacks the polish of a dedicated form builder or structured data viewer.
- **Hardcoded Logic:** `RESOURCES` and permission levels should be centralized.
- **Consistency:** Uses a mix of inline styles and CSS files (`studentProfileRecords.css`) for layout and error handling.

## 6. Recommendations
1. **Centralize Permissions:** Replace hardcoded strings and numbers with constants from a core policy file.
2. **Improve JSON Editing:** Consider using a proper JSON editor component (like `react-json-view` or a controlled code editor) instead of a raw `textarea`.
3. **Add Error Boundaries:** Specifically around the JSON rendering and the records grid.
4. **Refactor Zod Schemas:** Move schemas out of the component bodies to avoid re-creation on every render.
5. **Human-Readable Data:** Implement a "View" mode that parses common schema versions into a human-readable table or list, rather than just showing raw JSON.
