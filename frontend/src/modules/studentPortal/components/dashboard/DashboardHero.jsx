import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { motion } from "framer-motion";
import { CalendarDays, CreditCard, ClipboardList, Bell } from "lucide-react";
import { useAuth } from "../../../../core/auth/useAuth";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { getLocalized } from "../../../../core/utils/getLocalized";
import PortalProgressRing from "../shared/PortalProgressRing";
import PortalBadge from "../shared/PortalBadge";
import {
  useAggregatedDashboard, useGradesSummary, useFinancialSnapshot,
  useUnreadNotifications, useOpenRequests,
} from "../../hooks/useDashboardData";
import styles from "./DashboardHero.module.css";

const egp = (n) => `${Number(n || 0).toLocaleString(undefined, { maximumFractionDigits: 0 })} EGP`;

function greetingKey(hour) {
  if (hour < 12) return ["good_morning", "Good morning", "☀️"];
  if (hour < 18) return ["good_afternoon", "Good afternoon", "🌤️"];
  return ["good_evening", "Good evening", "🌙"];
}

function daysBetween(to) {
  const diff = new Date(to).getTime() - Date.now();
  return Math.max(0, Math.ceil(diff / 86_400_000));
}

function DashboardHero() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();

  // Single aggregated call; the legacy per-widget queries only run when the
  // API predates GET /student/dashboard (aggregate errored).
  const agg = useAggregatedDashboard();
  const useLegacy = agg.isError;
  const grades = useGradesSummary({ enabled: useLegacy });
  const fin = useFinancialSnapshot(useLegacy ? user?.id : undefined);
  const notif = useUnreadNotifications({ enabled: useLegacy });
  const requests = useOpenRequests();

  const firstName = (getLocalized(user?.name, i18n.language) || t("student", { defaultValue: "Student" })).split(" ")[0];
  const [gKey, gDefault, emoji] = greetingKey(new Date().getHours());

  const semesterName = selectedSemesterObj ? getLocalized(selectedSemesterObj.name, i18n.language) : null;
  const yearName = selectedYearObj ? getLocalized(selectedYearObj.name, i18n.language) : null;
  const daysLeft = selectedSemesterObj?.endDate ? daysBetween(selectedSemesterObj.endDate) : null;

  const summary = agg.data?.academicSummary ?? grades.data;
  const gpa = summary?.cgpa ?? summary?.gpa ?? null;
  const gpaTone = gpa == null ? "primary" : gpa >= 3 ? "success" : gpa >= 2 ? "warning" : "danger";

  const outstanding = agg.data?.outstandingFeeTotal ?? fin.data?.outstanding ?? 0;
  const unreadCount = agg.data?.unreadNotificationCount ?? notif.data?.length ?? 0;

  const alerts = [];
  if (outstanding > 0) {
    alerts.push({
      key: "fees", icon: CreditCard, tone: styles.alertDanger, to: "/student/payments",
      text: t("portal_dashboard.alert_unpaid_fees", { defaultValue: "Unpaid fees" }),
      value: egp(outstanding),
    });
  }
  if ((requests.data?.length ?? 0) > 0) {
    alerts.push({
      key: "requests", icon: ClipboardList, tone: styles.alertWarning, to: "/student/requests",
      text: t("portal_dashboard.alert_open_requests", { defaultValue: "Requests in progress" }),
      value: requests.data.length,
    });
  }
  if (unreadCount > 0) {
    alerts.push({
      key: "notif", icon: Bell, tone: styles.alertInfo, to: "/student/notifications",
      text: t("portal_dashboard.alert_unread", { defaultValue: "Unread notifications" }),
      value: unreadCount,
    });
  }

  return (
    <section className={styles.hero}>
      <div className={styles.greetingBlock}>
        <h1 className={styles.greeting}>
          {t(`portal_dashboard.${gKey}`, { defaultValue: gDefault })}, {firstName}{" "}
          <motion.span
            className={styles.wave}
            animate={{ rotate: [0, 16, -8, 16, 0] }}
            transition={{ duration: 1.4, delay: 0.4, ease: "easeInOut" }}
          >
            {emoji}
          </motion.span>
        </h1>
        <p className={styles.subtitle}>
          {t("portal_dashboard.hero_subtitle", { defaultValue: "Here's what's happening with your studies" })}
        </p>

        {(semesterName || yearName) && (
          <div className={styles.semesterCard}>
            <CalendarDays size={15} />
            <span className={styles.semesterName}>
              {yearName}{semesterName ? ` · ${semesterName}` : ""}
            </span>
            {daysLeft != null && (
              <PortalBadge tone={daysLeft <= 14 ? "warning" : "primary"}>
                {t("portal_dashboard.days_left", { defaultValue: "{{count}} days left", count: daysLeft })}
              </PortalBadge>
            )}
          </div>
        )}
      </div>

      <div className={styles.gpaBlock}>
        <PortalProgressRing
          value={gpa ?? 0}
          max={4}
          size={92}
          tone={gpaTone}
          label={t("portal_dashboard.gpa", { defaultValue: "GPA" })}
        >
          <span className={styles.gpaValue}>{gpa != null ? Number(gpa).toFixed(2) : "—"}</span>
          <span className={styles.gpaLabel}>{t("portal_dashboard.gpa", { defaultValue: "GPA" })}</span>
        </PortalProgressRing>
      </div>

      {alerts.length > 0 && (
        <div className={styles.alerts}>
          {alerts.map((a) => (
            <Link key={a.key} to={a.to} className={`${styles.alert} ${a.tone}`}>
              <a.icon size={14} />
              <span>{a.text}</span>
              <strong>{a.value}</strong>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

export default DashboardHero;
