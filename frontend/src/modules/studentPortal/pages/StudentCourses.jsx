import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  BookOpen, History, CheckCircle2, Clock, XCircle, Calendar, GraduationCap, AlertCircle,
} from "lucide-react";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalBadge from "../components/shared/PortalBadge";
import PortalStatCard from "../components/shared/PortalStatCard";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { useRegisteredCourses, useRegistrationHistory } from "../hooks/useAcademics";
import { REGISTRATION_STATUS, getRegistrationStatusLabel } from "../../../core/services/registrationService";
import styles from "./StudentCourses.module.css";

const STATUS_TONE = {
  [REGISTRATION_STATUS.Enrolled]: { tone: "primary", Icon: Clock },
  [REGISTRATION_STATUS.Completed]: { tone: "success", Icon: CheckCircle2 },
  [REGISTRATION_STATUS.Withdrawn]: { tone: "danger", Icon: XCircle },
};

function CourseCard({ c, t }) {
  const { tone, Icon } = STATUS_TONE[c.status] || { tone: "neutral", Icon: Clock };
  return (
    <PortalCard interactive>
      <div className={styles.cardHead}>
        <h3 className={styles.cardTitle}>{c.courseTitle}</h3>
        <PortalBadge tone="primary">{c.courseCode}</PortalBadge>
      </div>
      <div className={styles.cardMeta}>
        <PortalBadge tone="accent">{c.creditHours} {t("portal_courses.cr", { defaultValue: "cr" })}</PortalBadge>
        {c.semesterName && <PortalBadge tone="neutral">{c.semesterName}</PortalBadge>}
        {c.attemptNumber > 1 && (
          <PortalBadge tone="warning">
            {t("portal_courses.attempt", { defaultValue: "Attempt #{{n}}", n: c.attemptNumber })}
          </PortalBadge>
        )}
        <PortalBadge tone={tone} icon={Icon}>{getRegistrationStatusLabel(c.status)}</PortalBadge>
      </div>
      <div className={styles.cardActions}>
        <Link to="/student/schedule" className={styles.cardAction}>
          <Calendar size={12} /> {t("portal_courses.view_schedule", { defaultValue: "Schedule" })}
        </Link>
        <Link to="/student/grades" className={styles.cardAction}>
          <GraduationCap size={12} /> {t("portal_courses.view_grades", { defaultValue: "Grades" })}
        </Link>
      </div>
    </PortalCard>
  );
}

function CardsLoading() {
  return (
    <div className={styles.grid}>
      {Array.from({ length: 6 }).map((_, i) => <PortalSkeleton key={i} variant="block" height={140} />)}
    </div>
  );
}

function StudentCourses() {
  const { t } = useTranslation();
  const [tab, setTab] = useState("current");
  const current = useRegisteredCourses();
  const history = useRegistrationHistory();

  const registered = current.data || [];
  const totalCredits = registered.reduce((s, c) => s + (c.creditHours || 0), 0);
  const completed = registered.filter((c) => c.status === REGISTRATION_STATUS.Completed).length;

  // History grouped by semester, newest first.
  const historyBySemester = useMemo(() => {
    const rows = history.data || [];
    const groups = new Map();
    for (const c of rows) {
      const key = c.semesterName || "—";
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(c);
    }
    return [...groups.entries()];
  }, [history.data]);

  const active = tab === "current" ? current : history;

  return (
    <PortalPageShell
      title={t("portal_courses.title", { defaultValue: "My Courses" })}
      subtitle={t("portal_courses.subtitle", { defaultValue: "Your registered courses, synced from academic records" })}
    >
      <div className={styles.tabs}>
        <button
          type="button"
          className={`${styles.tab} ${tab === "current" ? styles.tabActive : ""}`}
          onClick={() => setTab("current")}
        >
          <BookOpen size={14} /> {t("portal_courses.current", { defaultValue: "Current" })}
        </button>
        <button
          type="button"
          className={`${styles.tab} ${tab === "history" ? styles.tabActive : ""}`}
          onClick={() => setTab("history")}
        >
          <History size={14} /> {t("portal_courses.all_semesters", { defaultValue: "All Semesters" })}
        </button>
      </div>

      {active.isLoading ? (
        <CardsLoading />
      ) : active.isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_courses.load_failed", { defaultValue: "Unable to load your courses" })}
          onAction={() => active.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : tab === "current" ? (
        registered.length === 0 ? (
          <PortalEmptyState
            icon={BookOpen}
            title={t("portal_courses.none", { defaultValue: "No registered courses" })}
            text={t("portal_courses.none_hint", { defaultValue: "You have no active course registrations this term." })}
          />
        ) : (
          <>
            <div className={styles.statsRow}>
              <PortalStatCard icon={BookOpen} value={registered.length} label={t("portal_courses.registered", { defaultValue: "Registered" })} variant="row" />
              <PortalStatCard icon={GraduationCap} value={totalCredits} label={t("portal_courses.credits", { defaultValue: "Credits" })} tone="accent" variant="row" />
              <PortalStatCard icon={CheckCircle2} value={completed} label={t("portal_courses.completed", { defaultValue: "Completed" })} tone="success" variant="row" />
            </div>
            <div className={styles.grid}>
              {registered.map((c) => <CourseCard key={c.id} c={c} t={t} />)}
            </div>
          </>
        )
      ) : historyBySemester.length === 0 ? (
        <PortalEmptyState
          icon={History}
          title={t("portal_courses.no_history", { defaultValue: "No course history yet" })}
        />
      ) : (
        historyBySemester.map(([sem, courses]) => (
          <div key={sem} className={styles.semGroup}>
            <h2 className={styles.semTitle}>{sem}</h2>
            <div className={styles.grid}>
              {courses.map((c) => <CourseCard key={c.id} c={c} t={t} />)}
            </div>
          </div>
        ))
      )}
    </PortalPageShell>
  );
}

export default StudentCourses;
