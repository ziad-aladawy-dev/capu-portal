import { Routes, Route, Navigate } from "react-router-dom";
import LandingPage from "../../modules/landing/pages/LandingPage";
import AdminLogin from "../auth/pages/AdminLogin";
import StudentLogin from "../auth/pages/StudentLogin";
import DashboardLayout from "../layouts/DashboardLayout";
import { buildProtectedRoutes } from "./routeRegistry";

function AppRouter() {
  const protectedRoutes = buildProtectedRoutes();

  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />
      <Route path="/admin/login" element={<AdminLogin />} />
      <Route path="/student/login" element={<StudentLogin />} />

      <Route element={<DashboardLayout />}>
        {protectedRoutes.map((route) => (
          <Route
            key={route.path}
            path={route.path}
            element={route.element}
          />
        ))}
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default AppRouter;
