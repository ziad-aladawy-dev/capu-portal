import React, { useState, useEffect } from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  useLocation,
  Outlet
} from "react-router-dom";

// --- Old Imports (from HEAD) ---
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
import ProtectedRoute from './core/guards/ProtectedRoute';
import './styles/global.css';

// --- New Contexts & Components (from fe-permissions) ---
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
import { Dashboard } from "./pages/Dashboard";
import { Users } from "./pages/Users";
import { StudentList } from "./pages/students/StudentList";
import { StudentDetail } from "./pages/students/StudentDetail";
import { mockPermissionMatrix } from "./lib/mock-data";
import "./App.css";

// AppLayout component
const AppLayout = () => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [filterSidebarOpen, setFilterSidebarOpen] = useState(true);
  const location = useLocation();
  const { setCurrentCategory } = useFilters();

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

  const showFilterSidebar = !["/dashboard", "/permissions"].includes(location.pathname);

  const getFilterSidebarClass = () => {
    if (!showFilterSidebar) return "";
    return filterSidebarOpen ? "with-filter-sidebar" : "with-filter-sidebar collapsed";
  };

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
    window.location.hash = path;
  };

  return (
    <div className="app-layout">
      <TopBar onMenuToggle={() => setSidebarOpen(!sidebarOpen)} />
      <div className="app-main">
        <AppSidebar 
          isOpen={sidebarOpen} 
          onClose={() => setSidebarOpen(false)}
          onNavigate={handleSidebarNavigate}
        />
        <FilterSidebar 
          isVisible={showFilterSidebar} 
          isOpen={filterSidebarOpen}
          onToggle={() => setFilterSidebarOpen(!filterSidebarOpen)}
        />
        <main className={`app-content ${getFilterSidebarClass()}`}>
          {getHorizontalNavItems().length > 0 && (
            <HorizontalNav items={getHorizontalNavItems()} />
          )}
          <div className="page-content">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};

const PermissionsPage = () => (
  <div className="page-container">
    <PermissionMatrix initialPermissions={mockPermissionMatrix} />
  </div>
);

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

                {/* Legacy / Custom Routes merged from HEAD */}
                <Route path="university-tree" element={<UniversityTree />} />
                <Route path="sync" element={<SyncManifest />} />
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
                
                {/* User routes */}
                <Route path="users" element={<UserManagement />} />
                <Route path="users/add-student" element={<AddStudent />} />
                <Route path="users/add-staff" element={<AddStaff />} />
                <Route path="users/edit-student/:id" element={<EditStudent />} />
                <Route path="users/edit-staff/:id" element={<EditStaff />} />
                <Route path="users/:id" element={<UserDetails />} />
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
