import { Activity } from "lucide-react";
import { useTranslation } from "react-i18next";
import { recentActivitiesData } from "../data/dashboardData";

function RecentActivities() {
  const { t } = useTranslation();

  const formatAction = (item) => {
    if (item.actionKey === "course_updated") {
      return t("course_updated", { courseName: item.courseName });
    }
    return t(item.actionKey);
  };

  const formatTime = (item) => {
    if (item.timeKey === "minutes_ago") {
      return t("minutes_ago", { minutes: item.timeValue });
    } else if (item.timeKey === "hour_ago") {
      return t("hour_ago");
    } else if (item.timeKey === "hours_ago") {
      return t("dashboard_hours_ago", { hours: item.timeValue });
    }
    return "";
  };

  return (
    <div className="dashboard-card anim-activities">
      <div className="card-header">
        <h3>{t("recent_activities")}</h3>
        <Activity size={17} color="#c9a84c" />
      </div>

      {recentActivitiesData.map((item) => (
        <div className="activity-item" key={item.id}>
          <span
            className="activity-dot"
            style={{ background: item.dot }}
          />
          <div>
            <p>{formatAction(item)}</p>
            <span>
              {item.user} · {formatTime(item)}
            </span>
          </div>
        </div>
      ))}
    </div>
  );
}

export default RecentActivities;