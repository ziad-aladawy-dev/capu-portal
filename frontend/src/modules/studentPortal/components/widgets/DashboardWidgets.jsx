import { memo } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  BookOpen, Calendar, CalendarClock, Bell, GraduationCap, CreditCard,
  ClipboardList, MapPin, Clock, User, TrendingUp, CheckCircle2,
  Hourglass, Zap, Package,
} from "lucide-react";
import { useAuth } from "../../../../core/auth/useAuth";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { getLocalized } from "../../../../core/utils/getLocalized";
import PortalBadge from "../shared/PortalBadge";
import PortalStatCard from "../shared/PortalStatCard";
import { REQUEST_STATUS_LABELS } from "../../../../core/constants/requestStatus";
import WidgetShell from "./WidgetShell";
import {
  useAcademicOverview, useGradesSummary, useFinancialSnapshot,
  useTodaySchedule, useUnreadNotifications, useOpenRequests,
} from "../../hooks/useDashboardData";
import styles from "./widgets.module.css";

const egp = (n, t) =>
  `${Number(n || 0).toLocaleString("en-US", { maximumFractionDigits: 0 })} ${t("egp", { defaultValue: "EGP" })}`;

const STATUS_TONE = {
  2: "warning",   // Pending
  3: "info",      // Under Review
  4: "danger",    // More Info Required
  7: "accent",    // Payment Pending
  10: "success",  // Ready for Pickup
};

/* Upcoming Schedule (span 2) */
export const UpcomingScheduleWidget = memo(function UpcomingScheduleWidget() {
  const { t } = useTranslation();
  const { activeScope } = useAuth();
  const { data, isLoading, isError, refetch } = useTodaySchedule(activeScope);
  const next = (data || []).slice(0, 3);

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_schedule", { defaultValue: "Upcoming Schedule" })}
      icon={CalendarClock}
      to="/student/schedule"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={next.length === 0}
      emptyIcon={Calendar}
      emptyText={t("portal_dashboard.no_classes_today", { defaultValue: "No classes scheduled today" })}
    >
      <ul className={styles.scheduleList}>
        {next.map((s, i) => (
          <li key={s.id ?? i} className={styles.scheduleItem}>
            <span className={styles.scheduleTime}>
              <Clock size={13} /> {String(s.startTime ?? "").slice(0, 5)}
            </span>
            <span className={styles.scheduleCourse}>
              {s.courseTitle ?? s.courseCode ?? s.title ?? t("portal_dashboard.class", { defaultValue: "Class" })}
            </span>
            {s.room && (
              <span className={styles.scheduleRoom}>
                <MapPin size={12} /> {s.room}
              </span>
            )}
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* Active Courses (span 2) */
export const ActiveCoursesWidget = memo(function ActiveCoursesWidget() {
  const { t } = useTranslation();
  const { activeScope } = useAuth();
  const { data, isLoading, isError, refetch } = useAcademicOverview(activeScope);

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_courses", { defaultValue: "Active Courses" })}
      icon={BookOpen}
      to="/student/courses"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data || data.courseCount === 0}
      emptyIcon={BookOpen}
      emptyText={t("portal_dashboard.no_scope", { defaultValue: "Select a semester to see your courses" })}
    >
      <div className={styles.statsRow}>
        <PortalStatCard value={data?.courseCount ?? 0} label={t("portal_dashboard.courses", { defaultValue: "Courses" })} />
        <PortalStatCard value={data?.totalCredits ?? 0} label={t("portal_dashboard.credits", { defaultValue: "Credits" })} tone="accent" />
      </div>
      <ul className={styles.courseList}>
        {(data?.courses ?? []).slice(0, 4).map((c) => (
          <li key={c.id} className={styles.courseItem}>
            <span className={styles.courseCode}>{c.code}</span>
            <span className={styles.courseTitle}>{c.title}</span>
            <PortalBadge tone="primary">{c.creditHours} {t("portal_dashboard.cr", { defaultValue: "cr" })}</PortalBadge>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* Recent Grades (span 1) */
export const RecentGradesWidget = memo(function RecentGradesWidget() {
  const { t } = useTranslation();
  const { data, isLoading } = useGradesSummary();
  const gpa = data?.cgpa ?? data?.gpa ?? null;
  const standing = data?.academicStanding ?? data?.standing ?? null;

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_grades", { defaultValue: "Recent Grades" })}
      icon={GraduationCap}
      to="/student/grades"
      isLoading={isLoading}
      isEmpty={gpa == null}
      emptyIcon={GraduationCap}
      emptyText={t("portal_dashboard.no_grades", { defaultValue: "No grades published yet" })}
    >
      <div className={styles.gradeBlock}>
        <span className={styles.gradeGpa}>{Number(gpa).toFixed(2)}</span>
        <span className={styles.gradeLabel}>
          <TrendingUp size={13} /> {t("portal_dashboard.gpa", { defaultValue: "GPA" })}
        </span>
        {standing && <PortalBadge tone="success">{standing}</PortalBadge>}
      </div>
    </WidgetShell>
  );
});

/* Pending Requests (span 1) */
export const PendingRequestsWidget = memo(function PendingRequestsWidget() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useOpenRequests();
  const open = data ?? [];

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_requests", { defaultValue: "Pending Requests" })}
      icon={ClipboardList}
      to="/student/requests"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={open.length === 0}
      emptyIcon={CheckCircle2}
      emptyText={t("portal_dashboard.no_open_requests", { defaultValue: "No requests in progress" })}
    >
      <ul className={styles.requestList}>
        {open.slice(0, 3).map((r) => (
          <li key={r.id} className={styles.requestItem}>
            <Link to={`/student/requests/${r.id}`} className={styles.requestName}>
              {r.serviceName ?? r.requestNumber ?? t("portal_dashboard.request", { defaultValue: "Request" })}
            </Link>
            <PortalBadge tone={STATUS_TONE[r.status] ?? "neutral"}>
              {REQUEST_STATUS_LABELS[r.status]
                ? t(`portal_requests.status_${REQUEST_STATUS_LABELS[r.status].replace(/\s+/g, "")}`, {
                    defaultValue: REQUEST_STATUS_LABELS[r.status],
                  })
                : "?"}
            </PortalBadge>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* Fee Status (span 2) */
export const FeeStatusWidget = memo(function FeeStatusWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, isError, refetch } = useFinancialSnapshot(user?.id);

  const total = data?.total ?? 0;
  const paid = data?.paid ?? 0;
  const outstanding = data?.outstanding ?? 0;
  const pct = total > 0 ? Math.min(100, Math.round((paid / total) * 100)) : 0;

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_fees", { defaultValue: "Fee Status" })}
      icon={CreditCard}
      to="/student/payments"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={!data || data.invoiceCount === 0}
      emptyIcon={CreditCard}
      emptyText={t("portal_dashboard.no_invoices", { defaultValue: "No fees on record — you're all set" })}
    >
      <div className={styles.feeAmounts}>
        <div>
          <span className={styles.feeLabel}>{t("portal_dashboard.outstanding", { defaultValue: "Outstanding" })}</span>
          <strong className={outstanding > 0 ? styles.feeDue : styles.feeOk}>{egp(outstanding, t)}</strong>
        </div>
        <div className={styles.feeEnd}>
          <span className={styles.feeLabel}>{t("portal_dashboard.total", { defaultValue: "Total" })}</span>
          <strong>{egp(total, t)}</strong>
        </div>
      </div>
      <div className={styles.feeBar}>
        <div className={styles.feeFill} style={{ width: `${pct}%` }} />
      </div>
      <div className={styles.feeFooter}>
        <span className={styles.feeLabel}>{pct}% {t("portal_dashboard.paid", { defaultValue: "paid" })}</span>
        {outstanding > 0 && (
          <Link to="/student/payments" className={styles.payNow}>
            {t("portal_dashboard.pay_now", { defaultValue: "Pay now" })}
          </Link>
        )}
      </div>
    </WidgetShell>
  );
});

/* Recent Notifications (span 2) */
export const RecentNotificationsWidget = memo(function RecentNotificationsWidget() {
  const { t, i18n } = useTranslation();
  const { data, isLoading } = useUnreadNotifications();
  const latest = (data ?? []).slice(0, 3);

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_notifications", { defaultValue: "Recent Notifications" })}
      icon={Bell}
      to="/student/notifications"
      isLoading={isLoading}
      isEmpty={latest.length === 0}
      emptyIcon={Bell}
      emptyText={t("portal_dashboard.no_unread", { defaultValue: "You're all caught up" })}
    >
      <ul className={styles.notifList}>
        {latest.map((n) => (
          <li key={n.id} className={styles.notifItem}>
            <span className={styles.notifDot} />
            <div className={styles.notifBody}>
              <strong>{getLocalized(n.title, i18n.language)}</strong>
              {n.body && <span>{getLocalized(n.body, i18n.language)}</span>}
            </div>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* Quick Actions (span 4) */
const QUICK_ACTIONS = [
  { to: "/student/services", icon: Package, key: "request_service", label: "Request Service" },
  { to: "/student/schedule", icon: Calendar, key: "view_schedule", label: "View Schedule" },
  { to: "/student/payments", icon: CreditCard, key: "pay_fees", label: "Pay Fees" },
  { to: "/student/courses", icon: BookOpen, key: "my_courses", label: "My Courses" },
  { to: "/student/grades", icon: GraduationCap, key: "grades", label: "Grades" },
  { to: "/student/profile", icon: User, key: "profile", label: "Profile" },
];

export const QuickActionsWidget = memo(function QuickActionsWidget() {
  const { t } = useTranslation();
  return (
    <WidgetShell title={t("portal_dashboard.widget_quickactions", { defaultValue: "Quick Actions" })} icon={Zap}>
      <div className={styles.quickGrid}>
        {QUICK_ACTIONS.map((q) => (
          <Link key={q.key} to={q.to} className={styles.quickAction}>
            <q.icon size={19} />
            <span>{t(`portal_dashboard.qa_${q.key}`, { defaultValue: q.label })}</span>
          </Link>
        ))}
      </div>
    </WidgetShell>
  );
});

/* Academic Calendar (span 1) */
export const AcademicCalendarWidget = memo(function AcademicCalendarWidget() {
  const { t, i18n } = useTranslation();
  const { selectedSemesterObj } = useAcademic();

  const endDate = selectedSemesterObj?.endDate ? new Date(selectedSemesterObj.endDate) : null;
  const daysLeft = endDate ? Math.max(0, Math.ceil((endDate.getTime() - Date.now()) / 86_400_000)) : null;

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_calendar", { defaultValue: "Academic Calendar" })}
      icon={Calendar}
      isEmpty={!endDate}
      emptyIcon={Calendar}
      emptyText={t("portal_dashboard.no_milestones", { defaultValue: "No upcoming milestones" })}
    >
      <div className={styles.milestone}>
        <span className={styles.milestoneIcon}><Hourglass size={18} /></span>
        <div className={styles.milestoneBody}>
          <strong>{t("portal_dashboard.semester_ends", { defaultValue: "Semester ends" })}</strong>
          <span>{endDate?.toLocaleDateString(i18n.language === "ar" ? "ar-EG" : "en-US", { month: "short", day: "numeric", year: "numeric" })}</span>
        </div>
        {daysLeft != null && (
          <span className={styles.milestoneCount}>
            {daysLeft}
            <small>{t("portal_dashboard.days", { defaultValue: "days" })}</small>
          </span>
        )}
      </div>
    </WidgetShell>
  );
});

export const WIDGET_REGISTRY = {
  schedule: { component: UpcomingScheduleWidget, span: 2 },
  courses: { component: ActiveCoursesWidget, span: 2 },
  grades: { component: RecentGradesWidget, span: 1 },
  requests: { component: PendingRequestsWidget, span: 1 },
  fees: { component: FeeStatusWidget, span: 2 },
  notifications: { component: RecentNotificationsWidget, span: 2 },
  quickactions: { component: QuickActionsWidget, span: 4 },
  calendar: { component: AcademicCalendarWidget, span: 1 },
};
