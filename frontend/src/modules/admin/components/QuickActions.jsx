import { useNavigate } from "react-router-dom";
import { ChevronRight } from "lucide-react";
import { quickActions } from "../data/dashboardData";

function QuickActions() {
  const navigate = useNavigate();

  return (
    <div className="dashboard-card anim-actions">
      <div className="card-header">
        <h3>Quick Actions</h3>
      </div>

      <div className="quick-actions-list">
        {quickActions.map((item, index) => (
          <button
            key={index}
            className="quick-action-btn"
            onClick={() => navigate(item.path)}
          >
            <span>{item.label}</span>
            <ChevronRight size={16} />
          </button>
        ))}
      </div>
    </div>
  );
}

export default QuickActions;