import { BookOpen, CalendarCheck, ClipboardList, GraduationCap, PieChart, Gauge } from "lucide-react";
import { useTranslation } from "react-i18next";
import PageHeader from "../../../../core/components/PageHeader";
import { usePermission } from "../../../../core/auth/usePermission";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { StatCard, ChartCard, DonutChart } from "../../../../core/components/dashboard/DashboardKit";
import { useOfferingStats } from "../../../../core/query/useCourseOfferings";
import {
  useCourseCount,
  useAcademicPlanCount,
  useProgramCount,
} from "../../../../core/query/useDashboardStats";

const fmt = (v) => (v == null ? "—" : Number(v).toLocaleString("en-US"));

/** Analytics overview for the Academic suite. Route-gated by the dashboard
 *  permission; each widget additionally requires its module's view grant. */
function AcademicDashboardPage() {
  const { t } = useTranslation();
  const { can } = usePermission();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();

  const canCourses = can("courses.courses.view");
  const canPlans = can("courses.academic-plans.view");
  const canPrograms = can("programs.programs.view");
  const canOfferings = can("course-offerings.course-offerings.view");

  const courses = useCourseCount(canCourses);
  const plans = useAcademicPlanCount(canPlans);
  const programs = useProgramCount(canPrograms);
  const offerings = useOfferingStats(canOfferings ? selectedSemesterObj?.id : undefined, scopeNode?.id);

  const stats = offerings.data;
  const registered = stats?.totalRegistered || 0;
  const capacity = stats?.totalCapacity || 0;
  const fillPct = capacity > 0 ? Math.round((registered / capacity) * 100) : 0;

  const statusData = stats
    ? [
        { name: t("acad_dash_status_open"), value: stats.openCount || 0, color: "#16a34a" },
        { name: t("acad_dash_status_draft"), value: stats.draftCount || 0, color: "#c9a84c" },
        { name: t("acad_dash_status_full"), value: stats.fullCount || 0, color: "#2563eb" },
        { name: t("acad_dash_status_closed"), value: stats.closedCount || 0, color: "#64748b" },
        { name: t("acad_dash_status_cancelled"), value: stats.cancelledCount || 0, color: "#dc2626" },
      ]
    : [];

  const capacityData = [
    { name: t("acad_dash_registered"), value: registered, color: "#2e3591" },
    { name: t("acad_dash_available"), value: Math.max(0, capacity - registered), color: "#c9a84c" },
  ];

  const noSemester = !selectedSemesterObj;

  return (
    <div className="dk-page">
      <PageHeader
        icon={PieChart}
        kicker={t("dash_overview_label")}
        title={t("acad_dash_title")}
        subtitle={t("acad_dash_subtitle")}
      />

      <div className="dk-stat-grid" style={{ marginTop: 18 }}>
        {canCourses && (
          <StatCard
            icon={BookOpen}
            tone="navy"
            label={t("acad_dash_courses")}
            value={fmt(courses.data)}
            loading={courses.isLoading}
            sub={t("dash_sub_in_catalog")}
          />
        )}
        {canOfferings && (
          <StatCard
            icon={CalendarCheck}
            tone="gold"
            label={t("acad_dash_offerings")}
            value={noSemester ? "—" : fmt(stats?.total)}
            loading={!noSemester && offerings.isLoading}
            sub={
              !noSemester && stats
                ? `${fmt(registered)} / ${fmt(capacity)} ${t("acad_dash_seats")}`
                : null
            }
          />
        )}
        {canPlans && (
          <StatCard
            icon={ClipboardList}
            tone="blue"
            label={t("acad_dash_plans")}
            value={fmt(plans.data)}
            loading={plans.isLoading}
          />
        )}
        {canPrograms && (
          <StatCard
            icon={GraduationCap}
            tone="pink"
            label={t("acad_dash_programs")}
            value={fmt(programs.data)}
            loading={programs.isLoading}
          />
        )}
      </div>

      {canOfferings && (
        <div className="dk-grid-2 dk-section">
          <ChartCard
            icon={CalendarCheck}
            title={t("acad_dash_offering_status")}
            loading={!noSemester && offerings.isLoading}
            empty={noSemester || (!offerings.isLoading && !stats?.total)}
            emptyLabel={noSemester ? t("dash_select_semester") : t("dash_no_data")}
          >
            <DonutChart data={statusData} centerLabel={t("acad_dash_offerings")} />
          </ChartCard>
          <ChartCard
            icon={Gauge}
            title={t("acad_dash_capacity")}
            subtitle={t("acad_dash_capacity_sub")}
            loading={!noSemester && offerings.isLoading}
            empty={noSemester || (!offerings.isLoading && capacity === 0)}
            emptyLabel={noSemester ? t("dash_select_semester") : t("dash_no_data")}
          >
            <DonutChart
              data={capacityData}
              centerValue={`${fillPct}%`}
              centerLabel={t("acad_dash_registered")}
            />
          </ChartCard>
        </div>
      )}
    </div>
  );
}

export default AcademicDashboardPage;
