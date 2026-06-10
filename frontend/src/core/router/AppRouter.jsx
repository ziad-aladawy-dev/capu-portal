import { Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider } from "../contexts/AuthContext";
import { ScopeProvider } from "../contexts/ScopeContext";
import ProtectedRoute from "./ProtectedRoute";

// Public pages
import LandingPage from "../../modules/landing/pages/LandingPage";
import AdminLogin from "../../modules/auth/pages/AdminLogin";
import StudentLogin from "../../modules/auth/pages/StudentLogin";

// Admin/Staff layouts and pages
import DashboardLayout from "../layouts/DashboardLayout";
import AdminDashboard from "../../modules/admin/pages/AdminDashboard";
import UserManagement from "../../modules/users/pages/UserManagement";
import AddStudent from "../../modules/users/pages/AddStudent";
import EditStudent from "../../modules/users/pages/EditStudent";
import AddStaff from "../../modules/users/pages/AddStaff";
import EditStaff from "../../modules/users/pages/EditStaff";
import UserDetails from "../../modules/users/pages/UserDetails";
import UniversityStructurePage from "../../modules/university/pages/UniversityStructurePage";

// Student Services - Staff/Admin pages
import StaffDashboard from "../../modules/studentServices/pages/admin/StaffDashboard";
import ServicesManagement from "../../modules/studentServices/pages/admin/ServicesManagement";
import ServiceBuilder from "../../modules/studentServices/pages/admin/ServiceBuilder";
import ServiceDetailsPage from "../../modules/studentServices/pages/admin/ServiceDetailsPage";
import RequestsManagement from "../../modules/studentServices/pages/admin/RequestsManagement";
import RequestReview from "../../modules/studentServices/pages/admin/RequestReview";
import NotificationsCenter from "../../modules/studentServices/pages/admin/NotificationsCenter";

// Student Services - Student pages
import StudentDashboard from "../../modules/studentServices/pages/student/StudentDashboard";
import ServiceDetails from "../../modules/studentServices/pages/student/ServiceDetails";
import RequestSubmission from "../../modules/studentServices/pages/student/RequestSubmission";
import MyRequests from "../../modules/studentServices/pages/student/MyRequests";
import StudentRequestDetails from "../../modules/studentServices/pages/student/StudentRequestDetails";
import StudentNotificationsCenter from "../../modules/studentServices/pages/student/StudentNotificationsCenter";

function AppRouter() {
  return (
    <AuthProvider>
      <ScopeProvider>
        <Routes>
          {/* Public routes */}
          <Route path="/" element={<LandingPage />} />
          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/student/login" element={<StudentLogin />} />

          {/* Staff / Admin protected routes */}
          <Route element={<ProtectedRoute allowedRoles={["Staff", "Super Admin", "Admin", "SystemAdmin"]} />}>
            <Route element={<DashboardLayout />}>
              {/* Core admin routes */}
              <Route path="/admin" element={<Navigate to="/admin/dashboard" replace />} />
              <Route path="/admin/dashboard" element={<AdminDashboard />} />
              <Route path="/admin/users" element={<UserManagement />} />
              <Route path="/admin/users/add-student" element={<AddStudent />} />
              <Route path="/admin/users/edit-student/:id" element={<EditStudent />} />
              <Route path="/admin/users/add-staff" element={<AddStaff />} />
              <Route path="/admin/users/edit-staff/:id" element={<EditStaff />} />
              <Route path="/admin/users/:id" element={<UserDetails />} />
              <Route path="/admin/university-structure" element={<UniversityStructurePage />} />

              {/* Student Services staff routes */}
              <Route path="/admin/student-services/dashboard" element={<StaffDashboard />} />
              <Route path="/admin/student-services/services" element={<ServicesManagement />} />
              <Route path="/admin/student-services/services/create" element={<ServiceBuilder />} />
              <Route path="/admin/student-services/services/edit/:id" element={<ServiceBuilder />} />
              <Route path="/admin/student-services/services/:id" element={<ServiceDetailsPage />} />
              <Route path="/admin/student-services/requests" element={<RequestsManagement />} />
              <Route path="/admin/student-services/requests/:id" element={<RequestReview />} />
              <Route path="/admin/student-services/notifications" element={<NotificationsCenter />} />
            </Route>
          </Route>

          {/* Student protected routes */}
          {/* <Route element={<ProtectedRoute allowedRoles={["Student"]} />}> */}
            {/* <Route element={<StudentLayout />}> */}
              <Route path="/student/dashboard" element={<StudentDashboard />} />
              <Route path="/student/services/:id" element={<ServiceDetails />} />
              <Route path="/student/services/:id/apply" element={<RequestSubmission />} />
              <Route path="/student/requests" element={<MyRequests />} />
              <Route path="/student/requests/:id" element={<StudentRequestDetails />} />
              <Route path="/student/notifications" element={<StudentNotificationsCenter />} />
              <Route path="/student/profile" element={<div>Student Profile Page</div>} />
            {/* </Route> */}
          {/* </Route> */}
        </Routes>
      </ScopeProvider>
    </AuthProvider>
  );
}

export default AppRouter;