import { useEffect, useState } from "react";
import { Outlet, useLocation } from "react-router-dom";

import Navbar from "../navigation/navbar/Navbar";
import Sidebar from "../navigation/sidebar/Sidebar";
import SecondarySidebar from "../navigation/secondarySidebar/SecondarySidebar";
import { StickySelectionProvider } from "../contexts/StickySelectionContext";

const MOBILE_BREAKPOINT = 768;
const DIRECTORY_ROUTES = ["/admin/users", "/admin/staff", "/admin/students"];

function getSecondarySidebarConfig(pathname) {
  if (pathname.startsWith("/admin/staff")) {
    return {
      directoryType: "staff",
      filters: [
        {
          key: "role",
          label: "Role",
          options: [],
        },
        {
          key: "status",
          label: "Status",
          options: [
            { value: "active", label: "Active" },
            { value: "inactive", label: "Inactive" },
          ],
        },
      ],
    };
  }

  if (pathname.startsWith("/admin/students")) {
    return {
      directoryType: "student",
      filters: [
        {
          key: "enrollment",
          label: "Enrollment",
          options: [
            { value: "active", label: "Active" },
            { value: "graduated", label: "Graduated" },
            { value: "suspended", label: "Suspended" },
          ],
        },
        {
          key: "level",
          label: "Academic Level",
          options: [],
        },
      ],
    };
  }

  if (pathname.startsWith("/admin/users")) {
    return {
      directoryType: "all",
      filters: [
        {
          key: "type",
          label: "Type",
          options: [
            { value: "staff", label: "Staff" },
            { value: "student", label: "Student" },
          ],
        },
        {
          key: "status",
          label: "Status",
          options: [
            { value: "active", label: "Active" },
            { value: "inactive", label: "Inactive" },
          ],
        },
      ],
    };
  }

  return null;
}

function DashboardLayout() {
  const location = useLocation();
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > MOBILE_BREAKPOINT);

  const isMobile = windowWidth <= MOBILE_BREAKPOINT;
  const showSecondary = DIRECTORY_ROUTES.some((p) => location.pathname.startsWith(p));
  const secondaryConfig = getSecondarySidebarConfig(location.pathname);

  const sidebarWidth = 230;
  const secondaryWidth = 280;

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    window.addEventListener("resize", handleResize);
    return () => window.removeEventListener("resize", handleResize);
  }, []);

  useEffect(() => {
    setSidebarOpen(!isMobile);
  }, [isMobile]);

  const toggleSidebar = () => setSidebarOpen((prev) => !prev);
  const closeSidebar = () => setSidebarOpen(false);

  const getContentMargin = () => {
    if (isMobile) return "0px";

    let margin = sidebarOpen && !isMobile ? `${sidebarWidth}px` : "0px";

    if (showSecondary && windowWidth > 1024) {
      const base = sidebarOpen ? sidebarWidth : 0;
      margin = `${base + secondaryWidth}px`;
    }

    return margin;
  };

  return (
    <StickySelectionProvider>
      <div className="dashboard-wrapper">
        {isMobile && sidebarOpen && (
          <div className="sidebar-overlay" onClick={closeSidebar} />
        )}

        <Sidebar isOpen={sidebarOpen} isMobile={isMobile} onClose={closeSidebar} />

        {showSecondary && !isMobile && (
          <SecondarySidebar config={secondaryConfig} sidebarOpen={sidebarOpen} sidebarWidth={sidebarWidth} />
        )}

        <div
          className="dashboard-content"
          style={{
            marginLeft: getContentMargin(),
            transition: "margin-left 0.35s cubic-bezier(0.4,0,0.2,1)",
          }}
        >
          <Navbar onToggleSidebar={toggleSidebar} showSecondary={showSecondary} />

          <main className="dashboard-page-content">
            <Outlet />
          </main>
        </div>
      </div>
    </StickySelectionProvider>
  );
}

export default DashboardLayout;
