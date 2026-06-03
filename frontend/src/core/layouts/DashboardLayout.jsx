import { useEffect, useState } from "react";
import { Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Navbar from "../navigation/navbar/Navbar";
import Sidebar from "../navigation/sidebar/Sidebar";

const MOBILE_BREAKPOINT = 768;

function DashboardLayout() {
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > MOBILE_BREAKPOINT);
  const { i18n } = useTranslation();
  const isRtl = i18n.language === 'ar';
  const isMobile = windowWidth <= MOBILE_BREAKPOINT;

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

  const contentMarginValue = () => {
    if (isMobile) return "0px";
    if (windowWidth <= 1024) return "64px";
    return sidebarOpen ? "230px" : "0px";
  };

  const contentStyle = isRtl
    ? { marginRight: contentMarginValue(), marginLeft: "0px", transition: "margin 0.35s cubic-bezier(0.4,0,0.2,1)" }
    : { marginLeft: contentMarginValue(), marginRight: "0px", transition: "margin 0.35s cubic-bezier(0.4,0,0.2,1)" };

  return (
    <div className="dashboard-wrapper">
      {isMobile && sidebarOpen && <div className="sidebar-overlay" onClick={closeSidebar} />}
      <Sidebar key={i18n.language} isOpen={sidebarOpen} isMobile={isMobile} onClose={closeSidebar} />
      <div className="dashboard-content" style={contentStyle}>
        <Navbar onToggleSidebar={toggleSidebar} />
        <main className="dashboard-page-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export default DashboardLayout;