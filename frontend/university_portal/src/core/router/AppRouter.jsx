import { Routes, Route } from "react-router-dom";

import LandingPage from "../../modules/landing/pages/LandingPage";

import AdminLogin from "../../modules/auth/pages/AdminLogin";
import StudentLogin from "../../modules/auth/pages/StudentLogin";
import DashboardLayout from "../layouts/DashboardLayout";
import AdminDashboard from "../../modules/admin/pages/AdminDashboard";

function AppRouter() {
  return (
    <Routes>

      <Route path="/" element={<LandingPage />} />

      <Route path="/admin/login" element={<AdminLogin />} />

     { /*<Route path="/student/login" element={<StudentLogin />} />*/}

         <Route element={<DashboardLayout />}>

        <Route
          path="/admin/dashboard"
          element={<AdminDashboard />}
        />

      </Route>

    { /* <Route
        path="/student/profile"
        element={<StudentProfile />}
      />*/}

    </Routes>
  );
}

export default AppRouter;