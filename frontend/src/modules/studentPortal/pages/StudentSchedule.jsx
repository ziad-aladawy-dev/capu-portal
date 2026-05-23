import { useState } from "react";
import { ChevronLeft, ChevronRight, Clock, MapPin, User } from "lucide-react";
import "../styles/studentSchedule.css";

const DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
const HOURS = ["8:00", "9:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00"];

const SCHEDULE_DATA = [
  { day: "Monday", start: "9:00", end: "10:30", code: "CS101", title: "Introduction to Programming", instructor: "Dr. Smith", room: "A-101", color: "#1a1f5e" },
  { day: "Monday", start: "13:00", end: "14:30", code: "PHYS201", title: "Physics II", instructor: "Dr. Miller", room: "D-301", color: "#c9a84c" },
  { day: "Tuesday", start: "10:00", end: "11:30", code: "CS201", title: "Data Structures", instructor: "Dr. Johnson", room: "B-205", color: "#2563eb" },
  { day: "Wednesday", start: "9:00", end: "10:30", code: "CS101", title: "Introduction to Programming", instructor: "Dr. Smith", room: "A-101", color: "#1a1f5e" },
  { day: "Wednesday", start: "13:00", end: "14:30", code: "PHYS201", title: "Physics II", instructor: "Dr. Miller", room: "D-301", color: "#c9a84c" },
  { day: "Thursday", start: "10:00", end: "11:30", code: "CS201", title: "Data Structures", instructor: "Dr. Johnson", room: "B-205", color: "#2563eb" },
  { day: "Friday", start: "9:00", end: "10:30", code: "CS101", title: "Introduction to Programming", instructor: "Dr. Smith", room: "A-101", color: "#1a1f5e" },
];

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function StudentSchedule() {
  const now = new Date();
  const [currentMonth, setCurrentMonth] = useState(now.getMonth());
  const [currentYear, setCurrentYear] = useState(now.getFullYear());

  const getSlotTop = (time) => {
    const [h, m] = time.split(":").map(Number);
    const base = (h - 8) * 60 + m;
    return (base / 60) * 80 + 40;
  };

  const getSlotHeight = (start, end) => {
    const [sh, sm] = start.split(":").map(Number);
    const [eh, em] = end.split(":").map(Number);
    return ((eh * 60 + em) - (sh * 60 + sm)) / 60 * 80 - 4;
  };

  const prevMonth = () => {
    if (currentMonth === 0) { setCurrentMonth(11); setCurrentYear(currentYear - 1); }
    else setCurrentMonth(currentMonth - 1);
  };

  const nextMonth = () => {
    if (currentMonth === 11) { setCurrentMonth(0); setCurrentYear(currentYear + 1); }
    else setCurrentMonth(currentMonth + 1);
  };

  const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate();
  const firstDay = new Date(currentYear, currentMonth, 1).getDay();
  const calendarDays = [];
  for (let i = 0; i < firstDay; i++) calendarDays.push(null);
  for (let i = 1; i <= daysInMonth; i++) calendarDays.push(i);

  return (
    <div className="student-schedule-container">
      <div className="ss-header">
        <h1>My Schedule</h1>
        <p>Weekly class schedule and timetable</p>
      </div>

      <div className="ss-content">
        {/* Weekly Schedule */}
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
                    {SCHEDULE_DATA.filter(s => s.day === day).map((slot, i) => (
                      <div
                        key={i}
                        className="ss-slot"
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

        {/* Calendar View */}
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
              <div key={i} className={`calendar-day ${day === null ? "empty" : day === now.getDate() && currentMonth === now.getMonth() && currentYear === now.getFullYear() ? "today" : ""} ${day !== null && SCHEDULE_DATA.some(() => true) ? "has-events" : ""}`}>
                {day !== null && <span className="day-number">{day}</span>}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Schedule Summary */}
      <div className="ss-section">
        <h2>Course Schedule Details</h2>
        <div className="ss-details-list">
          {SCHEDULE_DATA.reduce((unique, s) => {
            if (!unique.find(u => u.code === s.code)) unique.push(s);
            return unique;
          }, []).map((course, i) => (
            <div key={i} className="ss-detail-card">
              <div className="detail-color" style={{ backgroundColor: course.color }}></div>
              <div className="detail-info">
                <h4>{course.title}</h4>
                <p>{course.code}</p>
              </div>
              <div className="detail-meta">
                <div className="meta-item"><User size={14} /><span>{course.instructor}</span></div>
                <div className="meta-item"><MapPin size={14} /><span>{course.room}</span></div>
              </div>
              <div className="detail-times">
                {SCHEDULE_DATA.filter(s => s.code === course.code).map((s, j) => (
                  <div key={j} className="time-chip">
                    <Clock size={12} />
                    <span>{s.day}s {s.start}-{s.end}</span>
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
