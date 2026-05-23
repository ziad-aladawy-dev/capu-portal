import { lazy, Suspense } from "react";
import { Routes, Route } from "react-router-dom";
import DashboardLayout from "../layouts/DashboardLayout";
import { buildProtectedRoutes } from "./routeRegistry";

const LandingPage = lazy(() => import("../../modules/landing/pages/LandingPage"));
const AdminLogin = lazy(() => import("../auth/pages/AdminLogin"));
const StudentLogin = lazy(() => import("../auth/pages/StudentLogin"));

function AppRouter() {
  const protectedRoutes = buildProtectedRoutes();

  return (
    <Suspense fallback={<div style={{
      display: "flex", alignItems: "center", justifyContent: "center",
      height: "100vh", color: "#9ca3af", fontFamily: '"Outfit", sans-serif',
      fontSize: 13,
    }}>Loading...</div>}>
      <Routes>
        {/* Public routes */}
        <Route path="/" element={<LandingPage />} />
        <Route path="/admin/login" element={<AdminLogin />} />
        <Route path="/student/login" element={<StudentLogin />} />

        {/* Protected dashboard routes — manifest-driven, wrapped by RouteGuard */}
        <Route element={<DashboardLayout />}>
          {protectedRoutes.map((route) => (
            <Route
              key={route.path}
              path={route.path}
              element={route.element}
            />
          ))}
        </Route>

        {/* 404 catch-all */}
        <Route
          path="*"
          element={
            <div style={{
              display: "flex", alignItems: "center", justifyContent: "center",
              height: "100vh", flexDirection: "column",
              color: "#c9a84c", background: "#07091e", fontFamily: "Inter, sans-serif",
            }}>
              <h1 style={{ fontSize: "4rem", margin: 0, fontWeight: 700 }}>404</h1>
              <p style={{ opacity: 0.6, marginTop: 8 }}>Page not found</p>
              <a href="/" style={{ color: "#c9a84c", marginTop: 20, fontSize: "0.9rem" }}>
                ← Back to Home
              </a>
            </div>
          }
        />
      </Routes>
    </Suspense>
  );
}

export default AppRouter;