import { useEffect, useState } from "react";
import { Outlet, Navigate, useLocation } from "react-router-dom";

import Navbar from "../navigation/navbar/Navbar";
import Sidebar from "../navigation/sidebar/Sidebar";
import SecondarySidebar from "../navigation/secondarySidebar/SecondarySidebar";
import UserScopeBanner from "../components/UserScopeBanner";
import { useAuth } from "../auth/useAuth";
import { getCurrentRouteInfo } from "../router/routeRegistry";
import { PAGE_TYPES, APPLICABLE_TO } from "../manifests/manifestTypes";

const MOBILE_BREAKPOINT = 768;
const SIDEBAR_WIDTH = 230;
const SECONDARY_SIDEBAR_WIDTH = 280;

function DashboardLayout() {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > MOBILE_BREAKPOINT);
  const [secondaryOpen, setSecondaryOpen] = useState(false);

  const isMobile = windowWidth <= MOBILE_BREAKPOINT;

  const routeInfo = getCurrentRouteInfo(location.pathname);
  const currentPageType = routeInfo?.pageType || PAGE_TYPES.MANAGEMENT;
  const currentApplicableTo = routeInfo?.applicableTo || APPLICABLE_TO.BOTH;

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);

    window.addEventListener("resize", handleResize);

    return () => window.removeEventListener("resize", handleResize);
  }, []);

  useEffect(() => {
    setSidebarOpen(!isMobile);
  }, [isMobile]);

  const toggleSidebar = () => {
    setSidebarOpen((prev) => !prev);
  };

  const toggleSecondary = () => {
    setSecondaryOpen((prev) => !prev);
  };

  const closeSidebar = () => {
    setSidebarOpen(false);
  };

  const getContentMargin = () => {
    if (isMobile) return "0px";

    if (windowWidth <= 1024) return secondaryOpen ? `${64 + SECONDARY_SIDEBAR_WIDTH}px` : "64px";

    const primaryMargin = sidebarOpen ? SIDEBAR_WIDTH : 0;
    const secondaryMargin = secondaryOpen ? SECONDARY_SIDEBAR_WIDTH : 0;
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
      {isMobile && sidebarOpen && (
        <div className="sidebar-overlay" onClick={closeSidebar} />
      )}

      <Sidebar
        isOpen={sidebarOpen}
        isMobile={isMobile}
        onClose={closeSidebar}
      />

      {secondaryOpen && (
        <SecondarySidebar
          config={{
            directoryType: currentApplicableTo === APPLICABLE_TO.STAFF ? "staff"
              : currentApplicableTo === APPLICABLE_TO.STUDENT ? "student"
              : "all",
            currentPageType,
            currentApplicableTo,
          }}
          sidebarOpen={sidebarOpen}
          sidebarWidth={SIDEBAR_WIDTH}
        />
      )}

      <div
        className="dashboard-content"
        style={{
          marginLeft: getContentMargin(),
          transition: "margin-left 0.35s cubic-bezier(0.4,0,0.2,1)",
        }}
      >
        <Navbar
          onToggleSidebar={toggleSidebar}
          showSecondary={secondaryOpen}
          onToggleSecondary={toggleSecondary}
        />

        <main className="dashboard-page-content">
          {currentPageType === PAGE_TYPES.MANAGEMENT && <UserScopeBanner />}
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export default DashboardLayout;