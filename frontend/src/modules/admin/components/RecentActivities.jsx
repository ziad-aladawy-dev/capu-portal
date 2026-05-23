import { useState, useEffect } from "react";
import { Activity } from "lucide-react";
import * as notificationService from "../../../core/services/notificationService";

function RecentActivities() {
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    notificationService
      .fetchAllNotifications()
      .then((data) => {
        if (cancelled) return;
        const list = Array.isArray(data) ? data.slice(0, 5) : [];
        setActivities(list);
      })
      .catch(() => {
        if (!cancelled) setActivities([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const formatTime = (iso) => {
    if (!iso) return "";
    const diff = (Date.now() - new Date(iso).getTime()) / 1000;
    if (diff < 60) return "just now";
    if (diff < 3600) return `${Math.floor(diff / 60)} min ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)} h ago`;
    return `${Math.floor(diff / 86400)} d ago`;
  };

  const DOT_COLORS = ["#16a34a", "#c9a84c", "#2e3591", "#be185d"];

  return (
    <div className="dashboard-card anim-activities">
      <div className="card-header">
        <h3>Recent Activities</h3>
        <Activity size={17} color="#c9a84c" />
      </div>

      {loading ? (
        <div style={{ padding: 20, textAlign: "center", color: "#9ca3af", fontSize: 13 }}>
          Loading…
        </div>
      ) : activities.length === 0 ? (
        <div style={{ padding: 20, textAlign: "center", color: "#9ca3af", fontSize: 13 }}>
          No recent activities
        </div>
      ) : (
        activities.map((item, index) => (
          <div className="activity-item" key={item.id || index}>
            <span
              className="activity-dot"
              style={{ background: DOT_COLORS[index % DOT_COLORS.length] }}
            />
            <div>
              <p>{item.title || item.message || "Activity"}</p>
              <span>{formatTime(item.createdAt)}</span>
            </div>
          </div>
        ))
      )}
    </div>
  );
}

export default RecentActivities;