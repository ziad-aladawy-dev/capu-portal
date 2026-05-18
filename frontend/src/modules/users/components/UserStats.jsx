import React from "react";
import { Users, UserCheck, UserX, GraduationCap, Briefcase } from "lucide-react";

const UserStats = ({ statistics, loading }) => {
  if (loading) return <div className="users-stats-loading">Loading statistics...</div>;
  if (!statistics) return null;

  const stats = [
    { label: "Total Users", value: statistics.totalUsers || 0, icon: Users, tone: "navy" },
    { label: "Active Users", value: statistics.activeUsers || 0, icon: UserCheck, tone: "green" },
    { label: "Inactive Users", value: statistics.inactiveUsers || 0, icon: UserX, tone: "red" },
    { label: "Students", value: statistics.studentsCount || 0, icon: GraduationCap, tone: "blue" },
    { label: "Staff", value: statistics.staffCount || 0, icon: Briefcase, tone: "purple" },
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