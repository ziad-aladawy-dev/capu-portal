import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Calendar, CalendarDays, List, Clock, MapPin, Download, AlertCircle,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { getLocalized } from "../../../core/utils/getLocalized";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalBadge from "../components/shared/PortalBadge";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { useStudentSchedule, downloadIcs } from "../hooks/useStudentSchedule";
import { SCHEDULE_START_HOUR, SCHEDULE_END_HOUR } from "../../../core/constants/scheduleConfig";
import { formatDate } from "../utils/format";
import styles from "./StudentSchedule.module.css";

const DAY_KEYS = { 1: "monday", 2: "tuesday", 3: "wednesday", 4: "thursday", 5: "friday", 6: "saturday", 7: "sunday" };
const GRID_DAYS = [1, 2, 3, 4, 5]; // Mon–Fri teaching week
// Shared campus window — admin schedules 7:00–22:00, so the student view must
// span the same range or early/late slots silently disappear.
const START_HOUR = SCHEDULE_START_HOUR;
const END_HOUR = SCHEDULE_END_HOUR;
const HOUR_PX = 64;

const toMinutes = (time) => {
  const [h, m] = time.split(":").map(Number);
  return h * 60 + (m || 0);
};

function slotTop(start) {
  return ((toMinutes(start) - START_HOUR * 60) / 60) * HOUR_PX;
}

function slotHeight(start, end) {
  return Math.max(26, ((toMinutes(end) - toMinutes(start)) / 60) * HOUR_PX - 3);
}

/** Red "now" line; jsDay 0=Sun..6 → our dayNum 1=Mon..7. */
function useNow() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(id);
  }, []);
  const dayNum = now.getDay() === 0 ? 7 : now.getDay();
  const minutes = now.getHours() * 60 + now.getMinutes();
  return { dayNum, minutes };
}

function SlotBlock({ s, t, compact = false }) {
  return (
    <div
      className={`${styles.slot} ${s.isClosed ? styles.slotClosed : ""}`}
      style={{ top: slotTop(s.start), height: slotHeight(s.start, s.end), background: s.color }}
      title={`${s.code} · ${s.title} · ${s.start}–${s.end}${s.room ? ` · ${s.room}` : ""}`}
    >
      <strong>{s.code}</strong>
      {!compact && <span className={styles.slotTitle}>{s.title}</span>}
      <span className={styles.slotMeta}>
        {s.start}–{s.end}{s.room ? ` · ${s.room}` : ""}
      </span>
      {s.isClosed && <span className={styles.slotMeta}>{t("portal_schedule.closed", { defaultValue: "Closed" })}</span>}
    </div>
  );
}

function TimeGrid({ days, rows, t, now }) {
  const hours = [];
  for (let h = START_HOUR; h <= END_HOUR; h++) hours.push(h);
  const gridHeight = (END_HOUR - START_HOUR) * HOUR_PX;
  const nowTop = ((now.minutes - START_HOUR * 60) / 60) * HOUR_PX;
  const nowVisible = now.minutes >= START_HOUR * 60 && now.minutes <= END_HOUR * 60;

  return (
    <PortalCard padding="none" className={styles.gridCard}>
      <div className={styles.gridHead} style={{ gridTemplateColumns: `52px repeat(${days.length}, 1fr)` }}>
        <div />
        {days.map((d) => (
          <div key={d} className={`${styles.dayHead} ${d === now.dayNum ? styles.dayHeadToday : ""}`}>
            {t(`portal_schedule.${DAY_KEYS[d]}`, { defaultValue: DAY_KEYS[d] })}
          </div>
        ))}
      </div>
      <div className={styles.gridBody} style={{ gridTemplateColumns: `52px repeat(${days.length}, 1fr)`, height: gridHeight }}>
        <div className={styles.timeCol}>
          {hours.map((h) => (
            <span key={h} className={styles.timeLabel} style={{ top: (h - START_HOUR) * HOUR_PX }}>
              {String(h).padStart(2, "0")}:00
            </span>
          ))}
        </div>
        {days.map((d) => (
          <div key={d} className={styles.dayCol}>
            {hours.map((h) => (
              <div key={h} className={styles.hourLine} style={{ top: (h - START_HOUR) * HOUR_PX }} />
            ))}
            {d === now.dayNum && nowVisible && (
              <div className={styles.nowLine} style={{ top: nowTop }}>
                <span className={styles.nowDot} />
              </div>
            )}
            {rows.filter((s) => s.dayNum === d).map((s, i) => (
              <SlotBlock key={s.id ?? i} s={s} t={t} compact={days.length > 1} />
            ))}
          </div>
        ))}
      </div>
    </PortalCard>
  );
}

function ListView({ rows, t }) {
  const byDay = useMemo(() => {
    const m = new Map();
    for (const s of rows) {
      if (!m.has(s.dayNum)) m.set(s.dayNum, []);
      m.get(s.dayNum).push(s);
    }
    return [...m.entries()].sort((a, b) => a[0] - b[0]);
  }, [rows]);

  return (
    <div className={styles.list}>
      {byDay.map(([d, slots]) => (
        <PortalCard key={d}>
          <h3 className={styles.listDay}>{t(`portal_schedule.${DAY_KEYS[d]}`, { defaultValue: DAY_KEYS[d] })}</h3>
          <ul className={styles.listItems}>
            {slots.map((s, i) => (
              <li key={s.id ?? i} className={styles.listItem}>
                <span className={styles.listDot} style={{ background: s.color }} />
                <span className={styles.listTime}><Clock size={12} /> {s.start}–{s.end}</span>
                <span className={styles.listCourse}>{s.code} · {s.title}</span>
                {s.room && <span className={styles.listRoom}><MapPin size={11} /> {s.room}</span>}
              </li>
            ))}
          </ul>
        </PortalCard>
      ))}
    </div>
  );
}

function StudentSchedule() {
  const { t, i18n } = useTranslation();
  const { activeScope } = useAuth();
  const { selectedSemesterObj } = useAcademic();
  const schedule = useStudentSchedule(activeScope);
  const now = useNow();
  const [view, setView] = useState(() => (window.innerWidth < 700 ? "list" : "week"));

  const rows = schedule.data || [];
  const semesterLabel = selectedSemesterObj
    ? `${getLocalized(selectedSemesterObj.name, i18n.language)} · ${formatDate(selectedSemesterObj.startDate, i18n.language)} – ${formatDate(selectedSemesterObj.endDate, i18n.language)}`
    : null;

  const scopeMissing = !schedule.isLoading && !schedule.isError && schedule.fetchStatus === "idle" && !schedule.data;

  return (
    <PortalPageShell
      title={t("portal_schedule.title", { defaultValue: "My Schedule" })}
      subtitle={semesterLabel || t("portal_schedule.subtitle", { defaultValue: "Your weekly class timetable" })}
      actions={
        rows.length > 0 ? (
          <button
            type="button"
            className={styles.icsBtn}
            onClick={() => downloadIcs(rows, selectedSemesterObj?.endDate)}
          >
            <Download size={14} /> {t("portal_schedule.add_to_calendar", { defaultValue: "Add to calendar" })}
          </button>
        ) : undefined
      }
    >
      <div className={styles.viewTabs}>
        {[
          { key: "week", Icon: CalendarDays, label: t("portal_schedule.week", { defaultValue: "Week" }) },
          { key: "day", Icon: Calendar, label: t("portal_schedule.today", { defaultValue: "Today" }) },
          { key: "list", Icon: List, label: t("portal_schedule.list", { defaultValue: "List" }) },
        ].map(({ key, Icon, label }) => (
          <button
            key={key}
            type="button"
            className={`${styles.viewTab} ${view === key ? styles.viewTabActive : ""}`}
            onClick={() => setView(key)}
          >
            <Icon size={13} /> {label}
          </button>
        ))}
      </div>

      {schedule.isLoading ? (
        <PortalSkeleton variant="block" height={420} />
      ) : scopeMissing ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_schedule.no_scope", { defaultValue: "Academic scope not configured" })}
          text={t("portal_schedule.no_scope_hint", { defaultValue: "Your semester isn't set yet — contact the administration if this persists." })}
        />
      ) : schedule.isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_schedule.load_failed", { defaultValue: "Couldn't load your schedule" })}
          onAction={() => schedule.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : rows.length === 0 ? (
        <PortalEmptyState
          icon={Calendar}
          title={t("portal_schedule.empty", { defaultValue: "No scheduled classes" })}
          text={t("portal_schedule.empty_hint", { defaultValue: "Nothing is on your timetable for this semester yet." })}
        />
      ) : view === "list" ? (
        <ListView rows={rows} t={t} />
      ) : (
        <TimeGrid
          days={view === "day" ? [Math.min(Math.max(now.dayNum, 1), 7)] : GRID_DAYS}
          rows={view === "day" ? rows.filter((s) => s.dayNum === now.dayNum) : rows.filter((s) => GRID_DAYS.includes(s.dayNum))}
          t={t}
          now={now}
        />
      )}

      {rows.length > 0 && (
        <div className={styles.legend}>
          {[...new Map(rows.map((s) => [s.code, s])).values()].map((s) => (
            <PortalBadge key={s.code} tone="neutral">
              <span className={styles.legendDot} style={{ background: s.color }} /> {s.code}
            </PortalBadge>
          ))}
        </div>
      )}
    </PortalPageShell>
  );
}

export default StudentSchedule;
