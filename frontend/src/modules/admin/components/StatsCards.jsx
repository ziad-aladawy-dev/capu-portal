import { useState, useEffect } from "react";
import {
  GraduationCap, Building2, BookOpen, UserCircle2,
  TrendingUp, TrendingDown, Minus,
} from "lucide-react";
import * as studentService from "../../../core/services/studentService";
import * as staffService from "../../../core/services/staffService";

const ICON_MAP = {
  students: { Icon: GraduationCap, bg: "rgba(26,31,94,0.08)", color: "#1a1f5e" },
  staff: { Icon: UserCircle2, bg: "rgba(244,114,182,0.12)", color: "#be185d" },
  active: { Icon: TrendingUp, bg: "rgba(22,163,74,0.10)", color: "#16a34a" },
  inactive: { Icon: TrendingDown, bg: "rgba(201,168,76,0.12)", color: "#7a5c10" },
};

function StatsCards() {
  const [stats, setStats] = useState([
    { label: "Total Students", value: "—", key: "students" },
    { label: "Total Staff", value: "—", key: "staff" },
    { label: "Active Users", value: "—", key: "active" },
    { label: "Inactive Users", value: "—", key: "inactive" },
  ]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      studentService.fetchStudentStatistics({}).catch(() => null),
      staffService.fetchStaffStatistics({}).catch(() => null),
    ]).then(([studentStats, staffStats]) => {
      if (cancelled) return;

      const totalStudents = studentStats?.total ?? studentStats?.totalCount ?? 0;
      const activeStudents = studentStats?.active ?? studentStats?.activeCount ?? 0;
      const totalStaff = staffStats?.total ?? staffStats?.totalCount ?? 0;
      const activeStaff = staffStats?.active ?? staffStats?.activeCount ?? 0;

      const totalActive = activeStudents + activeStaff;
      const totalInactive = (totalStudents + totalStaff) - totalActive;

      setStats([
        { label: "Total Students", value: totalStudents.toLocaleString(), key: "students" },
        { label: "Total Staff", value: totalStaff.toLocaleString(), key: "staff" },
        { label: "Active Users", value: totalActive.toLocaleString(), key: "active" },
        { label: "Inactive Users", value: totalInactive.toLocaleString(), key: "inactive" },
      ]);
    });
    return () => { cancelled = true; };
  }, []);

  return (
    <div className="stats-grid">
      {stats.map((item, index) => {
        const meta = ICON_MAP[item.key] || ICON_MAP.students;
        const Icon = meta.Icon;
        return (
          <div className={`stat-card anim-s${index}`} key={item.key}>
            <div className="stat-card-top">
              <span>{item.label}</span>
              <div className="stat-icon" style={{ background: meta.bg, color: meta.color }}>
                <Icon size={17} />
              </div>
            </div>
            <h2>{item.value}</h2>
          </div>
        );
      })}
    </div>
  );
}

export default StatsCards;