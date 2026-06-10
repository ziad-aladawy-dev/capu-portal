import { useNavigate } from "react-router-dom";
import { ChevronRight, ExternalLink } from "lucide-react";
import { useTranslation } from "react-i18next";
import { quickActionsConfig } from "../data/dashboardData";
import { useScopeAwareUI } from "../../../core/hooks/useScopeAwareUI";

function QuickActions() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { relevantActions } = useScopeAwareUI();

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
        {relevantActions.length > 0 && (
          <>
            <div className="quick-actions-divider" style={{ height: 1, background: "#e5e7eb", margin: "4px 0" }} />
            {relevantActions.map((action) => {
              const Icon = action.icon;
              return (
                <button
                  key={action.capability}
                  className="quick-action-btn"
                  onClick={() => navigate(action.path)}
                >
                  <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                    <Icon size={14} />
                    {t(action.labelKey)}
                  </span>
                  <ExternalLink size={14} />
                </button>
              );
            })}
          </>
        )}
      </div>
    </div>
  );
}

export default QuickActions;