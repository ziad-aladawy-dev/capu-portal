import { useTranslation } from "react-i18next";
import { useAuth } from "../../../core/auth/useAuth";
import DashboardGrid from "../components/widgets/DashboardGrid";
import "../styles/studentDashboard.css";

/**
 * Student dashboard / command center. Data fetching, loading, and error states
 * now live inside each widget (TanStack Query); this shell only renders the
 * greeting and the customizable widget grid.
 */
function StudentDashboard() {
  const { t } = useTranslation();
  const { user } = useAuth();

  return (
    <div className="student-dashboard">
      <div className="sd-header">
        <div className="sd-welcome">
          <h1>{t("welcome")}, {user?.name || t("student")}</h1>
          <p className="sd-subtitle">{t("academic_overview_subtitle")}</p>
        </div>
      </div>

      <DashboardGrid />
    </div>
  );
}

export default StudentDashboard;
