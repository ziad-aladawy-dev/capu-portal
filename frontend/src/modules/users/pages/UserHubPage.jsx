import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  ArrowLeft, User, Edit3, Key, Trash2, XCircle, CheckCircle, BookOpen, Shield, Receipt, GraduationCap, Info,
} from "lucide-react";
import userService from "../services/userService";
import LoadingSpinner from "../components/LoadingSpinner";
import ErrorMessage from "../components/ErrorMessage";
import { useToast } from "../../../core/components/Toast";
import { useStickySelection } from "../../../core/contexts/StickySelectionContext";
import UserProfileTab from "../components/tabs/UserProfileTab";
import UserPermissionsTab from "../components/tabs/UserPermissionsTab";
import UserFinancialsTab from "../components/tabs/UserFinancialsTab";
import UserCoursesTab from "../components/tabs/UserCoursesTab";
import "../styles/UserDetails.css";

const TAB_CONFIG = [
  { id: "profile", label: "Profile & Info", icon: Info, userTypes: ["student", "staff"] },
  { id: "permissions", label: "Permissions & Roles", icon: Shield, userTypes: ["staff", "student"] },
  { id: "financials", label: "Financials", icon: Receipt, userTypes: ["student"] },
  { id: "courses", label: "Courses", icon: GraduationCap, userTypes: ["student"] },
];

function UserHubPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const { selected, select } = useStickySelection();

  const [user, setUser] = useState(null);
  const [userType, setUserType] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("profile");

  const loadUserData = useCallback(async () => {
    setLoading(true);
    setError(null);

    // Try sticky selection first to determine user type
    if (selected?.id === id && selected?.type) {
      const type = selected.type === "staff" ? "staff" : "student";
      setUserType(type);
      try {
        const data = type === "student"
          ? await userService.getStudentById(id)
          : await userService.getStaffById(id);
        setUser(data);
        setLoading(false);
        return;
      } catch {}
    }

    // Fallback: try student, then staff
    try {
      const data = await userService.getStudentById(id);
      setUserType("student");
      setUser(data);
      // Update sticky selection
      if (selected?.id !== id) {
        select({ id, name: data.name, code: data.studentCode, type: "student" });
      }
    } catch {
      try {
        const data = await userService.getStaffById(id);
        setUserType("staff");
        setUser(data);
        if (selected?.id !== id) {
          select({ id, name: data.name, code: data.employeeCode, type: "staff" });
        }
      } catch (err) {
        setError(err.message || "User not found");
      }
    } finally {
      setLoading(false);
    }
  }, [id, selected, select]);

  useEffect(() => {
    loadUserData();
  }, [loadUserData]);

  const handleToggleActive = async () => {
    if (!user) return;
    try {
      if (user.isActive) {
        await userService.deactivateUser(id, userType === "student" ? "Student" : "Staff", "Deactivated from user hub");
      } else {
        await userService.activateUser(id, userType === "student" ? "Student" : "Staff");
      }
      const updatedUser = await (userType === "student" ? userService.getStudentById(id) : userService.getStaffById(id));
      setUser(updatedUser);
      addToast(`User ${user.isActive ? "deactivated" : "activated"} successfully`, "success");
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  const handleSoftDelete = async () => {
    if (!window.confirm("Are you sure you want to delete this user?")) return;
    try {
      if (userType === "student") {
        await userService.deleteStudent(id);
      } else {
        await userService.deleteStaff(id);
      }
      addToast("User deleted successfully", "success");
      navigate("/admin/staff");
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  const isPasswordExpired = user?.passwordStatus === "Expired";

  if (loading) return <LoadingSpinner fullPage message="Loading user details..." />;
  if (error || !user) return <ErrorMessage message={error || "User not found"} />;

  const availableTabs = TAB_CONFIG.filter((t) => t.userTypes.includes(userType));

  return (
    <div className="user-details-layout">
      <div className="page-content">
        {/* Left column — Profile card */}
        <div className="left-column animated-fade">
          <div className="profile-card">
            <div className="avatar-shell">
              <div className="avatar-main">{user.name?.charAt(0) || "U"}</div>
            </div>
            <h1 className="profile-name">{user.name}</h1>
            <p className="profile-email">{user.email}</p>
            <div className="profile-badges">
              <span className={`role-badge ${userType === "student" ? "role-student" : "role-professor"}`}>
                {userType === "student" ? "Student" : "Staff"}
              </span>
              <span className={`status-badge ${user.isActive ? "status-active" : "status-inactive"}`}>
                <span className="status-dot"></span>
                {user.isActive ? "Active" : "Inactive"}
              </span>
              <span className={`password-badge ${isPasswordExpired ? "password-expired" : "password-valid"}`}>
                {isPasswordExpired ? "Password Expired" : "Valid Password"}
              </span>
            </div>
            <div className="mini-stats">
              <div className="mini-stat">
                <span>User Type</span>
                <strong>{userType === "student" ? "Student" : "Staff"}</strong>
              </div>
              <div className="mini-stat">
                <span>Member Since</span>
                <strong>{user.createdAt ? new Date(user.createdAt).toLocaleDateString("en-US", { year: "numeric", month: "short" }) : "—"}</strong>
              </div>
            </div>
          </div>
        </div>

        {/* Right column — Tabbed hub */}
        <div className="right-column animated-fade delay-2">
          {/* Tab bar */}
          <div className="tabs-container">
            <div className="tabs-row">
              {availableTabs.map((tab) => {
                const Icon = tab.icon;
                return (
                  <button
                    key={tab.id}
                    className={`tab-item ${activeTab === tab.id ? "active" : ""}`}
                    onClick={() => setActiveTab(tab.id)}
                  >
                    <Icon size={13} />
                    {tab.label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Tab content */}
          <div className="tab-content-card tab-switch-animate">
            {activeTab === "profile" && <UserProfileTab user={user} userType={userType} />}
            {activeTab === "permissions" && <UserPermissionsTab userId={id} userType={userType} />}
            {activeTab === "financials" && <UserFinancialsTab userId={id} userType={userType} />}
            {activeTab === "courses" && <UserCoursesTab userId={id} userType={userType} />}
          </div>

          {/* Actions panel */}
          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button
                className="bottom-action-btn gold"
                onClick={() => navigate(userType === "student" ? `/admin/users/edit-student/${id}` : `/admin/users/edit-staff/${id}`)}
              >
                <Edit3 size={18} /> Edit User
              </button>
              <button className="bottom-action-btn soft-gold" onClick={() => addToast("Password reset is not yet available", "info")}>
                <Key size={18} /> Reset Password
              </button>
              {userType === "student" && (
                <button
                  className="bottom-action-btn soft-gold"
                  onClick={() => navigate(`/admin/students/${id}/profile-records`)}
                >
                  <BookOpen size={18} /> Profile Records
                </button>
              )}
              <span className="action-separator"></span>
              <button
                className={`bottom-action-btn ${user.isActive ? "soft-red" : "soft-green"}`}
                onClick={handleToggleActive}
              >
                {user.isActive ? <XCircle size={18} /> : <CheckCircle size={18} />}
                {user.isActive ? "Deactivate" : "Activate"}
              </button>
              <button className="bottom-action-btn soft-red" onClick={handleSoftDelete}>
                <Trash2 size={18} /> Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default UserHubPage;
