import { memo, useMemo } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  BookOpen, Calendar, CalendarClock, Bell, GraduationCap, CreditCard,
  ClipboardList, MapPin, Clock, TrendingUp, CheckCircle2,
} from "lucide-react";
import { useAuth } from "../../../../core/auth/useAuth";
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
  2: "warning",
  3: "info",
  4: "danger",
  7: "accent",
  10: "success",
};

const GPA_BAR_MAX = 4;

/* Upcoming Schedule (span 2) */
export const UpcomingScheduleWidget = memo(function UpcomingScheduleWidget() {
  const { t, i18n } = useTranslation();
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
              {getLocalized(s.courseTitle, i18n.language) || s.courseCode || s.title || t("portal_dashboard.class", { defaultValue: "Class" })}
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
  const { t, i18n } = useTranslation();
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
            <span className={styles.courseTitle}>{getLocalized(c.title, i18n.language)}</span>
            <PortalBadge tone="primary">{c.creditHours} {t("portal_dashboard.cr", { defaultValue: "cr" })}</PortalBadge>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
});

/* Grade Trend (span 1) — replaces RecentGrades */
export const GradeTrendWidget = memo(function GradeTrendWidget() {
  const { t } = useTranslation();
  const { data, isLoading } = useGradesSummary();
  const gpa = data?.cgpa ?? data?.gpa ?? null;
  const standing = data?.academicStanding ?? data?.standing ?? null;
  const earnedCredits = data?.earnedCredits ?? data?.totalCredits ?? data?.completedCredits ?? 0;

  const pct = gpa != null ? Math.min(100, (gpa / GPA_BAR_MAX) * 100) : 0;
  const tone = gpa == null ? "primary" : gpa >= 3 ? "success" : gpa >= 2 ? "warning" : "danger";

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_grades", { defaultValue: "Grade Trend" })}
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
        {earnedCredits > 0 && (
          <span className={styles.gradeCredits}>
            {earnedCredits} {t("portal_dashboard.credits_earned", { defaultValue: "credits earned" })}
          </span>
        )}
      </div>
      <div className={styles.gpaBarTrack}>
        <div className={styles.gpaBarFill} style={{ width: `${pct}%` }} />
        <div className={styles.gpaBarDot} style={{ left: `${pct}%` }} />
      </div>
      <div className={styles.gpaBarLabels}>
        <span>0</span>
        <span>{GPA_BAR_MAX}</span>
      </div>
    </WidgetShell>
  );
});

/* Pending Requests (span 1) — improved */
export const PendingRequestsWidget = memo(function PendingRequestsWidget() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useOpenRequests();
  const open = useMemo(() => data ?? [], [data]);
  const count = open.length;

  return (
    <WidgetShell
      title={t("portal_dashboard.widget_requests", { defaultValue: "Pending Requests" })}
      icon={ClipboardList}
      to="/student/requests"
      isLoading={isLoading}
      isError={isError}
      onRetry={refetch}
      isEmpty={count === 0}
      emptyIcon={CheckCircle2}
      emptyText={t("portal_dashboard.no_open_requests", { defaultValue: "No requests in progress" })}
    >
      <div className={styles.requestHeader}>
        <span className={styles.requestCount}>{count}</span>
        <span className={styles.requestCountLabel}>
          {t("portal_dashboard.open_requests", { defaultValue: "open" })}
        </span>
      </div>
      {count > 0 && (
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
      )}
    </WidgetShell>
  );
});

/* Fee Status (span 2) — improved with breakdown bar */
export const FeeStatusWidget = memo(function FeeStatusWidget() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data, isLoading, isError, refetch } = useFinancialSnapshot(user?.id);

  const total = data?.total ?? 0;
  const paid = data?.paid ?? 0;
  const outstanding = data?.outstanding ?? 0;
  const pct = total > 0 ? Math.min(100, Math.round((paid / total) * 100)) : 0;
  const outstandingPct = total > 0 ? Math.round((outstanding / total) * 100) : 0;

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
        <div>
          <span className={styles.feeLabel}>{t("portal_dashboard.paid", { defaultValue: "Paid" })}</span>
          <strong className={styles.feeOk}>{egp(paid, t)}</strong>
        </div>
        <div className={styles.feeEnd}>
          <span className={styles.feeLabel}>{t("portal_dashboard.total", { defaultValue: "Total" })}</span>
          <strong>{egp(total, t)}</strong>
        </div>
      </div>
      <div className={styles.feeBreakdownBar}>
        <div className={styles.feeBreakdownFill} style={{ width: `${pct}%` }} />
        {outstanding > 0 && (
          <div className={styles.feeBreakdownOutstanding} style={{ width: `${outstandingPct}%` }} />
        )}
      </div>
      <div className={styles.feeFooter}>
        <span className={styles.feeLabel}>
          {pct}% {t("portal_dashboard.collected", { defaultValue: "collected" })}
        </span>
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

export const WIDGET_REGISTRY = {
  schedule: { component: UpcomingScheduleWidget, span: 2 },
  courses: { component: ActiveCoursesWidget, span: 2 },
  grades: { component: GradeTrendWidget, span: 1 },
  requests: { component: PendingRequestsWidget, span: 1 },
  fees: { component: FeeStatusWidget, span: 2 },
  notifications: { component: RecentNotificationsWidget, span: 2 },
};
