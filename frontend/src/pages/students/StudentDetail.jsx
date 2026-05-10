import React, { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { 
  getStudentById, 
  getStudentEnrollments, 
  getCollegeById, 
  getProgramById,
  mockColleges,
  mockPrograms,
  mockAcademicYears,
  mockSemesters
} from "../../lib/mock-data";
import { 
  ArrowLeft, 
  User, 
  BookOpen, 
  Award, 
  DollarSign, 
  Phone, 
  Mail, 
  MapPin, 
  Calendar,
  GraduationCap,
  Edit,
  Download,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle
} from "lucide-react";
import "./StudentDetail.css";

const TAB_TYPE = {
  PROFILE: "profile",
  ENROLLMENT: "enrollment",
  GRADES: "grades",
  FINANCIAL: "financial"
};

export const StudentDetail = () => {
  const { studentId } = useParams();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState(TAB_TYPE.PROFILE);

  const student = studentId ? getStudentById(studentId) : undefined;
  const enrollments = studentId ? getStudentEnrollments(studentId) : [];

  if (!student) {
    return (
      <div className="student-detail-container">
        <div className="not-found">
          <User size={64} />
          <h2>Student Not Found</h2>
          <p>The student with ID "{studentId}" could not be found.</p>
          <button onClick={() => navigate("/students/list")} className="back-btn">
            <ArrowLeft size={18} />
            Back to Students
          </button>
        </div>
      </div>
    );
  }

  const college = getCollegeById(student.collegeId);
  const program = getProgramById(student.programId);

  const tabs = [
    { id: "profile", label: "Profile", icon: User },
    { id: "enrollment", label: "Enrollment", icon: BookOpen },
    { id: "grades", label: "Grades", icon: Award },
    { id: "financial", label: "Financial", icon: DollarSign }
  ];

  const getGradeColor = (grade) => {
    if (!grade) return "";
    const g = grade.replace("+", "").replace("-", "");
    if (["A", "A+"].includes(grade) || g === "A") return "grade-a";
    if (["B", "B+", "B-"].includes(grade) || g === "B") return "grade-b";
    if (["C", "C+", "C-"].includes(grade) || g === "C") return "grade-c";
    if (["D", "D+", "D-"].includes(grade) || g === "D") return "grade-d";
    return "grade-f";
  };

  return (
    <div className="student-detail-container">
      {/* Header */}
      <div className="detail-header">
        <button onClick={() => navigate("/students/list")} className="back-button">
          <ArrowLeft size={20} />
        </button>
        <div className="header-main">
          <div className="student-avatar-large">
            {student.firstName.charAt(0)}{student.lastName.charAt(0)}
          </div>
          <div className="header-info">
            <h1>{student.firstName} {student.lastName}</h1>
            <div className="header-meta">
              <span className="meta-item">
                <GraduationCap size={14} />
                {student.studentId}
              </span>
              <span className="meta-item">
                {program?.name} - {college?.name}
              </span>
              <span className={`status-badge-large ${student.enrollmentStatus.toLowerCase().replace(" ", "-")}`}>
                {student.enrollmentStatus}
              </span>
            </div>
          </div>
        </div>
        <div className="header-actions">
          <button className="action-btn secondary">
            <Edit size={16} />
            Edit
          </button>
          <button className="action-btn primary">
            <Download size={16} />
            Export
          </button>
        </div>
      </div>

      {/* GPA Summary Bar */}
      <div className="gpa-summary-bar">
        <div className="gpa-item">
          <span className="gpa-label">Current GPA</span>
          <span className="gpa-value-large">{student.gpa.toFixed(2)}</span>
        </div>
        <div className="gpa-item">
          <span className="gpa-label">Total Credits</span>
          <span className="gpa-value">{student.totalCredits}</span>
        </div>
        <div className="gpa-item">
          <span className="gpa-label">Financial Status</span>
          <span className={`financial-status ${student.financialStatus.toLowerCase()}`}>
            {student.financialStatus}
          </span>
        </div>
        <div className="gpa-item">
          <span className="gpa-label">Active Courses</span>
          <span className="gpa-value">{enrollments.filter(e => e.status === "Enrolled").length}</span>
        </div>
      </div>

      {/* Tabs */}
      <div className="tabs-container">
        <div className="tabs">
          {tabs.map(tab => (
            <button
              key={tab.id}
              className={`tab ${activeTab === tab.id ? "active" : ""}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <tab.icon size={18} />
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {/* Tab Content */}
      <div className="tab-content">
        {/* Profile Tab */}
        {activeTab === "profile" && (
          <div className="profile-tab">
            <div className="info-grid">
              <div className="info-card">
                <h3>Personal Information</h3>
                <div className="info-list">
                  <div className="info-item">
                    <User size={16} />
                    <span className="label">Full Name</span>
                    <span className="value">{student.firstName} {student.lastName}</span>
                  </div>
                  <div className="info-item">
                    <Mail size={16} />
                    <span className="label">Email</span>
                    <span className="value">{student.email}</span>
                  </div>
                  <div className="info-item">
                    <Phone size={16} />
                    <span className="label">Phone</span>
                    <span className="value">{student.phone}</span>
                  </div>
                  <div className="info-item">
                    <Calendar size={16} />
                    <span className="label">Date of Birth</span>
                    <span className="value">{student.dateOfBirth}</span>
                  </div>
                  <div className="info-item">
                    <MapPin size={16} />
                    <span className="label">Address</span>
                    <span className="value">{student.address}</span>
                  </div>
                  <div className="info-item">
                    <span className="icon-placeholder">N</span>
                    <span className="label">Nationality</span>
                    <span className="value">{student.nationality}</span>
                  </div>
                  <div className="info-item">
                    <span className="icon-placeholder">G</span>
                    <span className="label">Gender</span>
                    <span className="value">{student.gender}</span>
                  </div>
                </div>
              </div>

              <div className="info-card">
                <h3>Academic Information</h3>
                <div className="info-list">
                  <div className="info-item">
                    <GraduationCap size={16} />
                    <span className="label">Student ID</span>
                    <span className="value">{student.studentId}</span>
                  </div>
                  <div className="info-item">
                    <BookOpen size={16} />
                    <span className="label">College</span>
                    <span className="value">{college?.name}</span>
                  </div>
                  <div className="info-item">
                    <GraduationCap size={16} />
                    <span className="label">Program</span>
                    <span className="value">{program?.name}</span>
                  </div>
                  <div className="info-item">
                    <Calendar size={16} />
                    <span className="label">Enrollment Date</span>
                    <span className="value">{student.enrollmentDate}</span>
                  </div>
                  <div className="info-item">
                    <Award size={16} />
                    <span className="label">Current GPA</span>
                    <span className="value">{student.gpa.toFixed(2)}</span>
                  </div>
                  <div className="info-item">
                    <BookOpen size={16} />
                    <span className="label">Credits Completed</span>
                    <span className="value">{student.totalCredits}</span>
                  </div>
                </div>
              </div>

              <div className="info-card">
                <h3>Guardian Information</h3>
                <div className="info-list">
                  <div className="info-item">
                    <User size={16} />
                    <span className="label">Guardian Name</span>
                    <span className="value">{student.guardianName}</span>
                  </div>
                  <div className="info-item">
                    <Phone size={16} />
                    <span className="label">Guardian Phone</span>
                    <span className="value">{student.guardianPhone}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Enrollment Tab */}
        {activeTab === "enrollment" && (
          <div className="enrollment-tab">
            <div className="section-header">
              <h3>Current Enrollments</h3>
              <span className="section-count">{enrollments.filter(e => e.status === "Enrolled").length} courses</span>
            </div>
            
            <div className="courses-grid">
              {enrollments.filter(e => e.status === "Enrolled").map(enrollment => (
                <div key={enrollment.id} className="course-card">
                  <div className="course-header">
                    <span className="course-code">{enrollment.courseCode}</span>
                    <span className="course-credits">{enrollment.credits} Credits</span>
                  </div>
                  <h4 className="course-name">{enrollment.courseName}</h4>
                  <div className="course-details">
                    <div className="course-detail">
                      <Clock size={14} />
                      <span>{enrollment.semester} {enrollment.academicYear}</span>
                    </div>
                    <div className="course-detail">
                      <CheckCircle size={14} />
                      <span>Attendance: {enrollment.attendancePercentage}%</span>
                    </div>
                  </div>
                  <div className="course-status-badge">
                    {enrollment.status}
                  </div>
                </div>
              ))}
            </div>

            {enrollments.filter(e => e.status === "Enrolled").length === 0 && (
              <div className="empty-section">
                <AlertCircle size={32} />
                <p>No active enrollments</p>
              </div>
            )}
          </div>
        )}

        {/* Grades Tab */}
        {activeTab === "grades" && (
          <div className="grades-tab">
            <div className="grades-table-wrapper">
              <table className="grades-table">
                <thead>
                  <tr>
                    <th>Course</th>
                    <th>Code</th>
                    <th>Credits</th>
                    <th>Semester</th>
                    <th>Grade</th>
                    <th>Points</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {enrollments.map(enrollment => (
                    <tr key={enrollment.id}>
                      <td className="course-name-cell">{enrollment.courseName}</td>
                      <td><span className="course-code-badge">{enrollment.courseCode}</span></td>
                      <td>{enrollment.credits}</td>
                      <td>{enrollment.semester} {enrollment.academicYear}</td>
                      <td>
                        <span className={`grade-badge ${getGradeColor(enrollment.grade || "")}`}>
                          {enrollment.grade || "-"}
                        </span>
                      </td>
                      <td>{enrollment.gradePoints?.toFixed(1) || "-"}</td>
                      <td>
                        <span className={`status-cell ${enrollment.status.toLowerCase()}`}>
                          {enrollment.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Financial Tab */}
        {activeTab === "financial" && (
          <div className="financial-tab">
            <div className="financial-summary">
              <div className={`financial-card ${student.financialStatus.toLowerCase()}`}>
                <div className="financial-icon">
                  <DollarSign size={24} />
                </div>
                <div className="financial-info">
                  <span className="financial-label">Payment Status</span>
                  <span className="financial-value">{student.financialStatus}</span>
                </div>
              </div>
            </div>

            <div className="financial-details">
              <div className="info-card">
                <h3>Payment History</h3>
                <div className="payment-list">
                  <div className="payment-item">
                    <div className="payment-info">
                      <span className="payment-desc">Fall 2026 Tuition</span>
                      <span className="payment-date">Sep 1, 2026</span>
                    </div>
                    <div className="payment-amount">$2,500.00</div>
                    <span className={`payment-status ${student.financialStatus.toLowerCase()}`}>
                      {student.financialStatus === "Paid" ? "Paid" : 
                       student.financialStatus === "Pending" ? "Pending" :
                       student.financialStatus === "Partial" ? "Partial" : "Overdue"}
                    </span>
                  </div>
                  <div className="payment-item">
                    <div className="payment-info">
                      <span className="payment-desc">Spring 2025 Tuition</span>
                      <span className="payment-date">Jan 15, 2025</span>
                    </div>
                    <div className="payment-amount">$2,500.00</div>
                    <span className="payment-status paid">Paid</span>
                  </div>
                  <div className="payment-item">
                    <div className="payment-info">
                      <span className="payment-desc">Fall 2025 Tuition</span>
                      <span className="payment-date">Sep 1, 2025</span>
                    </div>
                    <div className="payment-amount">$2,500.00</div>
                    <span className="payment-status paid">Paid</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};