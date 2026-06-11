import { useQuery } from "@tanstack/react-query";
import api from "../../../core/api/apiClient";
import * as scheduleService from "../../../core/services/scheduleService";
import * as courseService from "../../../core/services/courseService";

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

function resolveScope(activeScope) {
  let nodeId = activeScope?.structural?.nodeId;
  let semesterId = activeScope?.temporal?.semesterId;
  try {
    if (!nodeId) nodeId = JSON.parse(localStorage.getItem("capu_selected_scope_node"))?.id || null;
    if (!semesterId) semesterId = JSON.parse(localStorage.getItem("capu_selected_semester"))?.id || null;
  } catch { /* ignore */ }
  return { nodeId, semesterId };
}

/**
 * Weekly timetable entries for the student's scope: offerings → slots →
 * course titles, flattened to render-ready rows
 * { dayNum (1=Mon..7=Sun), start, end, code, title, room, color, sectionCode }.
 */
export function useStudentSchedule(activeScope) {
  const { nodeId, semesterId } = resolveScope(activeScope);

  return useQuery({
    queryKey: ["portal", "schedule", nodeId, semesterId],
    enabled: Boolean(nodeId && semesterId),
    staleTime: 60_000,
    queryFn: async () => {
      const offeringsResp = await api.get(`/course-offerings/node/${nodeId}/semester/${semesterId}`);
      const offerings = Array.isArray(offeringsResp.data) ? offeringsResp.data : [];
      if (offerings.length === 0) return [];

      const slotsResults = await Promise.allSettled(
        offerings.map((o) =>
          scheduleService.fetchSlotsForOffering(o.id).then((slots) => ({
            offering: o,
            slots: Array.isArray(slots) ? slots : [],
          }))
        )
      );

      const courseIds = [...new Set(offerings.map((o) => o.courseId))];
      const courseResults = await Promise.allSettled(courseIds.map((id) => courseService.fetchCourse(id)));
      const courseMap = {};
      courseIds.forEach((id, i) => {
        if (courseResults[i].status === "fulfilled" && courseResults[i].value) courseMap[id] = courseResults[i].value;
      });

      const rows = [];
      for (const r of slotsResults) {
        if (r.status !== "fulfilled") continue;
        const { offering, slots } = r.value;
        const course = courseMap[offering.courseId];
        if (!course) continue;
        for (const slot of slots) {
          const dayNum = Number(slot.dayOfWeek ?? slot.day);
          if (!dayNum || dayNum < 1 || dayNum > 7) continue;
          rows.push({
            id: slot.id,
            dayNum,
            start: trimTime(slot.startTime),
            end: trimTime(slot.endTime),
            code: course.code || "?",
            title: course.title || "",
            room: slot.location || "",
            color: hashColor(course.code || "?"),
            sectionCode: offering.sectionCode,
            isClosed: slot.isClosed,
          });
        }
      }
      rows.sort((a, b) => a.dayNum - b.dayNum || a.start.localeCompare(b.start));
      return rows;
    },
  });
}

/* ── .ics export ──────────────────────────────────────────
   One weekly-recurring VEVENT per slot, until the semester end. */

const ICS_DAY = { 1: "MO", 2: "TU", 3: "WE", 4: "TH", 5: "FR", 6: "SA", 7: "SU" };

function nextDateForWeekday(dayNum) {
  // dayNum: 1=Mon..7=Sun ; JS getDay: 0=Sun..6=Sat
  const target = dayNum % 7; // 7→0 (Sun)
  const d = new Date();
  const delta = (target - d.getDay() + 7) % 7;
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
