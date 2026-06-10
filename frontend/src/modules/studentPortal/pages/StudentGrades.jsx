import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { BarChart3, TrendingUp, Award, BookOpen, Calculator, AlertCircle } from "lucide-react";
import { useGradeSummary, useGradeHistory } from "../hooks/useAcademics";
import { GRADE_POINTS, GRADE_OPTIONS, projectCgpa } from "../../../core/services/gradeService";
import "../styles/studentGrades.css";

function gradeColor(grade) {
  const g = GRADE_POINTS[grade];
  if (g == null) return "";
  if (g >= 3.7) return "grade-a";
  if (g >= 3.0) return "grade-b";
  if (g >= 2.0) return "grade-c";
  return "grade-d";
}

const isInProgress = (c) => !c.grade || c.status === 0;

function StudentGrades() {
  const { t } = useTranslation();
  const summaryQ = useGradeSummary();
  const historyQ = useGradeHistory();

  const summary = summaryQ.data;
  const history = historyQ.data || [];
  const semesters = history.map((s) => ({ id: s.semesterId, name: s.semesterName }));

  const [activeTab, setActiveTab] = useState("all");
  const visibleSemesters = activeTab === "all" ? history : history.filter((s) => s.semesterId === activeTab);
  const allCourses = history.flatMap((s) => (s.courses || []).map((c) => ({ ...c, semesterName: s.semesterName })));

  // ── GPA projection (client-side what-if) ──
  const inProgress = useMemo(() => allCourses.filter(isInProgress), [allCourses]);
  const [planned, setPlanned] = useState({}); // courseId -> letter grade
  const projected = useMemo(() => {
    const plannedRows = inProgress
      .filter((c) => planned[c.id])
      .map((c) => ({ credits: c.creditHours, grade: planned[c.id] }));
    return projectCgpa({
      priorCredits: summary?.earnedCredits ?? 0,
      priorCgpa: summary?.cgpa ?? 0,
      plannedRows,
    });
  }, [inProgress, planned, summary]);

  if (summaryQ.isLoading || historyQ.isLoading) {
    return (
      <div className="student-grades-container">
        <div className="sg-header"><h1>{t("grades", { defaultValue: "Grades" })}</h1></div>
        <div className="sg-stat-card"><div className="stat-value">…</div></div>
      </div>
    );
  }

  return (
    <div className="student-grades-container">
      <div className="sg-header">
        <h1>{t("grades_title", { defaultValue: "Grades & Academic Performance" })}</h1>
        <p>{t("grades.subtitle", { defaultValue: "Your academic records, synced from academic systems" })}</p>
      </div>

      <div className="sg-stats-grid">
        <div className="sg-stat-card highlight">
          <div className="stat-icon"><BarChart3 size={24} /></div>
          <div>
            <div className="stat-value">{summary ? Number(summary.cgpa).toFixed(2) : "—"}</div>
            <div className="stat-label">{t("grades.cgpa", { defaultValue: "Cumulative GPA" })}</div>
          </div>
        </div>
        <div className="sg-stat-card">
          <div className="stat-icon completed"><Award size={24} /></div>
          <div>
            <div className="stat-value">{summary ? Number(summary.gpa).toFixed(2) : "—"}</div>
            <div className="stat-label">{t("grades.term_gpa", { defaultValue: "Term GPA" })}</div>
          </div>
        </div>
        <div className="sg-stat-card">
          <div className="stat-icon credits"><BookOpen size={24} /></div>
          <div>
            <div className="stat-value">{summary?.earnedCredits ?? 0}</div>
            <div className="stat-label">{t("grades.credits_earned", { defaultValue: "Credits Earned" })}</div>
          </div>
        </div>
        <div className="sg-stat-card">
          <div className="stat-icon trend"><TrendingUp size={24} /></div>
          <div>
            <div className="stat-value">{summary?.academicStanding || "—"}</div>
            <div className="stat-label">{t("grades.standing", { defaultValue: "Academic Standing" })}</div>
          </div>
        </div>
      </div>

      {historyQ.isError ? (
        <div className="sg-section"><AlertCircle size={18} /> Couldn't load grade history.</div>
      ) : history.length === 0 ? (
        <div className="sg-section"><p>No grades recorded yet.</p></div>
      ) : (
        <>
          <div className="sg-tabs">
            <button className={`tab ${activeTab === "all" ? "active" : ""}`} onClick={() => setActiveTab("all")}>
              {t("grades.all_semesters", { defaultValue: "All Semesters" })}
            </button>
            {semesters.map((s) => (
              <button key={s.id} className={`tab ${activeTab === s.id ? "active" : ""}`} onClick={() => setActiveTab(s.id)}>{s.name}</button>
            ))}
          </div>

          <div className="sg-table-wrapper">
            <table className="sg-table">
              <thead>
                <tr>
                  <th>Course</th><th>Code</th><th>Credits</th><th>Grade</th><th>Score</th><th>Semester</th>
                </tr>
              </thead>
              <tbody>
                {visibleSemesters.flatMap((sem) =>
                  (sem.courses || []).map((c) => (
                    <tr key={c.id}>
                      <td className="course-title">{c.courseTitle}{c.attemptNumber > 1 ? ` (#${c.attemptNumber})` : ""}</td>
                      <td>{c.courseCode}</td>
                      <td>{c.creditHours}</td>
                      <td><span className={`grade-badge ${gradeColor(c.grade)}`}>{c.grade || "-"}</span></td>
                      <td>{c.numericScore != null ? Number(c.numericScore).toFixed(0) : "-"}</td>
                      <td>{sem.semesterName}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Semester GPA breakdown */}
          <div className="sg-section">
            <h2>{t("grades.semester_breakdown", { defaultValue: "Semester Breakdown" })}</h2>
            <div className="sg-semester-cards">
              {history.map((sem) => {
                const graded = (sem.courses || []).filter((c) => GRADE_POINTS[c.grade] != null);
                const pts = graded.reduce((s, c) => s + GRADE_POINTS[c.grade] * c.creditHours, 0);
                const cr = graded.reduce((s, c) => s + c.creditHours, 0);
                return (
                  <div key={sem.semesterId} className="sg-semester-card">
                    <h3>{sem.semesterName}</h3>
                    <div className="sem-gpa">{cr ? (pts / cr).toFixed(2) : "—"}</div>
                    <p>Courses: {graded.length} | Credits: {cr}</p>
                  </div>
                );
              })}
            </div>
          </div>
        </>
      )}

      {/* GPA Projection calculator */}
      {inProgress.length > 0 && (
        <div className="sg-section sg-projection">
          <h2><Calculator size={18} /> {t("grades.projection", { defaultValue: "GPA Projection (What-If)" })}</h2>
          <p className="sg-muted">Pick expected grades for your in-progress courses to project your cumulative GPA.</p>
          <div className="sg-projection-rows">
            {inProgress.map((c) => (
              <div key={c.id} className="sg-projection-row">
                <span>{c.courseCode} · {c.courseTitle} ({c.creditHours} cr)</span>
                <select
                  value={planned[c.id] || ""}
                  onChange={(e) => setPlanned((p) => ({ ...p, [c.id]: e.target.value }))}
                >
                  <option value="">—</option>
                  {GRADE_OPTIONS.map((g) => <option key={g} value={g}>{g}</option>)}
                </select>
              </div>
            ))}
          </div>
          <div className="sg-projection-result">
            {t("grades.projected_cgpa", { defaultValue: "Projected CGPA" })}: <strong>{projected.toFixed(2)}</strong>
            <span className="sg-muted"> (current {summary ? Number(summary.cgpa).toFixed(2) : "—"})</span>
          </div>
        </div>
      )}
    </div>
  );
}

export default StudentGrades;
