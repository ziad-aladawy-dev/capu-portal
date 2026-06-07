import { Link } from "react-router-dom";
import { ShieldX } from "lucide-react";

/**
 * Forbidden (403) page shown when a user lacks permission for a route.
 */
function ForbiddenPage() {
  return (
    <div className="error-page-container">
      <div className="error-page-card">
        <div className="error-page-icon forbidden">
          <ShieldX size={48} />
        </div>
        <h1 className="error-page-code">403</h1>
        <h2 className="error-page-title">Access Denied</h2>
        <p className="error-page-desc">
          You don't have permission to view this page.
          Contact your system administrator if you believe this is a mistake.
        </p>
        <Link to="/admin/dashboard" className="error-page-link">
          ← Back to Dashboard
        </Link>
      </div>
    </div>
  );
}

export default ForbiddenPage;
