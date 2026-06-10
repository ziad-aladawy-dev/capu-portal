import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./useAuth";
import { usePermission } from "./usePermission";

function RouteGuard({ resource, minLevel = 1, fallback = "/admin/dashboard", children }) {
  const { isAuthenticated, isLoading } = useAuth();
  const { can } = usePermission();
  const location = useLocation();

  if (isLoading) {
    return <div className="route-guard-loading">Loading...</div>;
  }

  if (!isAuthenticated) {
    const loginPath = location.pathname.startsWith("/student") ? "/student/login" : "/admin/login";
    return <Navigate to={loginPath} replace />;
  }

  if (resource && !can(resource, minLevel)) {
    // A student must never be dumped into the admin shell — send them back
    // to their login with an explanation instead of the admin fallback.
    if (location.pathname.startsWith("/student")) {
      return <Navigate to="/student/login?session=unauthorized" replace />;
    }
    return <Navigate to={fallback} replace />;
  }

  return children;
}

export default RouteGuard;
