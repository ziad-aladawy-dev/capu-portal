# Academic Module Audit Report

## 1. State, Context & Persistence
- **State Complexity:** This module handles high-complexity state, especially in `AcademicPlansPage.jsx`. The use of multiple `useEffect` hooks for auto-selection logic is a "code smell" that could lead to race conditions or unexpected UI flickering.
- **Inconsistent Data Fetching:** `AcademicPlansPage.jsx` manually fetches courses using a service call inside `useEffect` instead of a React Query hook, which is inconsistent with `CourseCatalogPage.jsx` and `CourseOfferingsPage.jsx`.
- **Query Parameters:** Strong use of `useMemo` for query parameters ensures that API calls are only triggered when necessary.
- **Persistence:** Local state for search and pagination is lost on hard refresh.

## 2. Error Handling & Resilience
- **Error Banners & Toasts:** Good use of `addToast` for mutation feedback and `aplans-error-banner` for query failures.
- **Incomplete Wizard Checks:** Need to verify if `BatchSectionsWizard` has similar silent failure issues as found in the `AcademicYears` module.
- **Graceful Degradation:** `CourseOfferingsPage` handles missing semester selection with a clear `EmptyState`.
- **Read-Only Mode:** Robust implementation of read-only states for closed academic plans and courses, including UI-level locks.

## 3. Architecture & Wiring
- **Component Granularity:** Pages are well-decomposed into sub-components (e.g., `CurriculumGrid`, `CurriculumTable`, `CatalogHealth`).
- **Legacy Support:** The `routes.js` file includes legacy path redirects, which is excellent for system migration and user bookmarks.
- **Deep Integration:** Strong wiring with `DomainContext` to ensure all views (plans, courses, offerings) are filtered correctly by the user's active university scope.
- **Validation Logic:** `prereqGraph.js` provides client-side cycle detection for prerequisites, which is a high-quality UX feature.

## 4. Code Quality & Tech Debt
- **Tech Debt in Plans:** The `handleExportCsv` function in `AcademicPlansPage.jsx` is a large block of procedural code that should be moved to a shared utility or a dedicated export service.
- **Atomic Operations:** `handleUpdateCourse` in `AcademicPlansPage.jsx` performs an atomic remove-then-re-add via a batch endpoint because a dedicated update endpoint is missing in the backend/service layer.
- **Inline Styles:** Still present in several places (e.g., `aplans-page` has `style={{ padding: 0 }}`).
- **Hardcoded Localized Strings:** Some Arabic strings like "مثال: خطة ٢٠٢٤" are hardcoded. While it's correct for an Arabic example, it should ideally be managed via i18n keys for consistency.
- **Manual Date Formatting:** `formatDate` utility is redefined locally in `AcademicPlansPage.jsx`.

## Summary of Defects
1. **[HIGH]** Inconsistent data fetching pattern (Service vs Query Hook) in `AcademicPlansPage.jsx`.
2. **[MEDIUM]** Complex and potentially brittle `useEffect` chain for auto-selection in Plans.
3. **[MEDIUM]** Large procedural logic for CSV export embedded in a UI component.
4. **[MEDIUM]** Local re-implementation of common utilities like `formatDate`.
5. **[LOW]** Mixture of CSS modules and inline styles.
