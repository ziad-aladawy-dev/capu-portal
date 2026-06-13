# Student Services Module Audit

## 1. State, Context & Persistence
- **Inconsistent State Management:** Unlike the Students module, this module relies heavily on manual `useState` and `useEffect` within custom hooks (`useStaffRequests`, `useStaffRequestsPaged`). This misses out on the caching, synchronization, and background fetching benefits of React Query.
- **Manual Pagination State:** `useStaffRequestsPaged` manually tracks page, pageSize, and totalCount. This is boilerplate-heavy and prone to synchronization issues.
- **Refresh Persistence:** Page state and filters in `RequestsManagement.jsx` are not persisted in the URL, meaning a browser refresh resets the user to page 1 with no filters.

## 2. Error Handling & Resilience
- **Missing Try/Catch:** Many API calls in components (e.g., `handleStatusChange` in `RequestReview.jsx`) have very basic error handling (`console.error`). They should provide user-visible feedback via toasts or error boundaries.
- **Attachment Failures:** `getAttachments` in `useStaffRequests` catches errors but only logs them to the console, returning an empty array. The user is not informed that attachments failed to load.
- **Form Validation:** `ServiceBuilder.jsx` has manual validation logic in `validateStep`. While functional, it's scattered and hard to maintain as the form grows.

## 3. Architecture & Wiring
- **API/DTO Deficiencies:** `RequestReview.jsx` has to derive the current assignee by searching through the history log because the Request DTO lacks an `assigneeId` field. This is a major architectural weakness.
- **Hardcoded Logic:** `VALID_NEXT` (status transitions) and `ASSIGNABLE_STATUSES` are hardcoded in `RequestReview.jsx`. This business logic should ideally be driven by the backend to ensure consistency.
- **Prop Drilling:** `BasicInfo`, `EligibilityPricing`, and `WorkflowStep` components in `ServiceBuilder.jsx` receive many props. As the builder expands, this will become difficult to manage. Consider using a Context or a state management library for the builder's multi-step state.
- **Mixed API Scopes:** `studentServicesService.js` contains calls to `/academic-years` and `/structure`, which are outside the student-services domain. While necessary for lookups, they should ideally be imported from their respective service files.

## 4. Code Quality & Tech Debt
- **JSON Display:** `RequestReview.jsx` displays `submittedData` using `<pre>{JSON.stringify(...)}</pre>`. This is unprofessional for a production system and should be replaced by a structured, user-friendly display (e.g., using `DynamicFormRenderer` in read-only mode).
- **ID Generation:** `FormBuilder.jsx` uses `Date.now()` and `Math.random()` for field IDs. While unlikely to collide in a single session, it's better to use a UUID library.
- **Type Conversions:** Constant mapping between integer enums (from API) and string labels/types (for UI) is scattered across `DynamicFormRenderer`, `FormBuilder`, and `ServiceBuilder`. This should be centralized in a mapper utility.
- **Dead Code/Mock Logic:** `useStaffRequests.js` initializes `requests` as an empty array but doesn't use React Query's `data` property, even though it injects `queryClient`. It seems to be in a halfway state between manual fetching and React Query.
- **UI/UX Bottleneck:** Large file uploads in `FileUploader.jsx` (referenced in `DynamicFormRenderer`) should have progress indicators and better size/type validation on the client side.

## Summary of Critical Issues
1. **State Management:** Migrate custom hooks to `useQuery` and `useMutation` for better consistency and performance.
2. **Data Model:** Update the backend Request DTO to include `assigneeId`.
3. **UX Quality:** Replace JSON pre-tags with proper data visualizations.
4. **Maintenance:** Centralize enum/type mapping and business logic (like status transitions).
5. **Persistence:** Sync pagination and filter state with the URL.
