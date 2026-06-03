import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { Plus, BarChart3, BookOpen, Calendar, FileText, AlertCircle, Receipt } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import * as studentService from "../../../core/services/studentService";
import "../styles/studentDashboard.css";

function StudentDashboard() {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchStudentData = async () => {
      try {
        if (user?.id) {
          await studentService.fetchStudentById(user.id);
        }
      } catch (err) {
        setError(err.message || "Failed to load student data");
      } finally {
        setLoading(false);
      }
    };

    fetchStudentData();
  }, [user?.id]);

  const gpa = (3.45).toFixed(2); // Mock GPA
  const completedCredits = 45;
  const totalCredits = 120;
  const enrolledCourses = 5; // Mock enrollment count

  return (
    <div className="student-dashboard">
      <div className="sd-header">
        <div className="sd-welcome">
          <h1>Welcome, {user?.name || "Student"}</h1>
          <p className="sd-subtitle">
            Here's your academic overview for this semester
          </p>
        </div>
      </div>

      {error && (
        <div className="alert alert-warning">
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="sd-loading">
          <div className="spinner"></div>
          <p>Loading your data...</p>
        </div>
      ) : (
        <>
          {/* Stats Cards */}
          <div className="sd-stats-grid">
            <div className="sd-stat-card">
              <div className="stat-icon gpa">
                <BarChart3 size={24} />
              </div>
              <div className="stat-content">
                <div className="stat-value">{gpa}</div>
                <div className="stat-label">Current GPA</div>
              </div>
            </div>

            <div className="sd-stat-card">
              <div className="stat-icon courses">
                <BookOpen size={24} />
              </div>
              <div className="stat-content">
                <div className="stat-value">{enrolledCourses}</div>
                <div className="stat-label">Enrolled Courses</div>
              </div>
            </div>

            <div className="sd-stat-card">
              <div className="stat-icon credits">
                <FileText size={24} />
              </div>
              <div className="stat-content">
                <div className="stat-value">
                  {completedCredits}/{totalCredits}
                </div>
                <div className="stat-label">Credits Completed</div>
              </div>
            </div>

            <div className="sd-stat-card">
              <div className="stat-icon schedule">
                <Calendar size={24} />
              </div>
              <div className="stat-content">
                <div className="stat-value">Spring 2024</div>
                <div className="stat-label">Current Semester</div>
              </div>
            </div>
          </div>

          {/* Quick Actions */}
          <div className="sd-section">
            <h2>Quick Actions</h2>
            <div className="sd-actions-grid">
              <Link to="/student/courses" className="action-card">
                <BookOpen size={20} />
                <div>
                  <h3>My Courses</h3>
                  <p>View enrolled courses</p>
                </div>
              </Link>
              <Link to="/student/courses/register" className="action-card">
                <Plus size={20} />
                <div>
                  <h3>Register Courses</h3>
                  <p>Add new courses</p>
                </div>
              </Link>
              <Link to="/student/grades" className="action-card">
                <BarChart3 size={20} />
                <div>
                  <h3>View Grades</h3>
                  <p>Check your performance</p>
                </div>
              </Link>
              <Link to="/student/schedule" className="action-card">
                <Calendar size={20} />
                <div>
                  <h3>My Schedule</h3>
                  <p>Class timetable</p>
                </div>
              </Link>
              <Link to="/student/payments" className="action-card">
                <Receipt size={20} />
                <div>
                  <h3>Payments & Fees</h3>
                  <p>View financial status</p>
                </div>
              </Link>
            </div>
          </div>

          {/* Recent Activities */}
          <div className="sd-section">
            <h2>Recent Activities</h2>
            <div className="sd-activities">
              <div className="activity-item">
                <div className="activity-dot"></div>
                <div className="activity-content">
                  <p>
                    <strong>Registered for 5 courses</strong> this semester
                  </p>
                  <small>2 days ago</small>
                </div>
              </div>
              <div className="activity-item">
                <div className="activity-dot"></div>
                <div className="activity-content">
                  <p>
                    <strong>Grades posted</strong> for Programming 101
                  </p>
                  <small>1 week ago</small>
                </div>
              </div>
              <div className="activity-item">
                <div className="activity-dot"></div>
                <div className="activity-content">
                  <p>
                    <strong>Profile updated</strong> successfully
                  </p>
                  <small>2 weeks ago</small>
                </div>
              </div>
            </div>
          </div>

          {/* Semester Overview */}
          <div className="sd-section">
            <h2>Semester Overview</h2>
            <div className="sd-semester-info">
              <div className="semester-detail">
                <label>Academic Year:</label>
                <span>2023-2024</span>
              </div>
              <div className="semester-detail">
                <label>Semester:</label>
                <span>Spring (Semester 2)</span>
              </div>
              <div className="semester-detail">
                <label>Status:</label>
                <span className="status-active">Active</span>
              </div>
              <div className="semester-detail">
                <label>Enrollment Status:</label>
                <span className="status-enrolled">Fully Enrolled</span>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default StudentDashboard;
