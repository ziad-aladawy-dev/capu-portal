import { useEffect, useState } from "react";
import { Outlet, Navigate, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { WifiOff } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";
import { useAuth } from "../../../core/auth/useAuth";
import StudentBlockerGate from "../../../core/components/StudentBlockerGate";
import SessionTimeoutWarning from "../../../core/components/SessionTimeoutWarning";
import Footer from "../../../core/components/Footer";
import BottomNav from "./BottomNav";
import SideDrawer from "./SideDrawer";
import { useNavBadges } from "./useNavBadges";
import "../styles/portalTokens.css";
import styles from "./StudentPortalLayout.module.css";

const MOBILE_BREAKPOINT = 768;

/**
 * Shell for every /student/* route: injects the portal design-token layer,
 * renders mobile bottom nav / desktop side drawer, an offline banner, and
 * keeps the mandatory-step blocker (password change, profile completion)
 * as an inner gate so blocked students still see the portal chrome.
 */
function StudentPortalLayout() {
  const { isAuthenticated, isLoading, logout } = useAuth();
  const { t } = useTranslation();
  const location = useLocation();
  const [isMobile, setIsMobile] = useState(window.innerWidth < MOBILE_BREAKPOINT);
  const [isOffline, setIsOffline] = useState(!navigator.onLine);
  const [drawerCollapsed, setDrawerCollapsed] = useState(() => {
    try { return localStorage.getItem("capu_portal_drawer_collapsed") === "1"; } catch { return false; }
  });
  const badges = useNavBadges();

  const toggleDrawerCollapsed = () => {
    setDrawerCollapsed((prev) => {
      const next = !prev;
      try { localStorage.setItem("capu_portal_drawer_collapsed", next ? "1" : "0"); } catch { /* ignore */ }
      return next;
    });
  };

  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth < MOBILE_BREAKPOINT);
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  useEffect(() => {
    const goOffline = () => setIsOffline(true);
    const goOnline = () => setIsOffline(false);
    window.addEventListener("offline", goOffline);
    window.addEventListener("online", goOnline);
    return () => {
      window.removeEventListener("offline", goOffline);
      window.removeEventListener("online", goOnline);
    };
  }, []);

  if (isLoading) {
    return (
      <div className={`student-portal ${styles.bootSplash}`}>
        <div className={styles.bootSpinner} />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/student/login" replace />;
  }

  return (
    <div className={`student-portal ${styles.shell}`}>
      <SessionTimeoutWarning onLogout={logout} />

      {isOffline && (
        <div className={styles.offlineBanner} role="status">
          <WifiOff size={14} />
          {t("offline_banner", { defaultValue: "You're offline — some features may be limited" })}
        </div>
      )}

      {isMobile ? (
        <BottomNav badges={badges} />
      ) : (
        <SideDrawer
          badges={badges}
          collapsed={drawerCollapsed}
          onToggleCollapsed={toggleDrawerCollapsed}
        />
      )}

      <div
        className={isMobile ? styles.contentMobile : styles.contentDesktop}
        style={
          !isMobile
            ? {
                marginInlineStart: drawerCollapsed
                  ? "var(--portal-drawer-width-collapsed)"
                  : "var(--portal-drawer-width)",
              }
            : undefined
        }
      >
        <StudentBlockerGate>
          <AnimatePresence mode="wait">
            <motion.main
              key={location.pathname}
              className={styles.page}
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -8 }}
              transition={{ duration: 0.2, ease: "easeOut" }}
            >
              <Outlet />
            </motion.main>
          </AnimatePresence>
        </StudentBlockerGate>
        <Footer />
      </div>
    </div>
  );
}

export default StudentPortalLayout;
