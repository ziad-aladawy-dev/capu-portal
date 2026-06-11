import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  LayoutDashboard, BookOpen, LayoutGrid, ClipboardList,
  CalendarRange, FileText, GraduationCap, Receipt, Bell, UserCog,
  LogOut, Globe, PanelLeftClose, PanelLeftOpen,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { getLocalized } from "../../../core/utils/getLocalized";
import { getDrawerGroups, portalLabelKey } from "../routes";
import styles from "./SideDrawer.module.css";

const ICONS = {
  LayoutDashboard, BookOpen, LayoutGrid, ClipboardList,
  CalendarRange, FileText, GraduationCap, Receipt, Bell, UserCog,
};

const GROUP_LABELS = {
  main: null,
  academic: "Academic",
  services: "Services",
  account: "Account",
};

function SideDrawer({ badges = {}, collapsed, onToggleCollapsed }) {
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();
  const { selectedSemesterObj, selectedYearObj } = useAcademic();

  const groups = getDrawerGroups();
  const displayName = getLocalized(user?.name, i18n.language) || "Student";
  const initial = displayName.charAt(0).toUpperCase();
  const semesterLabel = selectedYearObj && selectedSemesterObj
    ? `${getLocalized(selectedYearObj.name, i18n.language)} · ${getLocalized(selectedSemesterObj.name, i18n.language)}`
    : null;

  const renderItem = (route) => {
    const Icon = ICONS[route.menuItem.icon] || LayoutGrid;
    const badge = badges[route.meta.badge];
    return (
      <NavLink
        key={route.path}
        to={route.path}
        className={({ isActive }) => `${styles.item} ${isActive ? styles.itemActive : ""}`}
        title={collapsed ? t(portalLabelKey(route.menuItem.label), { defaultValue: route.menuItem.label }) : undefined}
      >
        <span className={styles.itemIcon}>
          <Icon size={18} />
          {badge > 0 && <span className={styles.badge}>{badge > 99 ? "99+" : badge}</span>}
        </span>
        {!collapsed && (
          <span className={styles.itemLabel}>
            {t(portalLabelKey(route.menuItem.label), { defaultValue: route.menuItem.label })}
          </span>
        )}
      </NavLink>
    );
  };

  return (
    <aside className={`${styles.drawer} ${collapsed ? styles.collapsed : ""}`}>
      <div className={styles.profile}>
        <div className={styles.avatar}>{initial}</div>
        {!collapsed && (
          <div className={styles.profileText}>
            <strong className={styles.profileName}>{displayName}</strong>
            {user?.attributes?.department && (
              <span className={styles.profileMeta}>{user.attributes.department}</span>
            )}
            {semesterLabel && <span className={styles.semesterBadge}>{semesterLabel}</span>}
          </div>
        )}
      </div>

      <nav className={styles.nav}>
        {Object.entries(groups).map(([key, routes]) => {
          if (routes.length === 0) return null;
          return (
            <div key={key} className={styles.group}>
              {GROUP_LABELS[key] && !collapsed && (
                <div className={styles.groupLabel}>
                  {t(portalLabelKey(GROUP_LABELS[key]), { defaultValue: GROUP_LABELS[key] })}
                </div>
              )}
              {routes.map(renderItem)}
            </div>
          );
        })}
      </nav>

      <div className={styles.footer}>
        <button
          type="button"
          className={styles.footerBtn}
          onClick={() => i18n.changeLanguage(i18n.language === "ar" ? "en" : "ar")}
          title={i18n.language === "ar" ? "English" : "العربية"}
        >
          <Globe size={16} />
          {!collapsed && (i18n.language === "ar" ? "English" : "العربية")}
        </button>
        <button
          type="button"
          className={`${styles.footerBtn} ${styles.logout}`}
          onClick={() => logout()}
          title={t("logout", { defaultValue: "Logout" })}
        >
          <LogOut size={16} />
          {!collapsed && t("logout", { defaultValue: "Logout" })}
        </button>
        <button
          type="button"
          className={styles.footerBtn}
          onClick={onToggleCollapsed}
          aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {collapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
          {!collapsed && t("collapse", { defaultValue: "Collapse" })}
        </button>
      </div>
    </aside>
  );
}

export default SideDrawer;
