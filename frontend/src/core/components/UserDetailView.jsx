import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowLeft, Mail, Building2, Calendar, Shield, Phone, BadgeCheck, ExternalLink } from "lucide-react";
import * as studentService from "../services/studentService";
import * as staffService from "../services/staffService";
import "./userDetailView.css";

function UserDetailView({ userId, userType, onBack }) {
  const navigate = useNavigate();
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = userType === "staff"
          ? await staffService.fetchStaffById(userId)
          : await studentService.fetchStudentById(userId);
        if (!cancelled) setUser(data);
      } catch (err) {
        if (!cancelled) setError(err.message || "Failed to load user");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [userId, userType]);

  if (loading) {
    return (
      <div className="user-detail-loading">
        <div className="user-detail-spinner" />
        <p>Loading user details...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="user-detail-error">
        <p>Error: {error}</p>
        <button className="user-detail-back-btn" onClick={onBack}>← Back to Directory</button>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="user-detail-error">
        <p>User not found</p>
        <button className="user-detail-back-btn" onClick={onBack}>← Back to Directory</button>
      </div>
    );
  }

  const code = userType === "staff" ? user.employeeCode : user.studentCode;
  const roleLabel = userType === "staff" ? (user.role || "Staff") : (user.levelName || "Student");

  return (
    <div className="user-detail-container">
      <div className="user-detail-topbar">
        <button className="user-detail-back-btn" onClick={onBack}>
          <ArrowLeft size={14} /> Back to Directory
        </button>
        <button
          className="user-detail-full-profile-btn"
          onClick={() => navigate(`/admin/users/${user.id}`)}
        >
          <ExternalLink size={14} /> Full Profile
        </button>
      </div>

      <div className="user-detail-card">
        <div className="user-detail-avatar-section">
          <div className={`user-detail-avatar type-${userType}`}>
            {user.name?.charAt(0).toUpperCase()}
          </div>
          <div className="user-detail-heading">
            <h2>{user.name}</h2>
            <div className="user-detail-badges">
              <span className={`user-detail-badge type-${userType}`}>
                {userType === "staff" ? "Staff" : "Student"}
              </span>
              <span className={`user-detail-badge status-${user.isActive ? "active" : "inactive"}`}>
                {user.isActive ? "Active" : "Inactive"}
              </span>
            </div>
          </div>
        </div>

        <div className="user-detail-info-grid">
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><BadgeCheck size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">ID / Code</span>
              <span className="user-detail-info-value">{code || "—"}</span>
            </div>
          </div>
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><Mail size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">Email</span>
              <span className="user-detail-info-value">{user.email || "—"}</span>
            </div>
          </div>
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><Shield size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">{userType === "staff" ? "Role" : "Level"}</span>
              <span className="user-detail-info-value">{roleLabel}</span>
            </div>
          </div>
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><Building2 size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">Department</span>
              <span className="user-detail-info-value">{user.departmentName || user.facultyName || "—"}</span>
            </div>
          </div>
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><Calendar size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">Joined</span>
              <span className="user-detail-info-value">
                {user.createdAt ? new Date(user.createdAt).toLocaleDateString() : "—"}
              </span>
            </div>
          </div>
          <div className="user-detail-info-item">
            <div className="user-detail-info-icon"><Phone size={14} /></div>
            <div className="user-detail-info-text">
              <span className="user-detail-info-label">Phone</span>
              <span className="user-detail-info-value">{user.phoneNumber || user.phone || "—"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default UserDetailView;
