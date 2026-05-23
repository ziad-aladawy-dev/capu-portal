import { useState } from "react";
import { BookOpen, Users, Clock, MapPin } from "lucide-react";
import "../styles/studentCourses.css";

function StudentCourses() {
  // Mock course data - in production, this would come from the backend
  const [courses] = useState([
    {
      id: 1,
      code: "CS101",
      title: "Introduction to Programming",
      instructor: "Dr. Smith",
      credits: 3,
      grade: "A-",
      status: "Completed",
      schedule: "MWF 10:00 AM - 11:30 AM",
      location: "Building A, Room 101",
      students: 35,
    },
    {
      id: 2,
      code: "CS201",
      title: "Data Structures",
      instructor: "Dr. Johnson",
      credits: 4,
      grade: "B+",
      status: "In Progress",
      schedule: "TTh 1:00 PM - 2:30 PM",
      location: "Building B, Room 205",
      students: 28,
    },
    {
      id: 3,
      code: "MATH201",
      title: "Calculus II",
      instructor: "Prof. Williams",
      credits: 4,
      grade: "A",
      status: "In Progress",
      schedule: "MWF 2:00 PM - 3:30 PM",
      location: "Building C, Room 115",
      students: 42,
    },
    {
      id: 4,
      code: "ENG101",
      title: "English Composition",
      instructor: "Dr. Brown",
      credits: 3,
      grade: "A-",
      status: "Completed",
      schedule: "TTh 10:00 AM - 11:30 AM",
      location: "Building A, Room 201",
      students: 22,
    },
    {
      id: 5,
      code: "PHYS201",
      title: "Physics II",
      instructor: "Dr. Miller",
      credits: 4,
      grade: null,
      status: "In Progress",
      schedule: "MWF 1:00 PM - 2:30 PM",
      location: "Building D, Room 301",
      students: 31,
    },
  ]);

  const completedCourses = courses.filter((c) => c.status === "Completed");
  const activeCourses = courses.filter((c) => c.status === "In Progress");

  const getGradeColor = (grade) => {
    if (!grade) return "";
    const firstChar = grade.charAt(0);
    if (firstChar === "A") return "grade-a";
    if (firstChar === "B") return "grade-b";
    if (firstChar === "C") return "grade-c";
    if (firstChar === "D") return "grade-d";
    return "grade-f";
  };

  return (
    <div className="student-courses-container">
      <div className="sc-header">
        <h1>My Courses</h1>
        <p>Enrolled courses and grades</p>
      </div>

      {/* Active Courses */}
      <div className="sc-section">
        <h2>Current Courses ({activeCourses.length})</h2>
        <div className="sc-courses-grid">
          {activeCourses.map((course) => (
            <div key={course.id} className="sc-course-card active">
              <div className="card-header">
                <h3>{course.title}</h3>
                <span className="course-code">{course.code}</span>
              </div>

              <div className="card-info">
                <div className="info-row">
                  <span className="label">Instructor:</span>
                  <span>{course.instructor}</span>
                </div>
                <div className="info-row">
                  <span className="label">Credits:</span>
                  <span>{course.credits}</span>
                </div>
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
                <div className="schedule-item">
                  <Users size={14} />
                  <span>{course.students} students</span>
                </div>
              </div>

              <div className="card-status">
                <span className="status-badge in-progress">In Progress</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Completed Courses */}
      <div className="sc-section">
        <h2>Completed Courses ({completedCourses.length})</h2>
        <div className="sc-courses-list">
          {completedCourses.map((course) => (
            <div key={course.id} className="sc-course-row">
              <div className="row-left">
                <div>
                  <h4>{course.title}</h4>
                  <p>{course.code}</p>
                </div>
              </div>
              <div className="row-right">
                <div className="row-info">
                  <span className="label">Instructor:</span>
                  <span>{course.instructor}</span>
                </div>
                <div className="row-info">
                  <span className="label">Credits:</span>
                  <span>{course.credits}</span>
                </div>
                <div className={`row-grade ${getGradeColor(course.grade)}`}>
                  {course.grade}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Course Statistics */}
      <div className="sc-section">
        <h2>Academic Summary</h2>
        <div className="sc-stats">
          <div className="stat">
            <div className="stat-value">{courses.length}</div>
            <div className="stat-label">Total Courses</div>
          </div>
          <div className="stat">
            <div className="stat-value">
              {courses.reduce((sum, c) => sum + c.credits, 0)}
            </div>
            <div className="stat-label">Total Credits</div>
          </div>
          <div className="stat">
            <div className="stat-value">{activeCourses.length}</div>
            <div className="stat-label">Active Courses</div>
          </div>
          <div className="stat">
            <div className="stat-value">
              {courses
                .filter((c) => c.grade)
                .reduce((sum, c) => sum + parseFloat(c.grade), 0)
                .toFixed(1)}
            </div>
            <div className="stat-label">Average Grade (Numeric)</div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default StudentCourses;
