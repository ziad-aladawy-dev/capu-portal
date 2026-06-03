import { BarChart3 } from "lucide-react";
import { useTranslation } from "react-i18next";

function DashboardHeader() {
  const { t } = useTranslation();

  return (
    <div className="dashboard-header">
      <div className="dashboard-title-box">
        <div className="dashboard-title-icon">
          <BarChart3 size={24} />
        </div>
        <div>
          <h1>{t("system_overview")}</h1>
          <div className="gold-line" />
        </div>
      </div>

      <div className="welcome-block">
        <p>{t("welcome_back_admin")}</p>
        <span>{t("whats_happening_today")}</span>
      </div>
    </div>
  );
}

export default DashboardHeader;