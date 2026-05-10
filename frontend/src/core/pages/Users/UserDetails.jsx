import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, User, Mail, Phone, Calendar, Shield, BookOpen, Building2,
  Edit3, Key, Trash2, RefreshCw, Award, Hash, AtSign, CheckCircle, XCircle, Briefcase
} from 'lucide-react';
import userService from '../../api/userService';
import LoadingSpinner from '../../components/UI/LoadingSpinner';
import ErrorMessage from '../../components/UI/ErrorMessage';
import Sidebar from '../../layouts/Sidebar/Sidebar';
import Navbar from '../../layouts/Navbar/Navbar';
import './UserDetails.css';

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
        await userService.softDeleteUser(id, 'Deleted from details page');
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

    return null;
  };

  return (
    <div className="user-details-layout">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <Navbar onMenuClick={() => setSidebarOpen(true)} />

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
              <button className={`tab-item ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>Account Details</button>
            </div>
          </div>

          <div key={activeTab} className="tab-content-card tab-switch-animate">
            {renderTabContent()}
          </div>

          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button className="bottom-action-btn gold" onClick={() => navigate(userType === 'student' ? `/users/edit-student/${id}` : `/users/edit-staff/${id}`)}>
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