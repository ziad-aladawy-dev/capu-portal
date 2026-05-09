import React, { useState, useEffect } from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  useLocation,
  Outlet
} from "react-router-dom";
import { AuthProvider } from "./contexts/AuthContext";
import { ScopeProvider } from "./contexts/ScopeContext";
import { FilterProvider } from "./contexts/FilterContext";
import { useFilters } from "./hooks/use-filters";
import { FILTER_CATEGORIES } from "./lib/constants";
import { TopBar } from "./components/layout/TopBar";
import { AppSidebar } from "./components/layout/AppSidebar";
import { FilterSidebar } from "./components/layout/FilterSidebar";
import { HorizontalNav } from "./components/layout/HorizontalNav";
import { PermissionMatrix } from "./components/permissions/PermissionMatrix";
import { Login } from "./pages/Login";
import { Dashboard } from "./pages/Dashboard";
import { Users } from "./pages/Users";
import { StudentList } from "./pages/students/StudentList";
import { StudentDetail } from "./pages/students/StudentDetail";
import { mockPermissionMatrix } from "./lib/mock-data";
import "./App.css";

/**
 * Main layout wrapper for authenticated routes
 */
const AppLayout = () => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [filterSidebarOpen, setFilterSidebarOpen] = useState(true);
  const location = useLocation();
  const { setCurrentCategory } = useFilters();

  // Determine category based on URL and update filter context
  useEffect(() => {
    if (location.pathname.startsWith("/students")) {
      setCurrentCategory(FILTER_CATEGORIES.STUDENTS);
    } else if (location.pathname.startsWith("/admin")) {
      setCurrentCategory(FILTER_CATEGORIES.ADMIN);
    } else if (location.pathname.startsWith("/financial")) {
      setCurrentCategory(FILTER_CATEGORIES.FINANCIAL);
    } else if (location.pathname.startsWith("/registration")) {
      setCurrentCategory(FILTER_CATEGORIES.REGISTRATION);
    }
  }, [location.pathname, setCurrentCategory]);

  // Determine if we should show filter sidebar (not on dashboard or permissions)
  const showFilterSidebar = !["/dashboard", "/permissions"].includes(location.pathname);

  // Get filter sidebar class
  const getFilterSidebarClass = () => {
    if (!showFilterSidebar) return "";
    return filterSidebarOpen ? "with-filter-sidebar" : "with-filter-sidebar collapsed";
  };

  // Get horizontal nav items based on current path
  const getHorizontalNavItems = () => {
    if (location.pathname.startsWith("/students")) {
      return [
        { label: "All Students", path: "/students/list" },
        { label: "Enrollment", path: "/students/enrollment" },
        { label: "Grades", path: "/students/grades" }
      ];
    }
    if (location.pathname.startsWith("/admin")) {
      return [
        { label: "Users", path: "/admin/users" },
        { label: "Roles", path: "/admin/roles" },
        { label: "Departments", path: "/admin/departments" }
      ];
    }
    return [];
  };

  const handleSidebarNavigate = (path) => {
    // Navigation handled through React Router
    window.location.hash = path;
  };

  return (
    <div className="app-layout">
      {/* Top Navigation Bar */}
      <TopBar onMenuToggle={() => setSidebarOpen(!sidebarOpen)} />

      <div className="app-main">
        {/* Sidebar 1: Navigation */}
        <AppSidebar 
          isOpen={sidebarOpen} 
          onClose={() => setSidebarOpen(false)}
          onNavigate={handleSidebarNavigate}
        />

        {/* Sidebar 2: Filters */}
        <FilterSidebar 
          isVisible={showFilterSidebar} 
          isOpen={filterSidebarOpen}
          onToggle={() => setFilterSidebarOpen(!filterSidebarOpen)}
        />

        {/* Main Content */}
        <main className={`app-content ${getFilterSidebarClass()}`}>
          {/* Horizontal Sub-navigation */}
          {getHorizontalNavItems().length > 0 && (
            <HorizontalNav items={getHorizontalNavItems()} />
          )}

          {/* Page Content */}
          <div className="page-content">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};

/**
 * Permissions page
 */

/**
 * Permissions page
 */
const PermissionsPage = () => (
  <div className="page-container">
    <PermissionMatrix initialPermissions={mockPermissionMatrix} />
  </div>
);

/**
 * Placeholder pages
 */
const AdminPage = () => (
  <div className="page-container">
    <h1>Admin Management</h1>
    <p>Admin management page</p>
  </div>
);

const FinancialPage = () => (
  <div className="page-container">
    <h1>Financial</h1>
    <p>Financial management page</p>
  </div>
);

const RegistrationPage = () => (
  <div className="page-container">
    <h1>Registration</h1>
    <p>Registration management page</p>
  </div>
);

/**
 * Main App Component
 */
export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ScopeProvider>
          <FilterProvider>
            <Routes>
              <Route path="/login" element={<Login />} />
              
              <Route path="/" element={<AppLayout />}>
                <Route index element={<Navigate to="/dashboard" />} />
                <Route path="dashboard" element={<Dashboard />} />
                <Route path="permissions" element={<PermissionsPage />} />
                
                {/* Students routes */}
                <Route path="students" element={<Navigate to="/students/list" />} />
                <Route path="students/list" element={<StudentList />} />
                <Route path="students/detail/:studentId" element={<StudentDetail />} />
                <Route path="students/enrollment" element={<StudentList />} />
                <Route path="students/grades" element={<StudentList />} />
                
                {/* Admin routes */}
                <Route path="admin" element={<Navigate to="/admin/users" />} />
                <Route path="admin/users" element={<Users />} />
                <Route path="admin/*" element={<AdminPage />} />
                
                {/* Financial routes */}
                <Route path="financial" element={<FinancialPage />} />
                <Route path="financial/*" element={<FinancialPage />} />
                
                {/* Registration routes */}
                <Route path="registration" element={<RegistrationPage />} />
                <Route path="registration/*" element={<RegistrationPage />} />
              </Route>
            </Routes>
          </FilterProvider>
        </ScopeProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
