import { Navigate } from "react-router-dom";
import { useAuth } from "./useAuth";
import { usePermission } from "./usePermission";

function RouteGuard({ resource, minLevel = 1, fallback = "/admin/dashboard", children }) {
  const { isAuthenticated, isLoading } = useAuth();
  const { can } = usePermission();

  if (isLoading) {
    return <div className="route-guard-loading">Loading...</div>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/admin/login" replace />;
  }

  if (!can(resource, minLevel)) {
    return <Navigate to={fallback} replace />;
  }

  return children;
}

export default RouteGuard;
