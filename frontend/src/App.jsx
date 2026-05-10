import React, { useState, useEffect } from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  useLocation,
  Outlet
} from "react-router-dom";

// --- Original Layout Imports ---
import Sidebar from "./core/layouts/Sidebar/Sidebar";
import TopNav from "./core/layouts/TopNav/TopNav";

// --- Core Pages ---
import LandingPage from './core/pages/Landing/LandingPage';
import Login from './core/auth/pages/Login';
import AdminDashboard from './core/pages/Dashboard/AdminDashboard';
import UniversityTree from './core/pages/UniversityStructure/UniversityTree';
import PermissionManagement from './core/pages/Permissions/PermissionManagement';
import SyncManifest from './modules/sis-sync/pages/SyncManifest';
import UserManagement from './core/pages/Users/UserManagement';
import AddStudent from './core/pages/Users/AddStudent';
import AddStaff from './core/pages/Users/AddStaff';
import EditStudent from './core/pages/Users/EditStudent';
import EditStaff from './core/pages/Users/EditStaff';
import UserDetails from './core/pages/Users/UserDetails';

// --- Guards & Contexts ---
import ProtectedRoute from './core/guards/ProtectedRoute';
import { AuthProvider } from "./contexts/AuthContext";
import { ScopeProvider } from "./contexts/ScopeContext";
import { FilterProvider } from "./contexts/FilterContext";
import { useFilters } from "./hooks/use-filters";
import { FILTER_CATEGORIES } from "./lib/constants";

// --- Layouts ---
import { FilterSidebar } from "./core/layouts/FilterSidebar/FilterSidebar";

// --- Styles ---
import "./App.css";

// AppLayout using the original Sidebar and TopNav, but incorporating FilterSidebar
const AppLayout = () => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [filterSidebarOpen, setFilterSidebarOpen] = useState(true);
  const location = useLocation();
  const { setCurrentCategory } = useFilters();

  // Route matching to Filter Categories
  useEffect(() => {
    if (location.pathname.startsWith("/users")) {
      setCurrentCategory(FILTER_CATEGORIES.ADMIN); // Users filter matches admin category usually
    }
  }, [location.pathname, setCurrentCategory]);

  // Determine if filter sidebar should be visible
  const showFilterSidebar = ["/users"].some(path => location.pathname.startsWith(path));

  const getFilterSidebarClass = () => {
    if (!showFilterSidebar) return "";
    return filterSidebarOpen ? "with-filter-sidebar" : "with-filter-sidebar collapsed";
  };

  return (
    <div className="app-layout">
      <TopNav onMenuClick={() => setSidebarOpen(!sidebarOpen)} />
      <div className="app-main">
        <Sidebar
          isOpen={sidebarOpen} 
          onClose={() => setSidebarOpen(false)}
        />
        <FilterSidebar 
          isVisible={showFilterSidebar} 
          isOpen={filterSidebarOpen}
          onToggle={() => setFilterSidebarOpen(!filterSidebarOpen)}
        />
        <main className={`app-content ${getFilterSidebarClass()}`}>
          <div className="page-content">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ScopeProvider>
          <FilterProvider>
            <Routes>
              {/* Public Routes */}
              <Route path="/" element={<LandingPage />} />
              <Route path="/login" element={<Login />} />
              
              {/* Protected Layout wrapper */}
              <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
                <Route path="dashboard" element={<AdminDashboard />} />
                <Route path="permissions" element={<PermissionManagement />} />
                <Route path="university-tree" element={<UniversityTree />} />
                <Route path="sync" element={<SyncManifest />} />

                {/* User routes */}
                <Route path="users" element={<UserManagement />} />
                <Route path="users/add-student" element={<AddStudent />} />
                <Route path="users/add-staff" element={<AddStaff />} />
                <Route path="users/edit-student/:id" element={<EditStudent />} />
                <Route path="users/edit-staff/:id" element={<EditStaff />} />
                <Route path="users/:id" element={<UserDetails />} />

                {/* Legacy paths as placeholders */}
                <Route path="faculties" element={
                  <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                    <h2>Faculty Management</h2><p>Coming soon...</p>
                  </div>
                } />
                <Route path="courses" element={
                  <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                    <h2>Course Management</h2><p>Coming soon...</p>
                  </div>
                } />
                <Route path="reports" element={
                  <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                    <h2>Reports</h2><p>Coming soon...</p>
                  </div>
                } />
                <Route path="settings" element={
                  <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                    <h2>Settings</h2><p>Coming soon...</p>
                  </div>
                } />
              </Route>

              {/* Fallback */}
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </FilterProvider>
        </ScopeProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
