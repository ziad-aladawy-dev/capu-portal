import { useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AnimatePresence, motion } from "framer-motion";
import {
  LayoutDashboard, BookOpen, LayoutGrid, ClipboardList, Menu,
  CalendarRange, FileText, GraduationCap, Receipt, Bell, UserCog, LogOut, Globe,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { getBottomNavRoutes, getMoreSheetRoutes, portalLabelKey } from "../routes";
import styles from "./BottomNav.module.css";

const ICONS = {
  LayoutDashboard, BookOpen, LayoutGrid, ClipboardList,
  CalendarRange, FileText, GraduationCap, Receipt, Bell, UserCog,
};

function BottomNav({ badges = {} }) {
  const { t, i18n } = useTranslation();
  const { logout } = useAuth();
  const location = useLocation();
  const [moreOpen, setMoreOpen] = useState(false);

  const tabs = getBottomNavRoutes();
  const moreRoutes = getMoreSheetRoutes();
  const moreActive = moreRoutes.some((r) => location.pathname.startsWith(r.path));

  const toggleLanguage = () => {
    i18n.changeLanguage(i18n.language === "ar" ? "en" : "ar");
  };

  return (
    <>
      <nav className={styles.bar} aria-label={t("portal_navigation", { defaultValue: "Portal navigation" })}>
        {tabs.map((route) => {
          const Icon = ICONS[route.menuItem.icon] || LayoutDashboard;
          const badge = badges[route.meta.badge];
          return (
            <NavLink
              key={route.path}
              to={route.path}
              className={({ isActive }) => `${styles.tab} ${isActive ? styles.active : ""}`}
              onClick={() => setMoreOpen(false)}
            >
              {({ isActive }) => (
                <>
                  {isActive && (
                    <motion.span
                      layoutId="bottom-nav-indicator"
                      className={styles.indicator}
                      transition={{ type: "spring", stiffness: 400, damping: 32 }}
                    />
                  )}
                  <span className={styles.iconWrap}>
                    <Icon size={20} />
                    {badge > 0 && <span className={styles.badge}>{badge > 99 ? "99+" : badge}</span>}
                  </span>
                  <span className={styles.label}>{t(portalLabelKey(route.menuItem.label), { defaultValue: route.menuItem.label })}</span>
                </>
              )}
            </NavLink>
          );
        })}

        <button
          type="button"
          className={`${styles.tab} ${moreActive || moreOpen ? styles.active : ""}`}
          onClick={() => setMoreOpen((p) => !p)}
          aria-expanded={moreOpen}
          aria-label={t("more", { defaultValue: "More" })}
        >
          <span className={styles.iconWrap}>
            <Menu size={20} />
            {badges.notifications > 0 && <span className={styles.badge}>{badges.notifications}</span>}
          </span>
          <span className={styles.label}>{t("more", { defaultValue: "More" })}</span>
        </button>
      </nav>

      <AnimatePresence>
        {moreOpen && (
          <>
            <motion.div
              className={styles.sheetOverlay}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setMoreOpen(false)}
            />
            <motion.div
              className={styles.sheet}
              role="dialog"
              aria-label={t("more", { defaultValue: "More" })}
              initial={{ y: "100%" }}
              animate={{ y: 0 }}
              exit={{ y: "100%" }}
              transition={{ type: "spring", stiffness: 380, damping: 36 }}
            >
              <div className={styles.sheetHandle} />
              <div className={styles.sheetBrand}>
                <span className={styles.sheetBrandMark}>
                  <img src="/images/capital-uni-logo-nobackground.png" alt={t("app_name", { defaultValue: "Capital University" })} className={styles.sheetBrandLogo} />
                </span>
                <span className={styles.sheetBrandText}>
                  <strong>{t("university_name_full", { defaultValue: "Capital University" })}</strong>
                  <small>{t("student_portal_label", { defaultValue: "Student Portal" })}</small>
                </span>
              </div>
              <div className={styles.sheetGrid}>
                {moreRoutes.map((route) => {
                  const Icon = ICONS[route.menuItem.icon] || LayoutGrid;
                  const badge = badges[route.meta.badge];
                  return (
                    <NavLink
                      key={route.path}
                      to={route.path}
                      className={({ isActive }) => `${styles.sheetItem} ${isActive ? styles.sheetItemActive : ""}`}
                      onClick={() => setMoreOpen(false)}
                    >
                      <span className={styles.sheetIcon}>
                        <Icon size={20} />
                        {badge > 0 && <span className={styles.badge}>{badge}</span>}
                      </span>
                      <span>{t(portalLabelKey(route.menuItem.label), { defaultValue: route.menuItem.label })}</span>
                    </NavLink>
                  );
                })}
              </div>
              <div className={styles.sheetFooter}>
                <button type="button" className={styles.sheetAction} onClick={toggleLanguage}>
                  <Globe size={16} />
                  {i18n.language === "ar" ? "English" : "العربية"}
                </button>
                <button type="button" className={`${styles.sheetAction} ${styles.logout}`} onClick={() => logout()}>
                  <LogOut size={16} />
                  {t("logout", { defaultValue: "Logout" })}
                </button>
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </>
  );
}

export default BottomNav;
