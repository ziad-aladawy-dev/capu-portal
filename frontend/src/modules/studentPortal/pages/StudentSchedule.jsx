import { useState, useEffect } from "react";
import { ChevronLeft, ChevronRight, Clock, AlertCircle } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import * as scheduleService from "../../../core/services/scheduleService";
import * as courseService from "../../../core/services/courseService";
import api from "../../../core/api/apiClient";
import "../styles/studentSchedule.css";

const DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
const HOURS = ["8:00", "9:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00"];

const SLOT_COLORS = [
  "#1a1f5e", "#c9a84c", "#2563eb", "#059669", "#d97706",
  "#7c3aed", "#db2777", "#0891b2", "#65a30d", "#ca8a04",
];

function hashColor(code) {
  let hash = 0;
  for (let i = 0; i < code.length; i++) {
    hash = code.charCodeAt(i) + ((hash << 5) - hash);
  }
  return SLOT_COLORS[Math.abs(hash) % SLOT_COLORS.length];
}

function formatTime(timeStr) {
  const parts = timeStr.split(":");
  return `${parseInt(parts[0], 10)}:${parts[1] || "00"}`;
}

const DAY_MAP = { 1: "Monday", 2: "Tuesday", 3: "Wednesday", 4: "Thursday", 5: "Friday" };

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function StudentSchedule() {
  const { activeScope } = useAuth();
  const [scheduleData, setScheduleData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const now = new Date();
  const [currentMonth, setCurrentMonth] = useState(now.getMonth());
  const [currentYear, setCurrentYear] = useState(now.getFullYear());

  useEffect(() => {
    let cancelled = false;

    const fetchSchedule = async () => {
      setLoading(true);
      setError(null);

      let nodeId = activeScope?.structural?.nodeId;
      let semesterId = activeScope?.temporal?.semesterId;

      if (!nodeId || !semesterId) {
        try {
          const scopeNode = JSON.parse(localStorage.getItem("capu_selected_scope_node"));
          const semester = JSON.parse(localStorage.getItem("capu_selected_semester"));
          if (!nodeId && scopeNode?.id) nodeId = scopeNode.id;
          if (!semesterId && semester?.id) semesterId = semester.id;
        } catch { }
      }

      if (!nodeId || !semesterId) {
        if (!cancelled) {
          setError("Academic scope not configured");
          setLoading(false);
        }
        return;
      }

      try {
        const offeringsResp = await api.get(`/course-offerings/node/${nodeId}/semester/${semesterId}`);
        const offerings = Array.isArray(offeringsResp.data) ? offeringsResp.data : [];

        if (offerings.length === 0) {
          if (!cancelled) { setScheduleData([]); setLoading(false); }
          return;
        }

        const slotsResults = await Promise.allSettled(
          offerings.map(o =>
            scheduleService.fetchSlotsForOffering(o.id)
              .then(slots => ({ offeringId: o.id, slots: Array.isArray(slots) ? slots : [] }))
          )
        );

        const slotsByOffering = {};
        slotsResults.forEach(r => {
          if (r.status === "fulfilled") {
            slotsByOffering[r.value.offeringId] = r.value.slots;
          }
        });

        const courseIds = [...new Set(offerings.map(o => o.courseId))];
        const courseResults = await Promise.allSettled(
          courseIds.map(id => courseService.fetchCourse(id))
        );
        const courseMap = {};
        courseIds.forEach((id, i) => {
          if (courseResults[i].status === "fulfilled" && courseResults[i].value) {
            courseMap[id] = courseResults[i].value;
          }
        });

        const offeringMap = {};
        offerings.forEach(o => { offeringMap[o.id] = o; });

        const data = [];
        Object.entries(slotsByOffering).forEach(([offeringId, slots]) => {
          const offering = offeringMap[offeringId];
          if (!offering) return;
          const course = courseMap[offering.courseId];
          if (!course) return;

          slots.forEach(slot => {
            const dayNum = typeof slot.dayOfWeek === "number" ? slot.dayOfWeek : parseInt(slot.dayOfWeek, 10);
            const dayName = DAY_MAP[dayNum];
            if (!dayName) return;

            data.push({
              day: dayName,
              start: formatTime(slot.startTime),
              end: formatTime(slot.endTime),
              code: course.code || "?",
              title: course.title || "Unknown",
              room: slot.location || "TBD",
              color: hashColor(course.code || "?"),
              sectionCode: offering.sectionCode,
              isClosed: slot.isClosed,
            });
          });
        });

        if (!cancelled) {
          setScheduleData(data);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err.message || "Failed to load schedule");
          setLoading(false);
        }
      }
    };

    fetchSchedule();
    return () => { cancelled = true; };
  }, [activeScope]);

  const prevMonth = () => {
    if (currentMonth === 0) { setCurrentMonth(11); setCurrentYear(y => y - 1); }
    else setCurrentMonth(m => m - 1);
  };
  const nextMonth = () => {
    if (currentMonth === 11) { setCurrentMonth(0); setCurrentYear(y => y + 1); }
    else setCurrentMonth(m => m + 1);
  };

  const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate();
  const firstDay = new Date(currentYear, currentMonth, 1).getDay();
  const calendarDays = [];
  for (let i = 0; i < firstDay; i++) calendarDays.push(null);
  for (let i = 1; i <= daysInMonth; i++) calendarDays.push(i);

  const getSlotTop = (time) => {
    const [h, m] = time.split(":").map(Number);
    return ((h - 8) * 60 + m) / 60 * 80 + 40;
  };
  const getSlotHeight = (start, end) => {
    const [sh, sm] = start.split(":").map(Number);
    const [eh, em] = end.split(":").map(Number);
    return ((eh * 60 + em) - (sh * 60 + sm)) / 60 * 80 - 4;
  };

  if (loading) {
    return (
      <div className="student-schedule-container">
        <div className="ss-header">
          <h1>My Schedule</h1>
          <p>Loading your schedule...</p>
        </div>
        <div className="ss-status">
          <div className="ss-spinner" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="student-schedule-container">
        <div className="ss-header">
          <h1>My Schedule</h1>
          <p>Weekly class schedule and timetable</p>
        </div>
        <div className="ss-status">
          <AlertCircle size={48} color="#dc2626" />
          <h3>Unable to load schedule</h3>
          <p className="ss-status-text">{error}</p>
          <button className="ss-retry-btn" onClick={() => window.location.reload()}>
            Try Again
          </button>
        </div>
      </div>
    );
  }

  if (scheduleData.length === 0) {
    return (
      <div className="student-schedule-container">
        <div className="ss-header">
          <h1>My Schedule</h1>
          <p>Weekly class schedule and timetable</p>
        </div>
        <div className="ss-status">
          <h3>No schedule data</h3>
          <p className="ss-status-text">There are no scheduled classes for your current academic scope.</p>
        </div>
      </div>
    );
  }

  const uniqueCourses = scheduleData.reduce((acc, s) => {
    if (!acc.find(u => u.code === s.code)) {
      acc.push({
        code: s.code,
        title: s.title,
        color: s.color,
        slots: scheduleData.filter(x => x.code === s.code),
      });
    }
    return acc;
  }, []);

  return (
    <div className="student-schedule-container">
      <div className="ss-header">
        <h1>My Schedule</h1>
        <p>Weekly class schedule and timetable</p>
      </div>

      <div className="ss-content">
        <div className="ss-weekly">
          <h2>Weekly Timetable</h2>
          <div className="ss-timetable">
            <div className="ss-time-header">
              <div className="time-label-header"></div>
              {DAYS.map(day => (
                <div key={day} className="day-header">{day}</div>
              ))}
            </div>

            <div className="ss-time-grid">
              <div className="time-labels">
                {HOURS.map((h, i) => (
                  <div key={h} className="time-label" style={{ top: i * 80 + 40 }}>
                    {h}
                  </div>
                ))}
              </div>

              <div className="ss-grid-body">
                <div className="grid-lines">
                  {HOURS.map((_, i) => (
                    <div key={i} className="grid-line" style={{ top: i * 80 + 40 }}></div>
                  ))}
                </div>

                {DAYS.map(day => (
                  <div key={day} className="day-column">
                    {scheduleData.filter(s => s.day === day).map((slot, i) => (
                      <div
                        key={i}
                        className={`ss-slot${slot.isClosed ? " ss-slot-closed" : ""}`}
                        style={{
                          top: getSlotTop(slot.start),
                          height: getSlotHeight(slot.start, slot.end),
                          backgroundColor: slot.color,
                        }}
                      >
                        <strong>{slot.code}</strong>
                        <small>{slot.title}</small>
                        <small>{slot.room} &middot; {slot.start}-{slot.end}</small>
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="ss-calendar">
          <div className="calendar-header">
            <button onClick={prevMonth}><ChevronLeft size={20} /></button>
            <h3>{MONTHS[currentMonth]} {currentYear}</h3>
            <button onClick={nextMonth}><ChevronRight size={20} /></button>
          </div>
          <div className="calendar-weekdays">
            {WEEKDAYS.map(d => <div key={d} className="weekday">{d}</div>)}
          </div>
          <div className="calendar-grid">
            {calendarDays.map((day, i) => (
              <div key={i} className={`calendar-day ${day === null ? "empty" : day === now.getDate() && currentMonth === now.getMonth() && currentYear === now.getFullYear() ? "today" : ""}`}>
                {day !== null && <span className="day-number">{day}</span>}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="ss-section">
        <h2>Course Schedule Details</h2>
        <div className="ss-details-list">
          {uniqueCourses.map((course, i) => (
            <div key={i} className="ss-detail-card">
              <div className="detail-color" style={{ backgroundColor: course.color }}></div>
              <div className="detail-info">
                <h4>{course.title}</h4>
                <p>{course.code}{course.slots[0]?.sectionCode ? ` (Section ${course.slots[0].sectionCode})` : ""}</p>
              </div>
              <div className="detail-times">
                {course.slots.map((s, j) => (
                  <div key={j} className="time-chip">
                    <Clock size={12} />
                    <span>{s.day} {s.start}-{s.end}</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default StudentSchedule;
