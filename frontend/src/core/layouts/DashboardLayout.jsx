import { useEffect, useState, useCallback, lazy, Suspense } from "react";
import { Outlet, Navigate, useLocation } from "react-router-dom";

import Navbar from "../navigation/navbar/Navbar";
import Sidebar from "../navigation/sidebar/Sidebar";
import SecondarySidebar from "../navigation/secondarySidebar/SecondarySidebar";
import Footer from "../components/Footer";
import Breadcrumbs from "../components/Breadcrumbs";
import CategoryTabs from "../components/CategoryTabs";
import SessionTimeoutWarning from "../components/SessionTimeoutWarning";
import { useAuth } from "../auth/useAuth";
import { usePermission } from "../auth/usePermission";
import "../components/shellComponents.css";

const CommandPalette = lazy(() => import("../components/CommandPalette"));
const KeyboardShortcutsModal = lazy(() => import("../components/KeyboardShortcutsModal"));

const MOBILE_BREAKPOINT = 768;
const SIDEBAR_WIDTH = 230;
const SECONDARY_SIDEBAR_WIDTH = 280;

function DashboardLayout() {
  const { isAuthenticated, isLoading, logout } = useAuth();
  const { can } = usePermission();
  // The directory search panel lists staff/students — staff-only tooling.
  const canSearchDirectory = can("users.users.view");
  const location = useLocation();
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > MOBILE_BREAKPOINT);
  const [secondaryOpen, setSecondaryOpen] = useState(() => {
    try { return localStorage.getItem("capu_secondary_sidebar_open") === "1"; } catch { return false; }
  });
  const [showCommandPalette, setShowCommandPalette] = useState(false);
  const [showShortcuts, setShowShortcuts] = useState(false);

  const isMobile = windowWidth <= MOBILE_BREAKPOINT;

  useEffect(() => {
    const handleResize = () => {
      setWindowWidth(window.innerWidth);
      setSidebarOpen(window.innerWidth > MOBILE_BREAKPOINT);
    };

    window.addEventListener("resize", handleResize);

    return () => window.removeEventListener("resize", handleResize);
  }, []);

  // Global keyboard shortcuts: Cmd+K → Command Palette, ? → Shortcuts
  useEffect(() => {
    const handleGlobalKeyDown = (e) => {
      // Ignore when inside input/textarea/contenteditable
      const tag = e.target.tagName;
      const isInput = tag === "INPUT" || tag === "TEXTAREA" || e.target.isContentEditable;

      // Cmd+K / Ctrl+K → Command Palette
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setShowCommandPalette((prev) => !prev);
        return;
      }

      // ? → Keyboard Shortcuts (only when not in input)
      if (e.key === "?" && !isInput) {
        e.preventDefault();
        setShowShortcuts((prev) => !prev);
      }
    };

    document.addEventListener("keydown", handleGlobalKeyDown);
    return () => document.removeEventListener("keydown", handleGlobalKeyDown);
  }, []);

  const toggleSidebar = () => {
    setSidebarOpen((prev) => !prev);
  };

  const toggleSecondary = () => {
    setSecondaryOpen((prev) => {
      const next = !prev;
      try { localStorage.setItem("capu_secondary_sidebar_open", next ? "1" : "0"); } catch { /* ignore */ }
      return next;
    });
  };

  const closeSidebar = () => {
    setSidebarOpen(false);
  };

  const openCommandPalette = useCallback(() => {
    setShowCommandPalette(true);
  }, []);

  const getPrimaryMargin = () => {
    if (isMobile) return 0;
    if (windowWidth <= 1024) return sidebarOpen ? 64 : 0;
    return sidebarOpen ? SIDEBAR_WIDTH : 0;
  };

  const secondaryVisible = secondaryOpen && canSearchDirectory;

  const getContentMargin = () => {
    if (isMobile) return "0px";

    if (windowWidth <= 1024) {
      const tabletPrimary = sidebarOpen ? 64 : 0;
      return secondaryVisible
        ? `${tabletPrimary + SECONDARY_SIDEBAR_WIDTH}px`
        : `${tabletPrimary}px`;
    }

    const primaryMargin = sidebarOpen ? SIDEBAR_WIDTH : 0;
    const secondaryMargin = secondaryVisible ? SECONDARY_SIDEBAR_WIDTH : 0;
    return `${primaryMargin + secondaryMargin}px`;
  };

  if (isLoading) {
    return (
      <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "100vh", background: "#f5f6fa" }}>
        <p style={{ color: "#9ca3af", fontFamily: "Outfit, sans-serif" }}>Loading…</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    const loginPath = location.pathname.startsWith("/student") ? "/student/login" : "/admin/login";
    return <Navigate to={loginPath} replace />;
  }

  return (
    <div className="dashboard-wrapper">
      {/* Session timeout warning (fixed banner) */}
      <SessionTimeoutWarning onLogout={logout} />

      {isMobile && sidebarOpen && (
        <div className="sidebar-overlay" onClick={closeSidebar} />
      )}

      <Sidebar
        isOpen={sidebarOpen}
        isMobile={isMobile}
        onClose={closeSidebar}
      />

      {secondaryVisible && (
        <SecondarySidebar
          sidebarOpen={sidebarOpen}
          sidebarWidth={
            isMobile ? 0
            : windowWidth <= 1024 ? 64
            : SIDEBAR_WIDTH
          }
        />
      )}

      <Navbar
        onToggleSidebar={toggleSidebar}
        onToggleSecondary={toggleSecondary}
        onOpenCommandPalette={openCommandPalette}
        secondaryOpen={secondaryVisible}
        showDirectoryToggle={canSearchDirectory}
        style={{
          marginInlineStart: isMobile || getPrimaryMargin() === 0 ? "0px" : `${getPrimaryMargin()}px`,
          paddingInlineStart: isMobile || getPrimaryMargin() === 0 ? undefined : "17px",
          transition: "margin-inline-start 0.35s cubic-bezier(0.4,0,0.2,1), padding-inline-start 0.35s cubic-bezier(0.4,0,0.2,1)",
        }}
      />

      <div
        className="dashboard-content"
        style={{
          marginInlineStart: getContentMargin(),
          transition: "margin-inline-start 0.35s cubic-bezier(0.4,0,0.2,1)",
          overflowY: 'auto',
          display: 'flex',
          flexDirection: 'column',
          boxSizing: 'border-box',
        }}
      >

        <Breadcrumbs />

        <CategoryTabs />

        <main className="dashboard-page-content" style={{ flex: 1 }}>
          <Outlet />
        </main>
          <Footer /> 
      </div>

      {/* Command Palette (Cmd+K) */}
      {showCommandPalette && (
        <Suspense fallback={null}>
          <CommandPalette onClose={() => setShowCommandPalette(false)} />
        </Suspense>
      )}

      {/* Keyboard Shortcuts Modal (?) */}
      {showShortcuts && (
        <Suspense fallback={null}>
          <KeyboardShortcutsModal onClose={() => setShowShortcuts(false)} />
        </Suspense>
      )}
    </div>
  );
}

export default DashboardLayout;