import { Users, BookOpen, ClipboardList } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePermission } from "../../../core/auth/usePermission";
import {
  ChartCard, DonutChart, BarsChart,
} from "../../../core/components/dashboard/DashboardKit";
import {
  useStudentStatistics,
  useStaffStatistics,
  useServiceRequestStats,
  useCourseCount,
  useProgramCount,
  useFacultyCount,
} from "../../../core/query/useDashboardStats";

function InsightsCharts() {
  const { t } = useTranslation();
  const { can } = usePermission();

  const canUsers = can("users.users.view");
  const canCourses = can("courses.courses.view");
  const canStructure = can("structure.structure.view");
  const canServices = can("student-services.services.view");

  const students = useStudentStatistics(canUsers);
  const staff = useStaffStatistics(canUsers);
  const requests = useServiceRequestStats(canServices);
  const courses = useCourseCount(canCourses);
  const programs = useProgramCount(canStructure);
  const faculties = useFacultyCount(canStructure);

  if (!canUsers && !canCourses && !canServices && !canStructure) return null;

  const studentData = students.data
    ? [
        { name: t("dash_active"), value: students.data.activeStudents || 0, color: "#16a34a" },
        { name: t("dash_inactive"), value: students.data.inactiveStudents || 0, color: "#dc2626" },
      ]
    : [];

  const requestData = requests.data
    ? [
        { name: t("pending_requests"), value: requests.data.pendingRequests || 0, color: "#c9a84c" },
        { name: t("awaiting_approval"), value: requests.data.awaitingApproval || 0, color: "#2563eb" },
        { name: t("completed_requests"), value: requests.data.completedRequests || 0, color: "#16a34a" },
      ]
    : [];

  const hasAcademicData = (faculties.data != null || courses.data != null || programs.data != null);
  const academicData = hasAcademicData
    ? [
        ...(courses.data != null ? [{ name: t("courses"), value: courses.data || 0, color: "#2e3591" }] : []),
        ...(programs.data != null ? [{ name: t("programs"), value: programs.data || 0, color: "#c9a84c" }] : []),
        ...(faculties.data != null ? [{ name: t("faculties"), value: faculties.data || 0, color: "#2563eb" }] : []),
      ]
    : [];

  return (
    <div className="dk-grid-3" style={{ marginBottom: 18 }}>
      {canUsers && (
        <ChartCard
          icon={Users}
          title={t("dash_students_breakdown")}
          loading={students.isLoading}
          empty={!students.isLoading && studentData.every((d) => !d.value)}
          emptyLabel={t("dash_no_data")}
        >
          <DonutChart data={studentData} centerLabel={t("total_students")} />
        </ChartCard>
      )}
      {(canCourses || canStructure) && (
        <ChartCard
          icon={BookOpen}
          title={t("dash_academic_landscape")}
          loading={courses.isLoading || programs.isLoading || faculties.isLoading}
          empty={!hasAcademicData}
          emptyLabel={t("dash_no_data")}
        >
          <BarsChart data={academicData} />
        </ChartCard>
      )}
      {canServices && (
        <ChartCard
          icon={ClipboardList}
          title={t("dash_requests_pipeline")}
          loading={requests.isLoading}
          empty={!requests.isLoading && requestData.every((d) => !d.value)}
          emptyLabel={t("dash_no_data")}
        >
          <DonutChart
            data={requestData}
            centerValue={Number(requests.data?.totalRequests || 0).toLocaleString("en-US")}
            centerLabel={t("dash_requests_pipeline")}
          />
        </ChartCard>
      )}
    </div>
  );
}

export default InsightsCharts;
