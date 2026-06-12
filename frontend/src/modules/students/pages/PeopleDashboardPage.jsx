import { Users, GraduationCap, UserCheck, UserCog, PieChart } from "lucide-react";
import { useTranslation } from "react-i18next";
import PageHeader from "../../../core/components/PageHeader";
import { usePermission } from "../../../core/auth/usePermission";
import { StatCard, ChartCard, DonutChart, BarsChart } from "../../../core/components/dashboard/DashboardKit";
import { useStudentStatistics, useStaffStatistics } from "../../../core/query/useDashboardStats";

const fmt = (v) => (v == null ? "—" : Number(v).toLocaleString("en-US"));

/** People Management overview: student/staff population analytics. Route is
 *  gated by the dashboard permission; data widgets need users.users.view. */
function PeopleDashboardPage() {
  const { t } = useTranslation();
  const { can } = usePermission();
  const canUsers = can("users.users.view");

  const students = useStudentStatistics(canUsers);
  const staff = useStaffStatistics(canUsers);

  const s = students.data;
  const st = staff.data;

  const studentStatus = s
    ? [
        { name: t("dash_active"), value: s.activeStudents || 0, color: "#16a34a" },
        { name: t("dash_inactive"), value: s.inactiveStudents || 0, color: "#dc2626" },
      ]
    : [];

  const staffStatus = st
    ? [
        { name: t("dash_active"), value: st.activeStaff || 0, color: "#2e3591" },
        { name: t("dash_inactive"), value: st.inactiveStaff || 0, color: "#c9a84c" },
      ]
    : [];

  const composition = [
    { name: t("people_dash_students"), value: s?.totalStudents || 0, color: "#2e3591" },
    { name: t("people_dash_staff"), value: st?.totalStaff || 0, color: "#c9a84c" },
  ];

  return (
    <div className="dk-page">
      <PageHeader
        icon={PieChart}
        kicker={t("people_dash_label")}
        title={t("people_dash_title")}
        subtitle={t("people_dash_subtitle")}
      />

      {!canUsers ? (
        <p style={{ marginTop: 18, color: "var(--color-text-secondary)" }}>
          {t("dash_no_permission_widgets")}
        </p>
      ) : (
        <>
          <div className="dk-stat-grid" style={{ marginTop: 18 }}>
            <StatCard
              icon={GraduationCap}
              tone="navy"
              label={t("total_students")}
              value={fmt(s?.totalStudents)}
              loading={students.isLoading}
            />
            <StatCard
              icon={UserCheck}
              tone="green"
              label={t("people_dash_active_students")}
              value={fmt(s?.activeStudents)}
              loading={students.isLoading}
            />
            <StatCard
              icon={Users}
              tone="gold"
              label={t("people_dash_total_staff")}
              value={fmt(st?.totalStaff)}
              loading={staff.isLoading}
            />
            <StatCard
              icon={UserCog}
              tone="teal"
              label={t("people_dash_active_staff")}
              value={fmt(st?.activeStaff)}
              loading={staff.isLoading}
            />
          </div>

          <div className="dk-grid-3 dk-section">
            <ChartCard
              icon={GraduationCap}
              title={t("people_dash_students_status")}
              loading={students.isLoading}
              empty={!students.isLoading && studentStatus.every((d) => !d.value)}
              emptyLabel={t("dash_no_data")}
            >
              <DonutChart data={studentStatus} centerLabel={t("total_students")} />
            </ChartCard>
            <ChartCard
              icon={Users}
              title={t("people_dash_staff_status")}
              loading={staff.isLoading}
              empty={!staff.isLoading && staffStatus.every((d) => !d.value)}
              emptyLabel={t("dash_no_data")}
            >
              <DonutChart data={staffStatus} centerLabel={t("people_dash_total_staff")} />
            </ChartCard>
            <ChartCard
              icon={PieChart}
              title={t("people_dash_composition")}
              loading={students.isLoading || staff.isLoading}
              empty={!students.isLoading && !staff.isLoading && composition.every((d) => !d.value)}
              emptyLabel={t("dash_no_data")}
            >
              <BarsChart data={composition} height={266} />
            </ChartCard>
          </div>
        </>
      )}
    </div>
  );
}

export default PeopleDashboardPage;
