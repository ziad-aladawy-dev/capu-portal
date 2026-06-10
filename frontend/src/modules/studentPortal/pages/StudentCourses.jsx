import { useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertCircle, BookOpen, History, CheckCircle, Clock, XCircle } from "lucide-react";
import { useRegisteredCourses, useRegistrationHistory } from "../hooks/useAcademics";
import { REGISTRATION_STATUS, getRegistrationStatusLabel } from "../../../core/services/registrationService";
import "../styles/studentCourses.css";

function StatusBadge({ status }) {
  const map = {
    [REGISTRATION_STATUS.Enrolled]: { cls: "open", Icon: Clock },
    [REGISTRATION_STATUS.Completed]: { cls: "completed", Icon: CheckCircle },
    [REGISTRATION_STATUS.Withdrawn]: { cls: "withdrawn", Icon: XCircle },
  };
  const { cls, Icon } = map[status] || { cls: "", Icon: Clock };
  return (
    <span className={`sc-status-badge ${cls}`}>
      <Icon size={12} /> {getRegistrationStatusLabel(status)}
    </span>
  );
}

function CourseCard({ c }) {
  return (
    <div className="sc-course-card active">
      <div className="card-header">
        <h3>{c.courseTitle}</h3>
        <span className="course-code">{c.courseCode}</span>
      </div>
      <div className="card-info">
        <div className="info-row"><span className="label">Credits:</span><span>{c.creditHours}</span></div>
        <div className="info-row"><span className="label">Term:</span><span>{c.semesterName}</span></div>
        {c.attemptNumber > 1 && (
          <div className="info-row"><span className="label">Attempt:</span><span>#{c.attemptNumber}</span></div>
        )}
      </div>
      <div className="card-schedule">
        <StatusBadge status={c.status} />
      </div>
    </div>
  );
}

function Loading() {
  return <div className="sc-status"><div className="sc-spinner" /></div>;
}

function StudentCourses() {
  const { t } = useTranslation();
  const [tab, setTab] = useState("current");
  const current = useRegisteredCourses();
  const history = useRegistrationHistory();

  const registered = current.data || [];
  const totalCredits = registered.reduce((s, c) => s + (c.creditHours || 0), 0);
  const completed = registered.filter((c) => c.status === REGISTRATION_STATUS.Completed).length;

  return (
    <div className="student-courses-container">
      <div className="sc-header">
        <h1>{t("my_courses", { defaultValue: "My Courses" })}</h1>
        <p>{t("courses.subtitle", { defaultValue: "Your registered courses, synced from academic records" })}</p>
      </div>

      <div className="sc-tabs">
        <button className={tab === "current" ? "active" : ""} onClick={() => setTab("current")}>
          <BookOpen size={15} /> {t("courses.current", { defaultValue: "Current" })}
        </button>
        <button className={tab === "history" ? "active" : ""} onClick={() => setTab("history")}>
          <History size={15} /> {t("courses.history", { defaultValue: "History" })}
        </button>
      </div>

      {tab === "current" && (
        <>
          {current.isLoading ? (
            <Loading />
          ) : current.isError ? (
            <div className="sc-status">
              <AlertCircle size={40} color="#dc2626" />
              <p className="sc-status-text">Unable to load your courses.</p>
              <button className="sc-retry-btn" onClick={() => current.refetch()}>Try Again</button>
            </div>
          ) : registered.length === 0 ? (
            <div className="sc-status">
              <h3>No registered courses</h3>
              <p className="sc-status-text">You have no active course registrations this term.</p>
            </div>
          ) : (
            <>
              <div className="sc-section">
                <h2>{t("courses.registered", { defaultValue: "Registered Courses" })} ({registered.length})</h2>
                <div className="sc-courses-grid">
                  {registered.map((c) => <CourseCard key={c.id} c={c} />)}
                </div>
              </div>
              <div className="sc-section">
                <h2>Summary</h2>
                <div className="sc-stats">
                  <div className="stat"><div className="stat-value">{registered.length}</div><div className="stat-label">Registered</div></div>
                  <div className="stat"><div className="stat-value">{totalCredits}</div><div className="stat-label">Credits</div></div>
                  <div className="stat"><div className="stat-value">{completed}</div><div className="stat-label">Completed</div></div>
                </div>
              </div>
            </>
          )}
        </>
      )}

      {tab === "history" && (
        <>
          {history.isLoading ? (
            <Loading />
          ) : history.isError ? (
            <div className="sc-status">
              <AlertCircle size={40} color="#dc2626" />
              <p className="sc-status-text">Unable to load history.</p>
              <button className="sc-retry-btn" onClick={() => history.refetch()}>Try Again</button>
            </div>
          ) : (history.data || []).length === 0 ? (
            <div className="sc-status"><h3>No history yet</h3></div>
          ) : (
            (history.data || []).map((sem) => (
              <div key={sem.semesterId} className="sc-section">
                <h2>{sem.semesterName} <span className="sc-muted">({sem.courses?.length || 0})</span></h2>
                <div className="sc-courses-grid">
                  {(sem.courses || []).map((c) => <CourseCard key={c.id} c={c} />)}
                </div>
              </div>
            ))
          )}
        </>
      )}
    </div>
  );
}

export default StudentCourses;
