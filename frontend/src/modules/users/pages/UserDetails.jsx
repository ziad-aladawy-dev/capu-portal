import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, User, Mail, Phone, Calendar, Shield, BookOpen, Building2,
  Edit3, Key, Trash2, RefreshCw, Award, Hash, AtSign, CheckCircle, XCircle, Briefcase,
  Plus, X, AlertTriangle
} from 'lucide-react';
import userService from '../services/userService';
import * as permissionService from '../../../core/services/permissionService';
import LoadingSpinner from '../components/LoadingSpinner';
import ErrorMessage from '../components/ErrorMessage';
import '../styles/userDetails.css';

const UserDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();

  const [user, setUser] = useState(null);
  const [userType, setUserType] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState('personal');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [showResetConfirm, setShowResetConfirm] = useState(false);

  const [roles, setRoles] = useState([]);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [savingRole, setSavingRole] = useState(false);
  const [roleSaved, setRoleSaved] = useState(false);

  const [overrides, setOverrides] = useState([]);
  const [newOverrideResource, setNewOverrideResource] = useState("");
  const [newOverrideLevel, setNewOverrideLevel] = useState(5);
  const [overrideType, setOverrideType] = useState(1);

  const [permAssignment, setPermAssignment] = useState(null);
  const [permLoading, setPermLoading] = useState(false);
  const [permSaving, setPermSaving] = useState(false);
  const [permError, setPermError] = useState(null);
  const [permSaved, setPermSaved] = useState(false);

  useEffect(() => {
    const loadUserData = async () => {
      setLoading(true);
      try {
        let userData;
        try {
          userData = await userService.getStudentById(id);
          setUserType('student');
        } catch (e) {
          userData = await userService.getStaffById(id);
          setUserType('staff');
        }
        setUser(userData);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    loadUserData();
  }, [id]);

  useEffect(() => {
    if (userType === 'staff') {
      userService.getRoles().then(setRoles);
      setSelectedRoleId(user?.staffRoleId || "");
    }
  }, [userType, user?.staffRoleId]);

  useEffect(() => {
    if (activeTab !== 'roles' || userType !== 'staff' || !id) return;
    let cancelled = false;
    const load = async () => {
      setPermLoading(true);
      setPermError(null);
      try {
        const assignment = await permissionService.fetchPermissionAssignment({ userId: id });
        if (cancelled) return;
        setPermAssignment(assignment);
        const mapped = (assignment?.permissionOverrides || []).map((o, i) => ({
          _id: `ov-${i}`,
          resource: o.resource,
          level: o.level,
          type: o.type ?? 1,
        }));
        setOverrides(mapped);
      } catch (err) {
        if (!cancelled) setPermError(err.message || 'Failed to load permission assignment');
      } finally {
        if (!cancelled) setPermLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [activeTab, userType, id]);

  const getUserTypeDisplay = () => {
    if (!user) return '';
    return userType === 'student' ? 'Student' : 'Staff';
  };

  const getUserTypeClass = () => {
    if (userType === 'student') return 'role-student';
    return 'role-professor';
  };

  const isPasswordExpired = user?.isPasswordExpired ||
    (user?.passwordExpiryDate && new Date(user.passwordExpiryDate) < new Date());

  const formatDate = (date) => {
    if (!date) return 'Not specified';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric', month: 'long', day: 'numeric'
    });
  };

  const formatDateTime = (date) => {
    if (!date) return 'Never';
    return new Date(date).toLocaleString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  };

  const handleResetPassword = async () => {
    const newPassword = prompt('Enter new password (leave empty for auto-generated):');
    if (newPassword !== null) {
      try {
        await userService.resetUserPassword(id, userType === 'student' ? 'Student' : 'Staff', newPassword || null);
        alert('Password reset successfully');
        setShowResetConfirm(false);
      } catch (err) {
        alert(err.message);
      }
    }
  };

  const handleToggleActive = async () => {
    try {
      if (user.isActive) {
        await userService.deactivateUser(id, userType === 'student' ? 'Student' : 'Staff', 'Deactivated from details page');
      } else {
        await userService.activateUser(id, userType === 'student' ? 'Student' : 'Staff');
      }
      const updatedUser = await (userType === 'student' ? userService.getStudentById(id) : userService.getStaffById(id));
      setUser(updatedUser);
      alert(`User ${user.isActive ? 'deactivated' : 'activated'} successfully`);
    } catch (err) {
      alert(err.message);
    }
  };

  const handleSoftDelete = async () => {
    if (window.confirm('Are you sure you want to delete this user?')) {
      try {
        await userService.softDeleteUser(id, 'Deleted from details page', userType);
        const updatedUser = await (userType === 'student' ? userService.getStudentById(id) : userService.getStaffById(id));
        setUser(updatedUser);
        alert('User deleted successfully');
      } catch (err) {
        alert(err.message);
      }
    }
  };

  const handleRestore = async () => {
    try {
      await userService.restoreUser(id);
      const updatedUser = await (userType === 'student' ? userService.getStudentById(id) : userService.getStaffById(id));
      setUser(updatedUser);
      alert('User restored successfully');
    } catch (err) {
      alert(err.message);
    }
  };

  const handleRoleChange = async (newRoleId) => {
    const newRole = roles.find((r) => r.id === newRoleId);
    if (!newRole) return;
    setSavingRole(true);
    try {
      await userService.updateStaff(user.id, { staffRoleId: newRoleId, staffRoleName: newRole.name });
      setUser((prev) => ({ ...prev, staffRoleId: newRoleId, staffRoleName: newRole.name }));
      setSelectedRoleId(newRoleId);
      setRoleSaved(true);
      setTimeout(() => setRoleSaved(false), 2000);
    } catch (err) {
      alert(err.message);
    } finally {
      setSavingRole(false);
    }
  };

  const handleAddOverride = () => {
    if (!newOverrideResource.trim()) return;
    setOverrides((prev) => [
      ...prev,
      { _id: `ov-${Date.now()}`, resource: newOverrideResource.trim(), level: newOverrideLevel, type: overrideType },
    ]);
    setNewOverrideResource("");
    setNewOverrideLevel(5);
    setOverrideType(1);
  };

  const handleRemoveOverride = (overrideId) => {
    setOverrides((prev) => prev.filter((o) => o._id !== overrideId));
  };

  const handleSavePermOverrides = async () => {
    setPermSaving(true);
    setPermError(null);
    try {
      const currentOverrides = permAssignment?.permissionOverrides || [];
      const keyFn = (o) => o.resource;
      const currentKeys = new Set(currentOverrides.map(keyFn));
      const newKeys = new Set(overrides.map(keyFn));

      const permissionsToAdd = overrides
        .filter((o) => !currentKeys.has(keyFn(o)))
        .map((o) => ({ resource: o.resource, level: o.level, type: o.type }));

      const permissionsToRemove = currentOverrides
        .filter((o) => !newKeys.has(keyFn(o)))
        .map((o) => ({ resource: o.resource, level: o.level, type: o.type }));

      await permissionService.updatePermissionAssignment({
        userId: id,
        rolesToAdd: [],
        rolesToRemove: [],
        permissionsToAdd,
        permissionsToRemove,
        structuralScope: permAssignment?.structuralScope || { structureNodeId: null },
        temporalScope: permAssignment?.temporalScope || { academicYearId: null, semesterId: null, alwaysActive: true },
      });

      setPermSaved(true);
      setPermAssignment((prev) => ({
        ...prev,
        permissionOverrides: overrides.map((o) => ({ resource: o.resource, level: o.level, type: o.type })),
      }));
      setTimeout(() => setPermSaved(false), 2000);
    } catch (err) {
      setPermError(err.message || 'Failed to save');
    } finally {
      setPermSaving(false);
    }
  };

  const RESOURCE_OPTIONS = [
    { value: "dashboard.dashboard.view", label: "Dashboard View" },
    { value: "users.users.view", label: "Users View" },
    { value: "users.users.insert", label: "Users Create" },
    { value: "users.users.editclose", label: "Users Edit" },
    { value: "users.users.delete", label: "Users Delete" },
    { value: "structure.structure.view", label: "Structure View" },
    { value: "structure.structure.editclose", label: "Structure Edit" },
    { value: "staff.directory.view", label: "Staff Directory View" },
    { value: "students.directory.view", label: "Students Directory View" },
  ];

  const LEVEL_LABELS = ["None", "View", "Insert", "Edit", "Open", "Delete"];

  if (loading) return <LoadingSpinner fullPage message="Loading user details..." />;
  if (error || !user) return <ErrorMessage message={error || 'User not found'} onRetry={() => window.location.reload()} />;

  const InfoCard = ({ icon, label, value, full }) => (
    <div className={`detail-card ${full ? 'full' : ''}`}>
      <div className="detail-icon">{icon}</div>
      <div className="detail-content">
        <span className="detail-label">{label}</span>
        <h4 className="detail-value">{value || 'Not specified'}</h4>
      </div>
    </div>
  );

  const renderTabContent = () => {
    if (activeTab === 'personal') {
      return (
        <div className="details-grid">
          <InfoCard icon={<Hash size={19} />} label="National ID" value={user.nationalId} />
          <InfoCard icon={<User size={19} />} label="Full Name (Arabic)" value={user.fullNameAr} />
          <InfoCard icon={<User size={19} />} label="Full Name (English)" value={user.fullNameEn} />
          <InfoCard icon={<Calendar size={19} />} label="Date of Birth" value={user.dateOfBirth ? formatDate(user.dateOfBirth) : 'Not specified'} />
          <InfoCard icon={<Phone size={19} />} label="Phone" value={user.phone || 'Not specified'} />
          <InfoCard icon={<Mail size={19} />} label="Email" value={user.email} />
        </div>
      );
    }

    if (activeTab === 'academic' && userType === 'student') {
      return (
        <div className="details-grid">
          <InfoCard icon={<Award size={19} />} label="Student Code" value={user.studentCode} />
          <InfoCard icon={<AtSign size={19} />} label="Email" value={user.email} />
          <InfoCard icon={<Building2 size={19} />} label="Faculty" value={user.facultyName || 'Not specified'} />
          <InfoCard icon={<BookOpen size={19} />} label="Program" value={user.programName || 'Not specified'} />
          <InfoCard icon={<Award size={19} />} label="Level" value={user.levelName || 'Not specified'} />
          <InfoCard icon={<Shield size={19} />} label="Status" value={user.status || 'Active'} />
          <InfoCard icon={<Award size={19} />} label="GPA" value={user.gpa || '0.0'} />
          <InfoCard icon={<Calendar size={19} />} label="Enrollment Date" value={formatDate(user.enrollmentDate)} />
        </div>
      );
    }

    if (activeTab === 'employment' && userType === 'staff') {
      return (
        <div className="details-grid">
          <InfoCard icon={<Award size={19} />} label="Staff Code" value={user.staffCode} />
          <InfoCard icon={<Shield size={19} />} label="Role" value={user.staffRoleName || 'Not specified'} />
          <InfoCard icon={<Building2 size={19} />} label="University" value={user.universityName || 'Not specified'} />
          <InfoCard icon={<Briefcase size={19} />} label="Position" value={user.position || 'Not specified'} />
        </div>
      );
    }

    if (activeTab === 'account') {
      return (
        <div className="details-grid">
          <InfoCard icon={<Calendar size={19} />} label="Account Created" value={formatDateTime(user.createdAt)} />
          <InfoCard icon={<Calendar size={19} />} label="Last Updated" value={formatDateTime(user.updatedAt)} />
          <InfoCard icon={<Calendar size={19} />} label="Last Login" value={formatDateTime(user.lastLoginAt)} />
          <InfoCard icon={<Key size={19} />} label="Password Status" value={isPasswordExpired ? 'Expired' : 'Valid'} />
          <InfoCard icon={<CheckCircle size={19} />} label="Account Status" value={`${user.isActive ? 'Active' : 'Inactive'}${user.isDeleted ? ' (Deleted)' : ''}`} />
        </div>
      );
    }

    if (activeTab === 'roles' && userType === 'staff') {
      return (
        <div className="roles-permissions-tab">
          {/* Role Assignment */}
          <div className="rp-section">
            <h3 className="rp-section-title">
              <Shield size={16} />
              Role Assignment
            </h3>
            <p className="rp-section-desc">Assign a system role to this staff member. The role determines default permissions.</p>
            <div className="rp-role-row">
              <select
                className="rp-select"
                value={selectedRoleId}
                onChange={(e) => setSelectedRoleId(e.target.value)}
              >
                <option value="">No role assigned</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              <button
                className="rp-btn rp-btn-primary"
                onClick={() => handleRoleChange(selectedRoleId)}
                disabled={savingRole || !selectedRoleId || selectedRoleId === user.staffRoleId}
              >
                {savingRole ? "Saving…" : roleSaved ? "Saved!" : "Apply Role"}
              </button>
            </div>
            {user.staffRoleName && (
              <div className="rp-current-role">
                <Shield size={13} />
                Currently: <strong>{user.staffRoleName}</strong>
              </div>
            )}
          </div>

          {/* Permission Overrides */}
          <div className="rp-section">
            <h3 className="rp-section-title">
              <AlertTriangle size={16} />
              Permission Overrides
            </h3>
            <p className="rp-section-desc">Override specific permissions for this staff member, regardless of their role.</p>

            <div className="rp-override-form">
              <select
                className="rp-select"
                value={newOverrideResource}
                onChange={(e) => setNewOverrideResource(e.target.value)}
              >
                <option value="">Select resource…</option>
                {RESOURCE_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
              <select
                className="rp-select small"
                value={newOverrideLevel}
                onChange={(e) => setNewOverrideLevel(Number(e.target.value))}
              >
                {LEVEL_LABELS.map((label, i) => (
                  <option key={i} value={i}>{i} — {label}</option>
                ))}
              </select>
              <select
                className="rp-select small"
                value={overrideType}
                onChange={(e) => setOverrideType(Number(e.target.value))}
              >
                <option value={1}>Allow</option>
                <option value={2}>Deny</option>
              </select>
              <button
                className="rp-btn rp-btn-primary"
                onClick={handleAddOverride}
                disabled={!newOverrideResource}
              >
                <Plus size={13} />
                Add
              </button>
            </div>

            {permLoading ? (
              <p className="rp-empty">Loading permission overrides…</p>
            ) : overrides.length === 0 ? (
              <p className="rp-empty">No permission overrides configured.</p>
            ) : (
              <div className="rp-override-list">
                {overrides.map((ov) => (
                  <div key={ov._id} className="rp-override-item">
                    <div className="rp-override-info">
                      <strong>{ov.resource}</strong>
                      <span className="rp-override-level">{LEVEL_LABELS[ov.level]} (Level {ov.level})</span>
                      <span className={`rp-override-type ${ov.type === 2 ? "is-deny" : "is-allow"}`}>{ov.type === 2 ? "Deny" : "Allow"}</span>
                    </div>
                    <button className="rp-btn-icon" onClick={() => handleRemoveOverride(ov._id)}>
                      <X size={12} />
                    </button>
                  </div>
                ))}
              </div>
            )}

            {permError && <div className="rp-error">{permError}</div>}
            {permSaved && <div className="rp-success">Permission overrides saved successfully.</div>}
            <div className="rp-section-actions">
              <button
                className="rp-btn rp-btn-primary"
                onClick={handleSavePermOverrides}
                disabled={permSaving || permLoading}
              >
                {permSaving ? "Saving…" : permSaved ? "Saved!" : "Save Permission Overrides"}
              </button>
            </div>
          </div>
        </div>
      );
    }

    return null;
  };

  return (
    <div className="user-details-layout">

      <div className="page-content">
        <div className="left-column animated-fade">
          <div className="profile-card">
            <div className="avatar-shell">
              <div className="avatar-main">{user.fullNameAr?.charAt(0) || user.fullNameEn?.charAt(0) || 'U'}</div>
            </div>
            <h1 className="profile-name">{user.fullNameAr || user.fullNameEn}</h1>
            <p className="profile-email">{user.email}</p>

            <div className="profile-badges">
              <span className={`role-badge ${getUserTypeClass()}`}>{getUserTypeDisplay()}</span>
              <span className={`status-badge ${user.isActive ? 'status-active' : 'status-inactive'}`}>
                <span className="status-dot"></span>
                {user.isActive ? 'Active' : 'Inactive'}
              </span>
              {user.isDeleted && (
                <span className="status-badge status-deleted">
                  <span className="status-dot"></span>Deleted
                </span>
              )}
              <span className={`password-badge ${isPasswordExpired ? 'password-expired' : 'password-valid'}`}>
                {isPasswordExpired ? 'Password Expired' : 'Valid Password'}
              </span>
            </div>

            <div className="mini-stats">
              <div className="mini-stat">
                <span>User Type</span>
                <strong>{getUserTypeDisplay()}</strong>
              </div>
              {/* <div className="mini-stat">
                <span>Last Login</span>
                <strong>{formatDateTime(user.lastLoginAt)}</strong>
              </div> */}
              <div className="mini-stat">
                <span>Created At</span>
                <strong>{formatDate(user.createdAt)}</strong>
              </div>
            </div>
          </div>
        </div>

        <div className="right-column animated-fade delay-2">
          <div className="tabs-container">
            <div className="tabs-row">
              <button className={`tab-item ${activeTab === 'personal' ? 'active' : ''}`} onClick={() => setActiveTab('personal')}>Personal Information</button>
              {userType === 'student' && (
                <button className={`tab-item ${activeTab === 'academic' ? 'active' : ''}`} onClick={() => setActiveTab('academic')}>Academic Information</button>
              )}
              {userType === 'staff' && (
                <button className={`tab-item ${activeTab === 'employment' ? 'active' : ''}`} onClick={() => setActiveTab('employment')}>Employment Information</button>
              )}
              {userType === 'staff' && (
                <button className={`tab-item ${activeTab === 'roles' ? 'active' : ''}`} onClick={() => setActiveTab('roles')}>Roles & Permissions</button>
              )}
              <button className={`tab-item ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>Account Details</button>
            </div>
          </div>

          <div key={activeTab} className="tab-content-card tab-switch-animate">
            {renderTabContent()}
          </div>

          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button className="bottom-action-btn gold" onClick={() => navigate(userType === 'student' ? `/admin/users/edit-student/${id}` : `/admin/users/edit-staff/${id}`)}>
                <Edit3 size={18} /> Edit User
              </button>
              <button className="bottom-action-btn soft-gold" onClick={() => setShowResetConfirm(true)} disabled={user.isDeleted}>
                <Key size={18} /> Reset Password
              </button>
              <span className="action-separator"></span>
              <button className={`bottom-action-btn ${user.isActive ? 'soft-red' : 'soft-green'}`} onClick={handleToggleActive} disabled={user.isDeleted}>
                {user.isActive ? <XCircle size={18} /> : <CheckCircle size={18} />}
                {user.isActive ? 'Deactivate' : 'Activate'}
              </button>
              {!user.isDeleted ? (
                <button className="bottom-action-btn soft-red" onClick={handleSoftDelete}><Trash2 size={18} /> Delete</button>
              ) : (
                <button className="bottom-action-btn soft-green" onClick={handleRestore}><RefreshCw size={18} /> Restore</button>
              )}
            </div>
          </div>
        </div>
      </div>

      {showResetConfirm && (
        <>
          <div className="modal-overlay" onClick={() => setShowResetConfirm(false)} />
          <div className="confirm-modal">
            <h3>Reset Password</h3>
            <p>Are you sure you want to reset password for <span>"{user.fullNameEn || user.fullNameAr}"</span>?</p>
            <div className="modal-actions">
              <button className="cancel-btn" onClick={() => setShowResetConfirm(false)}>Cancel</button>
              <button className="confirm-btn" onClick={handleResetPassword}>Reset Password</button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default UserDetails;