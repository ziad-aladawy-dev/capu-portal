import { useNavigate } from "react-router-dom";
import { ChevronRight } from "lucide-react";
import { useTranslation } from "react-i18next";
import { quickActionsConfig } from "../data/dashboardData";

function QuickActions() {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className="dashboard-card anim-actions">
      <div className="card-header">
        <h3>{t("quick_actions")}</h3>
      </div>

      <div className="quick-actions-list">
        {quickActionsConfig.map((item, index) => (
          <button
            key={index}
            className="quick-action-btn"
            onClick={() => navigate(item.path)}
          >
            <span>{t(item.labelKey)}</span>
            <ChevronRight size={16} />
          </button>
        ))}
      </div>
    </div>
  );
}

export default QuickActions;