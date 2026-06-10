import { memo } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  User, BarChart3, Calendar, Receipt, Bell, BookOpen, FileText,
  GraduationCap, CreditCard, ClipboardList, MapPin, Clock, Package,
  AlertTriangle, CheckCircle, CalendarClock,
} from "lucide-react";
import { useAuth } from "../../../../core/auth/useAuth";
import { useCountUp } from "../../../../core/hooks/useCountUp";
import { useStudentStatistics } from "../../../studentServices/hooks/useStatistics";
import ServiceCard from "../ServiceCard";
import WidgetShell from "./WidgetShell";
import {
  useAcademicOverview, useAvailableServices, useGradesSummary,
  useFinancialSnapshot, useTodaySchedule, useUnreadNotifications,
} from "../../hooks/useDashboardData";

const egp = (n) =>
  `EGP ${Number(n || 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`;

function Stat({ value, label, suffix = "", decimals = 0 }) {
  const animated = useCountUp(value, { duration: 900 });
  const shown = decimals ? animated.toFixed(decimals) : Math.round(animated);
  return (
    <div className="dw-stat">
      <span className="dw-stat-value">{shown}{suffix}</span>
      <span className="dw-stat-label">{label}</span>
    </div>
  );
}

/* ---------------- Profile snippet ---------------- */
export const ProfileWidget = memo(function ProfileWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data: grades, isLoading } = useGradesSummary();

  const gpa = grades?.cgpa ?? grades?.gpa ?? null;
  const standing = grades?.academicStanding ?? grades?.standing ?? null;
  const gpaClass = gpa == null ? "" : gpa >= 3 ? "good" : gpa >= 2 ? "warn" : "bad";

  return (
    <WidgetShell title={t("dashboard.profile", { defaultValue: "Profile" })} icon={User} to="/student/profile" toLabel={t("dashboard.view_profile", { defaultValue: "Profile" })} isLoading={isLoading}>
      <div className="dw-profile">
        <div className="dw-avatar">{(user?.name || "S").charAt(0).toUpperCase()}</div>
        <div className="dw-profile-meta">
          <strong>{user?.name || t("student", { defaultValue: "Student" })}</strong>
          <span>{user?.email}</span>
          <div className="dw-profile-tags">
            <span className={`dw-gpa ${gpaClass}`}>
              {t("dashboard.gpa", { defaultValue: "GPA" })}: {gpa != null ? Number(gpa).toFixed(2) : "—"}
            </span>
            {standing && <span className="dw-standing">{standing}</span>}
          </div>
        </div>
      </div>
    </WidgetShell>
  );
});

/* ---------------- Academic stats ---------------- */
export const StatsWidget = memo(function StatsWidget() {
  const { t } = useTranslation();
  const { activeScope } = useAuth();
  const { data, isLoading, isError, refetch } = useAcademicOverview(activeScope);

  return (
    <WidgetShell
      title={t("dashboard.academic_stats", { defaultValue: "Academic Stats" })}
      icon={BarChart3}
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data}
      emptyIcon={BookOpen}
      emptyText={t("dashboard.no_scope", { defaultValue: "Select a semester to see your stats" })}
    >
      <div className="dw-stats-row">
        <Stat value={data?.courseCount ?? 0} label={t("dashboard.courses", { defaultValue: "Courses" })} />
        <Stat value={data?.totalCredits ?? 0} label={t("dashboard.credits", { defaultValue: "Credits" })} />
        <Stat value={data?.offeringCount ?? 0} label={t("dashboard.offerings", { defaultValue: "Offerings" })} />
      </div>
      {data?.semesterName && <p className="dw-muted">{data.semesterName}</p>}
    </WidgetShell>
  );
});

/* ---------------- Action center ---------------- */
export const ActionCenterWidget = memo(function ActionCenterWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const fin = useFinancialSnapshot(user?.id);
  const notif = useUnreadNotifications();
  const { stats, loading: statsLoading } = useStudentStatistics();

  const outstanding = fin.data?.outstanding ?? 0;
  const unread = notif.data?.length ?? 0;
  const pending = stats?.pendingRequests ?? 0;
  const items = [];
  if (outstanding > 0)
    items.push({ key: "fees", tone: "red", icon: CreditCard, label: t("dashboard.unpaid_fees", { defaultValue: "Unpaid fees" }), value: egp(outstanding), to: "/student/payments" });
  if (pending > 0)
    items.push({ key: "requests", tone: "orange", icon: ClipboardList, label: t("dashboard.pending_requests", { defaultValue: "Pending requests" }), value: pending, to: "/student/requests" });
  if (unread > 0)
    items.push({ key: "notif", tone: "blue", icon: Bell, label: t("dashboard.unread_notifications", { defaultValue: "Unread notifications" }), value: unread, to: "/student/notifications" });

  return (
    <WidgetShell
      title={t("dashboard.action_center", { defaultValue: "Action Center" })}
      icon={AlertTriangle}
      isLoading={fin.isLoading || notif.isLoading || statsLoading}
      isEmpty={items.length === 0}
      emptyIcon={CheckCircle}
      emptyText={t("dashboard.all_clear", { defaultValue: "You're all caught up!" })}
    >
      <ul className="dw-actions">
        {items.map((it) => (
          <li key={it.key} className={`dw-action tone-${it.tone}`}>
            <it.icon size={16} />
            <span className="dw-action-label">{it.label}</span>
            <Link to={it.to} className="dw-action-value">{it.value}</Link>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* ---------------- Today's schedule ---------------- */
export const ScheduleWidget = memo(function ScheduleWidget() {
  const { t } = useTranslation();
  const { activeScope } = useAuth();
  const { data, isLoading, isError, refetch } = useTodaySchedule(activeScope);

  return (
    <WidgetShell
      title={t("dashboard.today_schedule", { defaultValue: "Today's Schedule" })}
      icon={CalendarClock}
      to="/student/schedule"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data || data.length === 0}
      emptyIcon={Calendar}
      emptyText={t("dashboard.no_classes_today", { defaultValue: "No classes scheduled today" })}
    >
      <ul className="dw-schedule">
        {(data || []).map((s, i) => (
          <li key={s.id ?? i}>
            <Clock size={14} />
            <span className="dw-time">{s.startTime ?? "—"}</span>
            <span className="dw-course">{s.courseTitle ?? s.courseCode ?? s.title ?? `Class ${i + 1}`}</span>
            {s.room && <span className="dw-room"><MapPin size={12} /> {s.room}</span>}
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* ---------------- Financial snapshot ---------------- */
export const FinancialWidget = memo(function FinancialWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, isError, refetch } = useFinancialSnapshot(user?.id);

  const total = data?.total ?? 0;
  const paid = data?.paid ?? 0;
  const pct = total > 0 ? Math.min(100, Math.round((paid / total) * 100)) : 0;

  return (
    <WidgetShell
      title={t("dashboard.financial", { defaultValue: "Financial Snapshot" })}
      icon={Receipt}
      to="/student/payments"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data || data.invoiceCount === 0}
      emptyIcon={Receipt}
      emptyText={t("dashboard.no_invoices", { defaultValue: "No invoices yet" })}
    >
      <div className="dw-financial">
        <div className="dw-financial-amounts">
          <div>
            <span className="dw-muted">{t("dashboard.outstanding", { defaultValue: "Outstanding" })}</span>
            <strong className={data?.outstanding > 0 ? "dw-due" : ""}>{egp(data?.outstanding)}</strong>
          </div>
          <div className="dw-right">
            <span className="dw-muted">{t("dashboard.total", { defaultValue: "Total" })}</span>
            <strong>{egp(total)}</strong>
          </div>
        </div>
        <div className="dw-progress"><div className="dw-progress-fill" style={{ width: `${pct}%` }} /></div>
        <span className="dw-muted">{pct}% {t("dashboard.paid", { defaultValue: "paid" })}</span>
      </div>
    </WidgetShell>
  );
});

/* ---------------- Quick links ---------------- */
const QUICK_LINKS = [
  { to: "/student/courses", icon: BookOpen, key: "my_courses", label: "My Courses" },
  { to: "/student/courses/register", icon: ClipboardList, key: "register", label: "Register" },
  { to: "/student/grades", icon: GraduationCap, key: "grades", label: "Grades" },
  { to: "/student/schedule", icon: Calendar, key: "schedule", label: "Schedule" },
  { to: "/student/payments", icon: CreditCard, key: "payments", label: "Payments" },
  { to: "/student/services", icon: Package, key: "services", label: "Services" },
  { to: "/student/profile", icon: User, key: "profile", label: "Profile" },
  { to: "/student/notifications", icon: Bell, key: "notifications", label: "Notifications" },
];

export const QuickLinksWidget = memo(function QuickLinksWidget() {
  const { t } = useTranslation();
  return (
    <WidgetShell title={t("dashboard.quick_links", { defaultValue: "Quick Links" })} icon={FileText}>
      <div className="dw-quicklinks">
        {QUICK_LINKS.map((q, i) => (
          <Link key={q.key} to={q.to} className="dw-quicklink" style={{ animationDelay: `${i * 40}ms` }}>
            <q.icon size={20} />
            <span>{t(`dashboard.ql_${q.key}`, { defaultValue: q.label })}</span>
          </Link>
        ))}
      </div>
    </WidgetShell>
  );
});

/* ---------------- Available services ---------------- */
export const ServicesWidget = memo(function ServicesWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, isError, refetch } = useAvailableServices(user?.id);

  return (
    <WidgetShell
      title={t("dashboard.available_services", { defaultValue: "Available Services" })}
      icon={Package}
      to="/student/services"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data || data.length === 0}
      emptyIcon={Package}
      emptyText={t("dashboard.no_services", { defaultValue: "No services available" })}
      skeletonLines={2}
    >
      <div className="dw-services">
        {(data || []).slice(0, 6).map((service) => (
          <ServiceCard key={service.id} service={service} />
        ))}
      </div>
    </WidgetShell>
  );
});

export const WIDGET_REGISTRY = {
  profile: { component: ProfileWidget, span: 1 },
  stats: { component: StatsWidget, span: 1 },
  actions: { component: ActionCenterWidget, span: 1 },
  schedule: { component: ScheduleWidget, span: 1 },
  financial: { component: FinancialWidget, span: 1 },
  quicklinks: { component: QuickLinksWidget, span: 2 },
  services: { component: ServicesWidget, span: 2 },
};
