import { Activity } from "lucide-react";
import { recentActivities } from "../data/dashboardData";

function RecentActivities() {
  return (
    <div className="dashboard-card anim-activities">
      <div className="card-header">
        <h3>Recent Activities</h3>
        <Activity size={17} color="#c9a84c" />
      </div>

      {recentActivities.map((item, index) => (
        <div className="activity-item" key={item.id}>
          <span
            className="activity-dot"
            style={{ background: item.dot }}
          />

          <div>
            <p>{item.action}</p>
            <span>
              {item.user} · {item.time}
            </span>
          </div>
        </div>
      ))}
    </div>
  );
}

export default RecentActivities;