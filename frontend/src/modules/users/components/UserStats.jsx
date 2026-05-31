import React from "react";
import { useTranslation } from "react-i18next";
import { Users, UserCheck, UserX, GraduationCap, Briefcase } from "lucide-react";

const UserStats = ({ statistics, loading }) => {
  const { t } = useTranslation();

  if (loading) return <div className="users-stats-loading">{t("loading_statistics")}...</div>;
  if (!statistics) return null;

  const stats = [
    { label: t("total_users"), value: statistics.totalUsers || 0, icon: Users, tone: "navy" },
    { label: t("active_users"), value: statistics.activeUsers || 0, icon: UserCheck, tone: "green" },
    { label: t("inactive_users"), value: statistics.inactiveUsers || 0, icon: UserX, tone: "red" },
    { label: t("students"), value: statistics.studentsCount || 0, icon: GraduationCap, tone: "blue" },
    { label: t("staff"), value: statistics.staffCount || 0, icon: Briefcase, tone: "purple" },
  ];

  return (
    <div className="users-stats-grid">
      {stats.map((stat) => (
        <article className="users-stat-card" key={stat.label}>
          <div>
            <span>{stat.label}</span>
            <h3>{stat.value.toLocaleString()}</h3>
          </div>
          <div className={`users-stat-icon ${stat.tone}`}>
            <stat.icon size={17} />
          </div>
        </article>
      ))}
    </div>
  );
};

export default UserStats;