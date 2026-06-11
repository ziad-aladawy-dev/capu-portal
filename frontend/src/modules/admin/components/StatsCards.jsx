import { GraduationCap, Building2, BookOpen, UserCircle2 } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePermission } from "../../../core/auth/usePermission";
import { StatCard } from "../../../core/components/dashboard/DashboardKit";
import {
  useStudentStatistics,
  useStaffStatistics,
  useCourseCount,
  useFacultyCount,
  useProgramCount,
} from "../../../core/query/useDashboardStats";

const fmt = (v) => (v == null ? "—" : Number(v).toLocaleString("en-US"));

function StatsCards() {
  const { t } = useTranslation();
  const { can } = usePermission();

  const canUsers = can("users.users.view");
  const canCourses = can("courses.courses.view");
  const canStructure = can("structure.structure.view");

  const students = useStudentStatistics(canUsers);
  const staff = useStaffStatistics(canUsers);
  const courses = useCourseCount(canCourses);
  const faculties = useFacultyCount(canStructure);
  const programs = useProgramCount(canStructure);

  const cards = [
    canUsers && {
      key: "total_students",
      icon: GraduationCap,
      tone: "navy",
      value: fmt(students.data?.totalStudents),
      loading: students.isLoading,
      sub: students.data
        ? t("dash_sub_active_inactive", {
            active: fmt(students.data.activeStudents),
            inactive: fmt(students.data.inactiveStudents),
          })
        : null,
      subTone: "up",
    },
    canStructure && {
      key: "faculties",
      icon: Building2,
      tone: "gold",
      value: fmt(faculties.data),
      loading: faculties.isLoading,
      sub: programs.data != null ? t("dash_sub_programs", { count: fmt(programs.data) }) : null,
    },
    canCourses && {
      key: "active_courses",
      icon: BookOpen,
      tone: "blue",
      value: fmt(courses.data),
      loading: courses.isLoading,
      sub: t("dash_sub_in_catalog"),
    },
    canUsers && {
      key: "faculty_members",
      icon: UserCircle2,
      tone: "pink",
      value: fmt(staff.data?.totalStaff),
      loading: staff.isLoading,
      sub: staff.data
        ? t("dash_sub_active_inactive", {
            active: fmt(staff.data.activeStaff),
            inactive: fmt(staff.data.inactiveStaff),
          })
        : null,
      subTone: "up",
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
