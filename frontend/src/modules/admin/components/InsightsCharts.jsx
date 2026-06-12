import { Users, CalendarCheck, ClipboardList } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePermission } from "../../../core/auth/usePermission";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { ChartCard, DonutChart } from "../../../core/components/dashboard/DashboardKit";
import { useOfferingStats } from "../../../core/query/useCourseOfferings";
import { useStudentStatistics, useServiceRequestStats } from "../../../core/query/useDashboardStats";

/** Permission-gated analytics row: each donut renders only when the viewer
 *  holds the underlying module permission, so the dashboard never 403s. */
function InsightsCharts() {
  const { t } = useTranslation();
  const { can } = usePermission();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();

  const canUsers = can("users.users.view");
  const canOfferings = can("course-offerings.course-offerings.view");
  const canServices = can("student-services.services.view");

  const students = useStudentStatistics(canUsers);
  const offerings = useOfferingStats(canOfferings ? selectedSemesterObj?.id : undefined, scopeNode?.id);
  const requests = useServiceRequestStats(canServices);

  if (!canUsers && !canOfferings && !canServices) return null;

  const studentData = students.data
    ? [
        { name: t("dash_active"), value: students.data.activeStudents || 0, color: "#16a34a" },
        { name: t("dash_inactive"), value: students.data.inactiveStudents || 0, color: "#dc2626" },
      ]
    : [];

  const offeringData = offerings.data
    ? [
        { name: t("acad_dash_status_open"), value: offerings.data.openCount || 0, color: "#16a34a" },
        { name: t("acad_dash_status_draft"), value: offerings.data.draftCount || 0, color: "#c9a84c" },
        { name: t("acad_dash_status_full"), value: offerings.data.fullCount || 0, color: "#2563eb" },
        { name: t("acad_dash_status_closed"), value: offerings.data.closedCount || 0, color: "#64748b" },
        { name: t("acad_dash_status_cancelled"), value: offerings.data.cancelledCount || 0, color: "#dc2626" },
      ]
    : [];

  const requestData = requests.data
    ? [
        { name: t("pending_requests"), value: requests.data.pendingRequests || 0, color: "#c9a84c" },
        { name: t("awaiting_approval"), value: requests.data.awaitingApproval || 0, color: "#2563eb" },
        { name: t("completed_requests"), value: requests.data.completedRequests || 0, color: "#16a34a" },
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
      {canOfferings && (
        <ChartCard
          icon={CalendarCheck}
          title={t("dash_offerings_by_status")}
          loading={!!selectedSemesterObj && offerings.isLoading}
          empty={!selectedSemesterObj || (!offerings.isLoading && !offerings.data?.total)}
          emptyLabel={!selectedSemesterObj ? t("dash_select_semester") : t("dash_no_data")}
        >
          <DonutChart data={offeringData} centerLabel={t("acad_dash_offerings")} />
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
