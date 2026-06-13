# AcademicYears Module Audit Report

## 1. State, Context & Persistence
- **React Query Integration:** The module uses React Query hooks (`useAcademicYears`, `useSemesters`, etc.) for data fetching and mutations. This is a solid foundation for caching and state management.
- **Academic Context:** The `useAcademic` context is correctly used to trigger a refresh of academic years across the application after mutations, ensuring UI consistency.
- **Local State Management:** Drawers, wizards, and form states are managed locally using `useState`. This is generally appropriate, but the wizard state is complex enough that a reducer might be cleaner.
- **Hard Refreshes:** The module relies on React Query's default behavior to handle hard refreshes. No persistent local storage is used for unsaved form data, which means a refresh during the wizard process will lose all progress.

## 2. Error Handling & Resilience
- **Silent Failures (Critical):** In `AcademicYearsPage.jsx`, the `handleWizardSave` function contains an empty `catch` block when creating semesters: `try { await createSem.mutateAsync(...) } catch {}`. This is a major defect as it silently fails to create semesters without notifying the user.
- **Mutation Errors:** Most mutations (create, update, delete) are wrapped in `try/catch` and use `addToast` for feedback, which is good.
- **Validation:** `AcademicYearWizard.jsx` and `AcademicYearsPage.jsx` perform basic validation (date bounds, required names). However, more robust server-side validation errors should be explicitly handled and mapped to form fields.
- **Missing Error Boundaries:** No module-level error boundary is implemented to prevent the entire page from crashing if a component fails to render.

## 3. Architecture & Wiring
- **Component Decomposition:** The separation into `AcademicTimeline`, `AcademicYearWizard`, and the main page is good for maintainability.
- **Permission Gating:** Correct use of `PermissionGate` for UI-level access control on actions like edit, delete, and set current.
- **Legacy Redirects:** Not present in this module as it's a new layout.
- **Prop Drilling:** The `AcademicYearsPage` manages a lot of state that is passed down to multiple layers (e.g., `DataTable` props).

## 4. Code Quality & Tech Debt
- **Inline Styles:** Extensive use of inline styles in `AcademicYearWizard.jsx` and `AcademicTimeline.jsx`. This makes the code harder to read and maintain. Styles should be moved to the module's CSS file.
- **Hardcoded Strings:** Several UI labels and placeholders are hardcoded in English instead of using the `t()` translation function (e.g., "Semester {idx + 1}", "Academic Year Name *").
- **Date Handling:** Uses basic `Date` object and string splitting for ISO dates. This is fragile for cross-timezone consistency. A library like `date-fns` would be more resilient.
- **Inconsistent Localization:** Some Arabic placeholders are hardcoded (e.g., "مثال: ٢٠٢٤-٢٠٢٥").

## Summary of Defects
1. **[CRITICAL]** Silent failure in `handleWizardSave` semester creation.
2. **[HIGH]** Lack of persistent state in wizards leads to data loss on refresh.
3. **[MEDIUM]** Extensive inline styling across components.
4. **[MEDIUM]** Incomplete i18n coverage.
5. **[LOW]** Fragile date manipulation logic.
