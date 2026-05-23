import { useState } from "react";
import { BarChart3, TrendingUp, Award, BookOpen } from "lucide-react";
import "../styles/studentGrades.css";

const GRADE_DATA = [
  { code: "CS101", title: "Introduction to Programming", credits: 3, grade: "A-", points: 3.67, semester: "Fall 2023", status: "Completed" },
  { code: "CS201", title: "Data Structures", credits: 4, grade: "B+", points: 3.33, semester: "Fall 2023", status: "Completed" },
  { code: "MATH201", title: "Calculus II", credits: 4, grade: "A", points: 4.0, semester: "Spring 2024", status: "Completed" },
  { code: "ENG101", title: "English Composition", credits: 3, grade: "A-", points: 3.67, semester: "Fall 2023", status: "Completed" },
  { code: "PHYS201", title: "Physics II", credits: 4, grade: "B", points: 3.0, semester: "Spring 2024", status: "In Progress" },
  { code: "CS301", title: "Software Engineering", credits: 3, grade: "A", points: 4.0, semester: "Spring 2024", status: "In Progress" },
];

const GRADE_POINTS = { "A": 4.0, "A-": 3.67, "B+": 3.33, "B": 3.0, "B-": 2.67, "C+": 2.33, "C": 2.0, "C-": 1.67, "D+": 1.33, "D": 1.0, "F": 0.0 };

function calculateGPA(grades) {
  const completed = grades.filter(g => g.grade && g.status === "Completed");
  const totalPoints = completed.reduce((sum, g) => sum + (GRADE_POINTS[g.grade] || 0) * g.credits, 0);
  const totalCredits = completed.reduce((sum, g) => sum + g.credits, 0);
  return totalCredits > 0 ? (totalPoints / totalCredits).toFixed(2) : "0.00";
}

function StudentGrades() {
  const [activeTab, setActiveTab] = useState("all");
  const gpa = calculateGPA(GRADE_DATA);
  const completedCredits = GRADE_DATA.filter(g => g.status === "Completed").reduce((s, g) => s + g.credits, 0);
  const completedCount = GRADE_DATA.filter(g => g.status === "Completed").length;

  const filteredGrades = activeTab === "all" ? GRADE_DATA : GRADE_DATA.filter(g => g.semester === activeTab);
  const semesters = [...new Set(GRADE_DATA.map(g => g.semester))];

  const getGradeColor = (grade) => {
    if (!grade) return "";
    const g = GRADE_POINTS[grade] || 0;
    if (g >= 3.67) return "grade-a";
    if (g >= 3.0) return "grade-b";
    if (g >= 2.0) return "grade-c";
    return "grade-d";
  };

  return (
    <div className="student-grades-container">
      <div className="sg-header">
        <h1>Grades & Academic Performance</h1>
        <p>View your academic records and performance summary</p>
      </div>

      <div className="sg-stats-grid">
        <div className="sg-stat-card highlight">
          <div className="stat-icon">
            <BarChart3 size={24} />
          </div>
          <div>
            <div className="stat-value">{gpa}</div>
            <div className="stat-label">Cumulative GPA</div>
          </div>
        </div>

        <div className="sg-stat-card">
          <div className="stat-icon completed">
            <Award size={24} />
          </div>
          <div>
            <div className="stat-value">{completedCount}</div>
            <div className="stat-label">Completed Courses</div>
          </div>
        </div>

        <div className="sg-stat-card">
          <div className="stat-icon credits">
            <BookOpen size={24} />
          </div>
          <div>
            <div className="stat-value">{completedCredits}</div>
            <div className="stat-label">Credits Earned</div>
          </div>
        </div>

        <div className="sg-stat-card">
          <div className="stat-icon trend">
            <TrendingUp size={24} />
          </div>
          <div>
            <div className="stat-value">Good</div>
            <div className="stat-label">Academic Standing</div>
          </div>
        </div>
      </div>

      {/* Tab Filter */}
      <div className="sg-tabs">
        <button className={`tab ${activeTab === "all" ? "active" : ""}`} onClick={() => setActiveTab("all")}>All Semesters</button>
        {semesters.map(s => (
          <button key={s} className={`tab ${activeTab === s ? "active" : ""}`} onClick={() => setActiveTab(s)}>{s}</button>
        ))}
      </div>

      {/* Grade Table */}
      <div className="sg-table-wrapper">
        <table className="sg-table">
          <thead>
            <tr>
              <th>Course</th>
              <th>Code</th>
              <th>Credits</th>
              <th>Grade</th>
              <th>Points</th>
              <th>Semester</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {filteredGrades.map((g, i) => (
              <tr key={i}>
                <td className="course-title">{g.title}</td>
                <td>{g.code}</td>
                <td>{g.credits}</td>
                <td><span className={`grade-badge ${getGradeColor(g.grade)}`}>{g.grade || "-"}</span></td>
                <td>{g.points.toFixed(2)}</td>
                <td>{g.semester}</td>
                <td><span className={`status-badge ${g.status === "Completed" ? "completed" : "in-progress"}`}>{g.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Semester GPA */}
      <div className="sg-section">
        <h2>Semester GPA Breakdown</h2>
        <div className="sg-semester-cards">
          {semesters.map(sem => {
            const semGrades = GRADE_DATA.filter(g => g.semester === sem && g.grade);
            const semGPA = calculateGPA(semGrades);
            return (
              <div key={sem} className="sg-semester-card">
                <h3>{sem}</h3>
                <div className="sem-gpa">{semGPA}</div>
                <p>Courses: {semGrades.length} | Credits: {semGrades.reduce((s, g) => s + g.credits, 0)}</p>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

export default StudentGrades;
