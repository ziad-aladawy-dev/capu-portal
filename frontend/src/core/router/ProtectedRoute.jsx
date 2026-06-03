import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

const ProtectedRoute = ({ allowedRoles = [] }) => {
  const { isAuthenticated, user, loading } = useAuth();

  if (loading) {
    return <div>Loading...</div>;
  }

  if (!isAuthenticated) {
    const savedUser = localStorage.getItem("user");
    let redirect = "/admin/login";
    if (savedUser) {
      try {
        const u = JSON.parse(savedUser);
        if (u.role === "Student") redirect = "/student/login";
      } catch {}
    }
    return <Navigate to={redirect} replace />;
  }

  const userRole = user?.role || "";
  const normalizedRole = userRole.toLowerCase();
  const allowedNormalized = allowedRoles.map(r => r.toLowerCase());

  if (allowedRoles.length > 0 && !allowedNormalized.includes(normalizedRole)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <Outlet />;
};

export default ProtectedRoute;