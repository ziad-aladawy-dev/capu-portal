import { Users, GraduationCap, ClipboardList, TrendingUp } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePermission } from "../../../core/auth/usePermission";
import { StatCard } from "../../../core/components/dashboard/DashboardKit";
import {
  useStudentStatistics,
  useStaffStatistics,
  useCourseCount,
  useProgramCount,
  useServiceRequestStats,
} from "../../../core/query/useDashboardStats";

const fmt = (v) => (v == null ? "—" : Number(v).toLocaleString("en-US"));
const fmtPct = (v) => (v == null ? "—" : `${Number(v).toFixed(1)}%`);

function StatsCards() {
  const { t } = useTranslation();
  const { can } = usePermission();

  const canUsers = can("users.users.view");
  const canCourses = can("courses.courses.view");
  const canStructure = can("structure.structure.view");
  const canServices = can("student-services.services.view");

  const students = useStudentStatistics(canUsers);
  const staff = useStaffStatistics(canUsers);
  const courses = useCourseCount(canCourses);
  const programs = useProgramCount(canStructure);
  const requests = useServiceRequestStats(canServices);

  const totalStudents = students.data?.totalStudents ?? 0;
  const totalStaff = staff.data?.totalStaff ?? 0;
  const ratio = totalStaff > 0 ? (totalStudents / totalStaff).toFixed(1) : null;
  const activeRate = totalStudents > 0 ? ((students.data?.activeStudents ?? 0) / totalStudents) * 100 : null;
  const totalReq = requests.data?.totalRequests ?? 0;
  const completedReq = requests.data?.completedRequests ?? 0;
  const completionRate = totalReq > 0 ? (completedReq / totalReq) * 100 : null;

  const cards = [
    canUsers && {
      key: "dash_kpi_ratio",
      icon: Users,
      tone: "navy",
      value: ratio ? `${ratio}:1` : "—",
      loading: students.isLoading || staff.isLoading,
    },
    canUsers && {
      key: "dash_kpi_active_rate",
      icon: TrendingUp,
      tone: "gold",
      value: fmtPct(activeRate),
      loading: students.isLoading,
      sub: activeRate != null
        ? t("dash_sub_of_total", { count: fmt(totalStudents) })
        : null,
      subTone: "up",
    },
    canServices && {
      key: "dash_kpi_completion",
      icon: ClipboardList,
      tone: "blue",
      value: fmtPct(completionRate),
      loading: requests.isLoading,
      sub: completionRate != null
        ? t("dash_sub_of_total", { count: fmt(totalReq) })
        : null,
    },
    canCourses && {
      key: "dash_kpi_catalog",
      icon: GraduationCap,
      tone: "pink",
      value: fmt(courses.data),
      loading: courses.isLoading,
      sub: programs.data != null ? t("dash_sub_programs", { count: fmt(programs.data) }) : null,
    },
  ].filter(Boolean);

  if (cards.length === 0) return null;

  return (
    <div className="dk-stat-grid" style={{ marginBottom: 18 }}>
      {cards.map((c) => (
        <StatCard
          key={c.key}
          icon={c.icon}
          tone={c.tone}
          label={t(c.key)}
          value={c.value}
          sub={c.sub}
          subTone={c.subTone}
          loading={c.loading}
        />
      ))}
    </div>
  );
}

export default StatsCards;
