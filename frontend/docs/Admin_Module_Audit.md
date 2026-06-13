# Admin Module Audit Report

## 1. State, Context & Persistence
- **Context Usage:** The module integrates deeply with `usePermission`, `useDomain`, and `useAcademic` contexts. This allows the dashboard to be responsive to global app state changes (like switching semesters or university domains).
- **React Query for Dashboard:** Data is fetched using custom React Query hooks (`useStudentStatistics`, `useOfferingStats`, etc.), which provides caching and background updates.
- **Persistence:** Dashboard data is purely ephemeral; no local persistence is needed beyond the API-backed caching.

## 2. Error Handling & Resilience
- **Implicit Error Handling:** Most components rely on the `fmt` utility to handle null/undefined values gracefully (`v == null ? "—" : ...`). However, explicit error feedback is missing if a dashboard query fails entirely.
- **Permission Gating:** The module uses `can()` checks and `PermissionGate` to hide sections or buttons that the user isn't authorized for. This is good for resilience as it prevents unauthorized API calls and 403 errors.
- **Loading States:** Well-implemented loading states using skeletons (in `RecentActivities`) and `loading` props in dashboard widgets.

## 3. Architecture & Wiring
- **Component isolation:** Components like `InsightsCharts`, `RecentActivities`, and `StatsCards` are well-isolated and focus on specific data domains.
- **Dynamic Quick Actions:** `QuickActions.jsx` uses `useScopeAwareUI`, a clean architectural pattern for showing context-sensitive actions.
- **Wiring:** Connections between components are minimal, with most data flow coming from shared contexts or direct query hooks.

## 4. Code Quality & Tech Debt
- **Utility Duplication:**
    - `relativeTime` in `RecentActivities.jsx` is defined locally. This should be a global utility.
    - `fmt` in `StatsCards.jsx` is defined locally. This is also a candidate for a shared utility.
- **Hardcoded Logic:**
    - `DOT_BY_ACTION` map in `RecentActivities.jsx` is hardcoded.
    - Manual date string manipulation (`iso.endsWith("Z") || iso.includes("+")`) in `RecentActivities.jsx` is fragile.
- **Inline Styling:** Some minor inline styles remain (e.g., dividers and margin overrides), though less than in the `AcademicYears` module.
- **Data Configuration:** `quickActionsConfig` in `dashboardData.js` is quite small and could potentially be integrated into a larger configuration system.

## Summary of Defects
1. **[HIGH]** Local re-implementation of common utilities (`relativeTime`, `fmt`) leads to maintenance overhead.
2. **[MEDIUM]** Fragile manual date string parsing for audit logs.
3. **[MEDIUM]** Missing explicit error UI for failed dashboard data fetches.
4. **[LOW]** Some remaining inline styles instead of CSS classes.
