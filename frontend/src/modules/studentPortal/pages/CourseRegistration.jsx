import { useState } from "react";
import { Plus, Search, X, Calendar, Clock, MapPin, AlertCircle } from "lucide-react";
import "../styles/courseRegistration.css";

const AVAILABLE_COURSES = [
  { id: 101, code: "CS301", title: "Software Engineering", instructor: "Dr. Smith", credits: 3, schedule: "MWF 9:00 AM", capacity: 30, enrolled: 22, faculty: "Engineering" },
  { id: 102, code: "CS302", title: "Database Systems", instructor: "Dr. Johnson", credits: 4, schedule: "TTh 10:00 AM", capacity: 35, enrolled: 28, faculty: "Engineering" },
  { id: 103, code: "MATH301", title: "Linear Algebra", instructor: "Prof. Williams", credits: 3, schedule: "MWF 11:00 AM", capacity: 40, enrolled: 38, faculty: "Science" },
  { id: 104, code: "PHYS301", title: "Quantum Mechanics", instructor: "Dr. Miller", credits: 4, schedule: "TTh 1:00 PM", capacity: 25, enrolled: 12, faculty: "Science" },
  { id: 105, code: "ENG201", title: "Technical Writing", instructor: "Dr. Brown", credits: 3, schedule: "MWF 2:00 PM", capacity: 30, enrolled: 15, faculty: "Arts" },
  { id: 106, code: "CS303", title: "Computer Networks", instructor: "Dr. Davis", credits: 3, schedule: "TTh 3:00 PM", capacity: 35, enrolled: 25, faculty: "Engineering" },
  { id: 107, code: "MATH302", title: "Differential Equations", instructor: "Prof. Wilson", credits: 3, schedule: "MWF 10:00 AM", capacity: 30, enrolled: 30, faculty: "Science" },
  { id: 108, code: "BUS201", title: "Introduction to Business", instructor: "Dr. Taylor", credits: 3, schedule: "TTh 9:00 AM", capacity: 45, enrolled: 40, faculty: "Business" },
];

const ENROLLED_COURSES = [
  { id: 1, code: "CS101", title: "Introduction to Programming", instructor: "Dr. Smith", credits: 3 },
  { id: 2, code: "CS201", title: "Data Structures", instructor: "Dr. Johnson", credits: 4 },
  { id: 5, code: "PHYS201", title: "Physics II", instructor: "Dr. Miller", credits: 4 },
];

function CourseRegistration() {
  const [searchTerm, setSearchTerm] = useState("");
  const [facultyFilter, setFacultyFilter] = useState("all");
  const [selectedCourses, setSelectedCourses] = useState([]);
  const [success, setSuccess] = useState(null);

  const filteredCourses = AVAILABLE_COURSES.filter((c) => {
    const matchesSearch = c.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
      c.code.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesFaculty = facultyFilter === "all" || c.faculty === facultyFilter;
    return matchesSearch && matchesFaculty;
  }).filter((c) => !ENROLLED_COURSES.find((e) => e.id === c.id));

  const faculties = [...new Set(AVAILABLE_COURSES.map((c) => c.faculty))];

  const toggleCourse = (course) => {
    setSelectedCourses((prev) =>
      prev.find((c) => c.id === course.id)
        ? prev.filter((c) => c.id !== course.id)
        : [...prev, course]
    );
  };

  const handleRegister = () => {
    setSuccess("Registration submitted successfully! Your courses are pending approval.");
    setSelectedCourses([]);
    setTimeout(() => setSuccess(null), 5000);
  };

  return (
    <div className="course-registration-container">
      <div className="cr-header">
        <div>
          <h1>Course Registration</h1>
          <p>Select and register for courses for the upcoming semester</p>
        </div>
        <div className="cr-header-actions">
          <div className="cr-cart">
            <span className="cart-count">{selectedCourses.length}</span>
          </div>
          <button
            className="btn-register"
            onClick={handleRegister}
            disabled={selectedCourses.length === 0}
          >
            Register Selected ({selectedCourses.length})
          </button>
        </div>
      </div>

      {success && (
        <div className="alert alert-success">{success}</div>
      )}

      {/* Enrolled Courses */}
      <div className="cr-section">
        <h2>Currently Enrolled</h2>
        <div className="cr-enrolled-list">
          {ENROLLED_COURSES.map((course) => (
            <div key={course.id} className="enrolled-item">
              <div className="enrolled-info">
                <span className="course-code-badge">{course.code}</span>
                <div>
                  <strong>{course.title}</strong>
                  <p>{course.instructor} &middot; {course.credits} credits</p>
                </div>
              </div>
              <span className="enrolled-badge">Enrolled</span>
            </div>
          ))}
        </div>
      </div>

      {/* Available Courses */}
      <div className="cr-section">
        <h2>Available Courses</h2>

        {/* Filters */}
        <div className="cr-filters">
          <div className="search-box">
            <Search size={16} />
            <input
              type="text"
              placeholder="Search courses..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
          <select
            value={facultyFilter}
            onChange={(e) => setFacultyFilter(e.target.value)}
            className="faculty-select"
          >
            <option value="all">All Faculties</option>
            {faculties.map((f) => (
              <option key={f} value={f}>{f}</option>
            ))}
          </select>
        </div>

        {filteredCourses.length === 0 ? (
          <div className="empty-state">
            <AlertCircle size={48} />
            <h3>No Courses Found</h3>
            <p>Try adjusting your search or filter criteria</p>
          </div>
        ) : (
          <div className="cr-courses-grid">
            {filteredCourses.map((course) => {
              const isSelected = selectedCourses.find((c) => c.id === course.id);
              const full = course.enrolled >= course.capacity;
              return (
                <div
                  key={course.id}
                  className={`cr-course-card ${isSelected ? "selected" : ""} ${full ? "full" : ""}`}
                  onClick={() => !full && toggleCourse(course)}
                >
                  <div className="cr-card-header">
                    <h3>{course.title}</h3>
                    <span className="course-code-badge">{course.code}</span>
                  </div>

                  <div className="cr-card-body">
                    <div className="cr-info-row">
                      <Calendar size={14} />
                      <span>{course.instructor}</span>
                    </div>
                    <div className="cr-info-row">
                      <Clock size={14} />
                      <span>{course.schedule}</span>
                    </div>
                    <div className="cr-info-row">
                      <MapPin size={14} />
                      <span>{course.faculty} &middot; {course.credits} credits</span>
                    </div>
                  </div>

                  <div className="cr-card-footer">
                    <div className="capacity-bar">
                      <div
                        className="capacity-fill"
                        style={{ width: `${(course.enrolled / course.capacity) * 100}%` }}
                      ></div>
                      <span>{course.enrolled}/{course.capacity} enrolled</span>
                    </div>
                    {full ? (
                      <span className="full-badge">Full</span>
                    ) : isSelected ? (
                      <span className="selected-badge">
                        <X size={14} /> Selected
                      </span>
                    ) : (
                      <span className="add-badge">
                        <Plus size={14} /> Add
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

export default CourseRegistration;
