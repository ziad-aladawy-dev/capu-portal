import { Users, GraduationCap, UserCheck, UserCog, PieChart, ArrowLeftRight } from "lucide-react";
import { useTranslation } from "react-i18next";
import PageHeader from "../../../core/components/PageHeader";
import { usePermission } from "../../../core/auth/usePermission";
import { StatCard, ChartCard, DonutChart } from "../../../core/components/dashboard/DashboardKit";
import { useStudentStatistics, useStaffStatistics } from "../../../core/query/useDashboardStats";

const fmt = (v) => (v == null ? "—" : Number(v).toLocaleString("en-US"));

function PeopleDashboardPage() {
  const { t } = useTranslation();
  const { can } = usePermission();
  const canUsers = can("users.users.view");

  const students = useStudentStatistics(canUsers);
  const staff = useStaffStatistics(canUsers);

  const s = students.data;
  const st = staff.data;

  const totalStudents = s?.totalStudents ?? 0;
  const totalStaff = st?.totalStaff ?? 0;
  const ratio = totalStaff > 0 ? (totalStudents / totalStaff).toFixed(1) : null;

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
              icon={ArrowLeftRight}
              tone="gold"
              label={t("people_dash_ratio")}
              value={ratio ? `${ratio}:1` : "—"}
              loading={students.isLoading || staff.isLoading}
              sub={ratio ? t("people_dash_student_staff") : null}
            />
            <StatCard
              icon={UserCog}
              tone="teal"
              label={t("people_dash_active_staff")}
              value={fmt(st?.activeStaff)}
              loading={staff.isLoading}
            />
          </div>

          <div className="dk-grid-2 dk-section">
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
          </div>
        </>
      )}
    </div>
  );
}

export default PeopleDashboardPage;
