import { useQuery } from "@tanstack/react-query";
import api from "../../../core/api/apiClient";

const SLOT_COLORS = [
  "#2563eb", "#8b5cf6", "#059669", "#d97706", "#db2777",
  "#0891b2", "#65a30d", "#dc2626", "#7c3aed", "#ca8a04",
];

function hashColor(code) {
  let hash = 0;
  for (let i = 0; i < code.length; i++) hash = code.charCodeAt(i) + ((hash << 5) - hash);
  return SLOT_COLORS[Math.abs(hash) % SLOT_COLORS.length];
}

const trimTime = (s) => String(s || "").slice(0, 5);

function resolveSemesterId(activeScope) {
  let semesterId = activeScope?.temporal?.semesterId;
  try {
    if (!semesterId) semesterId = JSON.parse(localStorage.getItem("capu_selected_semester"))?.id || null;
  } catch { /* ignore */ }
  return semesterId;
}

/**
 * Weekly timetable rows for the logged-in student's OWN enrolled courses via
 * the self-scoped GET /student/schedule aggregate (students hold no
 * course-offerings/schedule-slots catalog grants, so the old offerings
 * fan-out 403'd for every student).
 *
 * Day convention follows the backend's System.DayOfWeek: 0=Sun .. 6=Sat.
 * Rows: { id, dayNum (0..6), start, end, code, title, room, color,
 * sectionCode, isClosed }.
 */
export function useStudentSchedule(activeScope) {
  const semesterId = resolveSemesterId(activeScope);

  return useQuery({
    queryKey: ["portal", "schedule", semesterId ?? "all"],
    staleTime: 60_000,
    queryFn: async () => {
      const { data } = await api.get("/student/schedule", {
        params: semesterId ? { semesterId } : undefined,
      });
      const slots = Array.isArray(data) ? data : [];
      return slots
        .filter((s) => Number.isInteger(s.dayOfWeek) && s.dayOfWeek >= 0 && s.dayOfWeek <= 6)
        .map((s) => ({
          id: s.id,
          offeringId: s.courseOfferingId,
          dayNum: s.dayOfWeek,
          start: trimTime(s.startTime),
          end: trimTime(s.endTime),
          code: s.courseCode || "?",
          title: s.courseTitle || "",
          room: s.location || "",
          instructorName: s.instructorName || "",
          color: hashColor(s.courseCode || "?"),
          sectionCode: s.sectionCode,
          isClosed: s.isClosed,
        }));
    },
  });
}

/* ── .ics export ──────────────────────────────────────────
   One weekly-recurring VEVENT per slot, until the semester end. */

const ICS_DAY = { 0: "SU", 1: "MO", 2: "TU", 3: "WE", 4: "TH", 5: "FR", 6: "SA" };

function nextDateForWeekday(dayNum) {
  // dayNum follows JS getDay / System.DayOfWeek: 0=Sun..6=Sat.
  const d = new Date();
  const delta = (dayNum - d.getDay() + 7) % 7;
  d.setDate(d.getDate() + delta);
  return d;
}

function icsDate(d, time) {
  const [h, m] = time.split(":").map(Number);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}T${pad(h)}${pad(m)}00`;
}

export function buildScheduleIcs(rows, semesterEndDate) {
  const until = semesterEndDate
    ? new Date(semesterEndDate).toISOString().replace(/[-:]/g, "").slice(0, 15) + "Z"
    : null;

  const events = rows.map((s, i) => {
    const day = nextDateForWeekday(s.dayNum);
    return [
      "BEGIN:VEVENT",
      `UID:capu-${s.id || i}@capital-university`,
      `DTSTART:${icsDate(day, s.start)}`,
      `DTEND:${icsDate(day, s.end)}`,
      `RRULE:FREQ=WEEKLY;BYDAY=${ICS_DAY[s.dayNum]}${until ? `;UNTIL=${until}` : ""}`,
      `SUMMARY:${s.code} — ${s.title}`,
      s.room ? `LOCATION:${s.room}` : null,
      "END:VEVENT",
    ].filter(Boolean).join("\r\n");
  });

  return [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Capital University//Student Portal//EN",
    ...events,
    "END:VCALENDAR",
  ].join("\r\n");
}

export function downloadIcs(rows, semesterEndDate) {
  const blob = new Blob([buildScheduleIcs(rows, semesterEndDate)], { type: "text/calendar" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "my-schedule.ics";
  a.click();
  URL.revokeObjectURL(url);
}
