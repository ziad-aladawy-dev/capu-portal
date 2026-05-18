import { Routes, Route, Navigate } from "react-router-dom";
import { ScopeProvider } from "../contexts/ScopeContext";
import LandingPage from "../../modules/landing/pages/LandingPage";
import AdminLogin from "../auth/pages/AdminLogin";
import StudentLogin from "../auth/pages/StudentLogin";
import DashboardLayout from "../layouts/DashboardLayout";
import AdminDashboard from "../../modules/admin/pages/AdminDashboard";
import UserManagement from "../../modules/users/pages/UserManagement";
import AddStudent from "../../modules/users/pages/AddStudent";
import EditStudent from "../../modules/users/pages/EditStudent";
import AddStaff from "../../modules/users/pages/AddStaff";
import EditStaff from "../../modules/users/pages/EditStaff";
import UserDetails from "../../modules/users/pages/UserDetails";
import UniversityStructurePage from "../../modules/university/pages/UniversityStructurePage";

function AppRouter() {
  return (
    <ScopeProvider>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/admin/login" element={<AdminLogin />} />
        <Route path="/student/login" element={<StudentLogin />} />

        <Route element={<DashboardLayout />}>
          <Route path="/admin" element={<Navigate to="/admin/dashboard" replace />} />
          <Route path="/admin/dashboard" element={<AdminDashboard />} />
          <Route path="/admin/users" element={<UserManagement />} />
          <Route path="/admin/users/add-student" element={<AddStudent />} />
          <Route path="/admin/users/edit-student/:id" element={<EditStudent />} />
          <Route path="/admin/users/add-staff" element={<AddStaff />} />
          <Route path="/admin/users/edit-staff/:id" element={<EditStaff />} />
          <Route path="/admin/users/:id" element={<UserDetails />} />
          <Route path="/admin/programs" element={<h1>Programs Page</h1>} />
          <Route path="/admin/permissions" element={<h1>Permissions Page</h1>} />
          <Route path="/admin/sync" element={<h1>SIS Sync Page</h1>} />
          <Route path="/admin/university-structure" element={<UniversityStructurePage />} />
        </Route>
      </Routes>
    </ScopeProvider>
  );
}

export default AppRouter;