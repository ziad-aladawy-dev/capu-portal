import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Calendar, CalendarDays, List, Clock, MapPin, Download, AlertCircle, User,
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

const DAY_KEYS = { 0: "sunday", 1: "monday", 2: "tuesday", 3: "wednesday", 4: "thursday", 5: "friday", 6: "saturday" };
const DAY_SHORT = { 0: "Sun", 1: "Mon", 2: "Tue", 3: "Wed", 4: "Thu", 5: "Fri", 6: "Sat" };
const BASE_GRID_DAYS = [0, 1, 2, 3, 4];
const START_HOUR = SCHEDULE_START_HOUR;
const END_HOUR = SCHEDULE_END_HOUR;
const HOUR_PX = 60;

const toMinutes = (time) => {
  const [h, m] = time.split(":").map(Number);
  return h * 60 + (m || 0);
};

function slotTop(start) {
  return ((toMinutes(start) - START_HOUR * 60) / 60) * HOUR_PX + 1;
}

function slotHeight(start, end) {
  return Math.max(24, ((toMinutes(end) - toMinutes(start)) / 60) * HOUR_PX - 2);
}

function useNow() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(id);
  }, []);
  const dayNum = now.getDay();
  const minutes = now.getHours() * 60 + now.getMinutes();
  return { dayNum, minutes };
}

function groupByCourse(rows) {
  const map = new Map();
  for (const s of rows) {
    const key = s.code;
    if (!map.has(key)) map.set(key, { code: s.code, title: s.title, color: s.color, slots: [] });
    map.get(key).slots.push(s);
  }
  return [...map.values()].sort((a, b) => a.code.localeCompare(b.code));
}

function SlotBlock({ s, compact, closedLabel }) {
  return (
    <div
      className={`${styles.slot} ${s.isClosed ? styles.slotClosed : ""}`}
      style={{ top: slotTop(s.start), height: slotHeight(s.start, s.end), background: s.color }}
      title={`${s.code}${s.sectionCode ? ` (${s.sectionCode})` : ""} · ${s.title} · ${s.start}–${s.end}${s.room ? ` · ${s.room}` : ""}${s.instructorName ? ` · ${s.instructorName}` : ""}`}
    >
      <div className={styles.slotHeader}>
        <strong>{s.code}</strong>
        {s.sectionCode && <span className={styles.slotSection}>{s.sectionCode}</span>}
      </div>
      {!compact && (
        <span className={styles.slotTitle} title={s.title}>{s.title}</span>
      )}
      <span className={styles.slotMeta}>
        <Clock size={9} /> {s.start}–{s.end}
        {s.room && <> · <MapPin size={9} /> {s.room}</>}
      </span>
      {!compact && s.instructorName && (
        <span className={styles.slotInstructor}>
          <User size={9} /> {s.instructorName}
        </span>
      )}
      {s.isClosed && <span className={styles.slotClosedTag}>{closedLabel}</span>}
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
            <span className={styles.dayHeadName}>{t(`portal_schedule.${DAY_KEYS[d]}`, { defaultValue: DAY_KEYS[d] })}</span>
            <span className={styles.dayHeadShort}>{DAY_SHORT[d]}</span>
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
              <SlotBlock
                key={s.id ?? i}
                s={s}
                compact={days.length > 1}
                closedLabel={t("portal_schedule.closed", { defaultValue: "Closed" })}
              />
            ))}
          </div>
        ))}
      </div>
    </PortalCard>
  );
}

function ListView({ rows, t }) {
  const groups = useMemo(() => groupByCourse(rows), [rows]);

  return (
    <div className={styles.list}>
      {groups.map((course) => (
        <PortalCard key={course.code} className={styles.listCourseGroup}>
          <div className={styles.listCourseHeader}>
            <span className={styles.listCourseDot} style={{ background: course.color }} />
            <div>
              <h3 className={styles.listCourseTitle}>{course.title}</h3>
              <span className={styles.listCourseCode}>
                {course.code} · {t("portal_schedule.slots_count", { defaultValue: "{{count}} slot(s)", count: course.slots.length })}
              </span>
            </div>
          </div>
          <ul className={styles.listItems}>
            {course.slots
              .sort((a, b) => a.dayNum - b.dayNum || a.start.localeCompare(b.start))
              .map((s, i) => (
              <li key={s.id ?? i} className={styles.listItem}>
                <span className={styles.listDay}>{t(`portal_schedule.${DAY_KEYS[s.dayNum]}`, { defaultValue: DAY_KEYS[s.dayNum] }).slice(0, 3)}</span>
                <span className={styles.listTime}><Clock size={12} /> {s.start}–{s.end}</span>
                {s.sectionCode && <PortalBadge tone="neutral">{s.sectionCode}</PortalBadge>}
                {s.instructorName && (
                  <span className={styles.listInstructor}><User size={11} /> {s.instructorName}</span>
                )}
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

  // Titles / instructor names arrive as bilingual {"ar","en"} JSON strings —
  // resolve once per active language (plain strings pass through unchanged).
  const rows = useMemo(
    () => (schedule.data || []).map((s) => ({
      ...s,
      title: getLocalized(s.title, i18n.language),
      instructorName: getLocalized(s.instructorName, i18n.language),
    })),
    [schedule.data, i18n.language]
  );
  const weekDays = useMemo(() => {
    const days = new Set(BASE_GRID_DAYS);
    for (const s of rows) days.add(s.dayNum);
    return [...days].sort((a, b) => a - b);
  }, [rows]);

  const courseGroups = useMemo(() => groupByCourse(rows), [rows]);

  const semesterLabel = selectedSemesterObj
    ? `${getLocalized(selectedSemesterObj.name, i18n.language)} · ${formatDate(selectedSemesterObj.startDate, i18n.language)} – ${formatDate(selectedSemesterObj.endDate, i18n.language)}`
    : null;

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
          days={view === "day" ? [now.dayNum] : weekDays}
          rows={view === "day" ? rows.filter((s) => s.dayNum === now.dayNum) : rows}
          t={t}
          now={now}
        />
      )}

      {view !== "list" && courseGroups.length > 0 && (
        <div className={styles.legend}>
          {courseGroups.map((course) => (
            <div key={course.code} className={styles.legendGroup}>
              <span className={styles.legendDot} style={{ background: course.color }} />
              <span className={styles.legendCode}>{course.code}</span>
              <span className={styles.legendTitle}>{course.title}</span>
              {course.slots[0]?.instructorName && (
                <span className={styles.legendInstructor}>
                  <User size={10} /> {course.slots[0].instructorName}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </PortalPageShell>
  );
}

export default StudentSchedule;
