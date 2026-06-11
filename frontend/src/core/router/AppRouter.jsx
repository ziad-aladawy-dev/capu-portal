import { lazy, Suspense, useMemo } from "react";
import { Routes, Route } from "react-router-dom";
import DashboardLayout from "../layouts/DashboardLayout";
import { buildProtectedRoutes } from "./routeRegistry";

const LandingPage = lazy(() => import("../../modules/landing/pages/LandingPage"));
const AdminLogin = lazy(() => import("../auth/pages/AdminLogin"));
const StudentLogin = lazy(() => import("../auth/pages/StudentLogin"));
const ResetPassword = lazy(() => import("../auth/pages/ResetPassword"));
const StudentPortalLayout = lazy(() => import("../../modules/studentPortal/layout/StudentPortalLayout"));

function AppRouter() {
  const protectedRoutes = useMemo(() => buildProtectedRoutes(), []);
  // /student/* lives in its own consumer-grade shell; everything else keeps
  // the admin dashboard chrome.
  const { studentRoutes, adminRoutes } = useMemo(() => ({
    studentRoutes: protectedRoutes.filter((r) => r.path.startsWith("/student")),
    adminRoutes: protectedRoutes.filter((r) => !r.path.startsWith("/student")),
  }), [protectedRoutes]);

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
        <Route path="/reset-password" element={<ResetPassword />} />

        {/* Protected dashboard routes — manifest-driven, wrapped by RouteGuard */}
        <Route element={<DashboardLayout />}>
          {adminRoutes.map((route) => (
            <Route
              key={route.path}
              path={route.path}
              element={route.element}
            />
          ))}
        </Route>

        {/* Student portal — its own shell (bottom nav / side drawer) */}
        <Route element={<StudentPortalLayout />}>
          {studentRoutes.map((route) => (
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
            <div className="error-page-container">
              <div className="error-page-card">
                <div className="error-page-icon not-found">
                  <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <path d="m15 9-6 6" />
                    <path d="m9 9 6 6" />
                  </svg>
                </div>
                <h1 className="error-page-code">404</h1>
                <h2 className="error-page-title">Page Not Found</h2>
                <p className="error-page-desc">
                  The page you're looking for doesn't exist or has been moved.
                </p>
                <a href="/admin/dashboard" className="error-page-link">
                  ← Back to Dashboard
                </a>
              </div>
            </div>
          }
        />
      </Routes>
    </Suspense>
  );
}

export default AppRouter;