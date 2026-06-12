import { useMemo } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { getCurrentRouteInfo, getGroupedMenuItems } from "../router/routeRegistry";
import { resolveIcon } from "../navigation/menuAggregator";
import { usePermission } from "../auth/usePermission";
import "./categoryTabs.css";

// Mirrors the sidebar's label → i18n key convention so both surfaces share
// translations ("Course Catalog" → t("course_catalog"), raw label fallback).
const labelToKey = (label) => label.toLowerCase().replace(/\s+/g, "_");

/**
 * Horizontal sub-navigation for the current sidebar category, rendered once by
 * DashboardLayout (above the routed page). Because it lives in the layout it
 * persists across page navigations — switching tabs swaps only the <Outlet/>
 * content, the tab bar itself never remounts.
 *
 * Tabs come straight from route registry menuItem metadata, permission-filtered
 * like the sidebar. Detail routes without a menuItem (e.g. /admin/users/:id)
 * resolve their category via longest menu-path prefix match, so the bar stays
 * visible while drilling into a category's records. Categories with fewer than
 * two visible pages render nothing.
 */
function CategoryTabs() {
  const { t } = useTranslation();
  const { can } = usePermission();
  const { pathname } = useLocation();

  // Route metadata is static for the app lifetime.
  const grouped = useMemo(() => getGroupedMenuItems(), []);

  const category = useMemo(() => {
    const route = getCurrentRouteInfo(pathname);
    if (route?.menuItem?.category) return route.menuItem.category;
    // Detail pages: longest menu-path prefix wins (/admin/student-services/
    // requests/:id → Student Services).
    let best = null;
    let bestLen = 0;
    for (const [cat, items] of Object.entries(grouped)) {
      for (const item of items) {
        if (pathname.startsWith(`${item.path}/`) && item.path.length > bestLen) {
          best = cat;
          bestLen = item.path.length;
        }
      }
    }
    return best;
  }, [pathname, grouped]);

  const tabs = useMemo(() => {
    if (!category) return [];
    return (grouped[category] || []).filter(
      (item) => !item.permission || can(item.permission, 1)
    );
  }, [category, grouped, can]);

  if (tabs.length < 2) return null;

  const catKey = labelToKey(category);

  return (
    <nav className="cat-tabs" aria-label={t(catKey) === catKey ? category : t(catKey)}>
      {tabs.map((item) => {
        const Icon = resolveIcon(item.icon);
        const itemKey = labelToKey(item.label);
        return (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) => `cat-tab${isActive ? " is-active" : ""}`}
          >
            <Icon size={14} aria-hidden="true" />
            <span>{t(itemKey) === itemKey ? item.label : t(itemKey)}</span>
          </NavLink>
        );
      })}
    </nav>
  );
}

export default CategoryTabs;
