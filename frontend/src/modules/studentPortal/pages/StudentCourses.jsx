import { useState, useEffect } from "react";
import { Clock, MapPin, AlertCircle } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import * as scheduleService from "../../../core/services/scheduleService";
import * as courseService from "../../../core/services/courseService";
import api from "../../../core/api/apiClient";
import "../styles/studentCourses.css";

const DAY_SHORT = { 1: "Mon", 2: "Tue", 3: "Wed", 4: "Thu", 5: "Fri" };

function formatTime(timeStr) {
  const parts = timeStr.split(":");
  return `${parseInt(parts[0], 10)}:${parts[1] || "00"}`;
}

function buildScheduleString(slots) {
  const byTime = {};
  slots.forEach(s => {
    const key = `${s.start}-${s.end}`;
    if (!byTime[key]) byTime[key] = { start: s.start, end: s.end, days: [] };
    const dayNum = typeof s.day === "number" ? s.day : parseInt(s.day, 10);
    const label = DAY_SHORT[dayNum];
    if (label && !byTime[key].days.includes(label)) byTime[key].days.push(label);
  });
  return Object.values(byTime)
    .map(g => `${g.days.join("/")} ${g.start}-${g.end}`)
    .join(", ");
}

function buildLocations(slots) {
  const locs = [...new Set(slots.map(s => s.location).filter(Boolean))];
  return locs.join(", ") || "TBD";
}

function StudentCourses() {
  const { activeScope } = useAuth();
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
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
        if (!cancelled) { setError("Academic scope not configured"); setLoading(false); }
        return;
      }

      try {
        const resp = await api.get(`/course-offerings/node/${nodeId}/semester/${semesterId}`);
        const offerings = Array.isArray(resp.data) ? resp.data : [];

        if (offerings.length === 0) {
          if (!cancelled) { setCourses([]); setLoading(false); }
          return;
        }

        const slotResults = await Promise.allSettled(
          offerings.map(o =>
            scheduleService.fetchSlotsForOffering(o.id)
              .then(slots => ({ offeringId: o.id, slots: Array.isArray(slots) ? slots : [] }))
          )
        );

        const slotsByOffering = {};
        slotResults.forEach(r => {
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

        const data = [];
        offerings.forEach(o => {
          const course = courseMap[o.courseId];
          if (!course) return;
          const slots = (slotsByOffering[o.id] || []).map(s => ({
            start: formatTime(s.startTime),
            end: formatTime(s.endTime),
            day: s.dayOfWeek,
            location: s.location,
          }));

          data.push({
            id: o.id,
            code: course.code || "?",
            title: course.title || "Unknown",
            credits: course.creditHours || 0,
            sectionCode: o.sectionCode,
            schedule: slots.length > 0 ? buildScheduleString(slots) : "No schedule",
            location: slots.length > 0 ? buildLocations(slots) : "TBD",
            registeredCount: o.registeredCount,
            capacity: o.capacity,
            registrationState: o.registrationState,
            slots,
          });
        });

        if (!cancelled) {
          setCourses(data);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err.message || "Failed to load courses");
          setLoading(false);
        }
      }
    };

    fetchData();
    return () => { cancelled = true; };
  }, [activeScope]);

  const totalCredits = courses.reduce((sum, c) => sum + (c.credits || 0), 0);

  if (loading) {
    return (
      <div className="student-courses-container">
        <div className="sc-header">
          <h1>My Courses</h1>
          <p>Loading your courses...</p>
        </div>
        <div className="sc-status">
          <div className="sc-spinner" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="student-courses-container">
        <div className="sc-header">
          <h1>My Courses</h1>
          <p>Course offerings and schedules</p>
        </div>
        <div className="sc-status">
          <AlertCircle size={48} color="#dc2626" />
          <h3>Unable to load courses</h3>
          <p className="sc-status-text">{error}</p>
          <button className="sc-retry-btn" onClick={() => window.location.reload()}>
            Try Again
          </button>
        </div>
      </div>
    );
  }

  if (courses.length === 0) {
    return (
      <div className="student-courses-container">
        <div className="sc-header">
          <h1>My Courses</h1>
          <p>Course offerings and schedules</p>
        </div>
        <div className="sc-status">
          <h3>No courses available</h3>
          <p className="sc-status-text">
            There are no course offerings for your current academic scope.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="student-courses-container">
      <div className="sc-header">
        <h1>My Courses</h1>
        <p>Course offerings and schedules</p>
      </div>

      <div className="sc-section">
        <h2>Course Offerings ({courses.length})</h2>
        <div className="sc-courses-grid">
          {courses.map(course => (
            <div key={course.id} className="sc-course-card active">
              <div className="card-header">
                <h3>{course.title}</h3>
                <span className="course-code">{course.code}</span>
              </div>

              <div className="card-info">
                <div className="info-row">
                  <span className="label">Credits:</span>
                  <span>{course.credits}</span>
                </div>
                {course.sectionCode && (
                  <div className="info-row">
                    <span className="label">Section:</span>
                    <span>{course.sectionCode}</span>
                  </div>
                )}
                {course.capacity > 0 && (
                  <div className="info-row">
                    <span className="label">Enrolled:</span>
                    <span>{course.registeredCount}/{course.capacity}</span>
                  </div>
                )}
              </div>

              <div className="card-schedule">
                <div className="schedule-item">
                  <Clock size={14} />
                  <span>{course.schedule}</span>
                </div>
                <div className="schedule-item">
                  <MapPin size={14} />
                  <span>{course.location}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="sc-section">
        <h2>Academic Summary</h2>
        <div className="sc-stats">
          <div className="stat">
            <div className="stat-value">{courses.length}</div>
            <div className="stat-label">Total Offerings</div>
          </div>
          <div className="stat">
            <div className="stat-value">{totalCredits}</div>
            <div className="stat-label">Total Credits</div>
          </div>
          <div className="stat">
            <div className="stat-value">{courses.filter(c => c.slots.length > 0).length}</div>
            <div className="stat-label">With Schedule</div>
          </div>
          <div className="stat">
            <div className="stat-value">{courses.filter(c => c.registrationState === 1).length}</div>
            <div className="stat-label">Registration Open</div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default StudentCourses;
