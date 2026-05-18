import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, User, Mail, Phone, Calendar, Shield, BookOpen, Building2,
  Edit3, Key, Trash2, RefreshCw, Award, Hash, AtSign, CheckCircle, XCircle, Briefcase
} from 'lucide-react';
import userService from '../services/userService';
import LoadingSpinner from '../components/LoadingSpinner';
import ErrorMessage from '../components/ErrorMessage';
import '../styles/UserDetails.css';

const UserDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();

  const [user, setUser] = useState(null);
  const [userType, setUserType] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState('personal');

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

  const isPasswordExpired = user?.passwordStatus === 'Expired';

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
        if (userType === 'student') {
          await userService.deleteStudent(id);
        } else {
          await userService.deleteStaff(id);
        }
        alert('User deleted successfully');
        navigate('/admin/users');
      } catch (err) {
        alert(err.message);
      }
    }
  };

  if (loading) return <LoadingSpinner fullPage message="Loading user details..." />;
  if (error || !user) return <ErrorMessage message={error || 'User not found'} />;

  const InfoCard = ({ icon, label, value }) => (
    <div className="detail-card">
      <div className="detail-icon">{icon}</div>
      <div className="detail-content">
        <span className="detail-label">{label}</span>
        <h4 className="detail-value">{value || 'Not specified'}</h4>
      </div>
    </div>
  );

  return (
    <div className="user-details-layout">
      <div className="page-content">
        <div className="left-column animated-fade">
          <div className="profile-card">
            <div className="avatar-shell">
              <div className="avatar-main">{user.name?.charAt(0) || 'U'}</div>
            </div>
            <h1 className="profile-name">{user.name}</h1>
            <p className="profile-email">{user.email}</p>
            <div className="profile-badges">
              <span className={`role-badge ${userType === 'student' ? 'role-student' : 'role-professor'}`}>
                {userType === 'student' ? 'Student' : 'Staff'}
              </span>
              <span className={`status-badge ${user.isActive ? 'status-active' : 'status-inactive'}`}>
                <span className="status-dot"></span>
                {user.isActive ? 'Active' : 'Inactive'}
              </span>
              <span className={`password-badge ${isPasswordExpired ? 'password-expired' : 'password-valid'}`}>
                {isPasswordExpired ? 'Password Expired' : 'Valid Password'}
              </span>
            </div>
            <div className="mini-stats">
              <div className="mini-stat">
                <span>User Type</span>
                <strong>{userType === 'student' ? 'Student' : 'Staff'}</strong>
              </div>
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
              <button className={`tab-item ${activeTab === 'personal' ? 'active' : ''}`} onClick={() => setActiveTab('personal')}>
                Personal Information
              </button>
              {userType === 'student' && (
                <button className={`tab-item ${activeTab === 'academic' ? 'active' : ''}`} onClick={() => setActiveTab('academic')}>
                  Academic Information
                </button>
              )}
              {userType === 'staff' && (
                <button className={`tab-item ${activeTab === 'employment' ? 'active' : ''}`} onClick={() => setActiveTab('employment')}>
                  Employment Information
                </button>
              )}
              <button className={`tab-item ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>
                Account Details
              </button>
            </div>
          </div>

          <div className="tab-content-card tab-switch-animate">
            {activeTab === 'personal' && (
              <div className="details-grid">
                <InfoCard icon={<Hash size={19} />} label="National ID" value={user.nationalId} />
                <InfoCard icon={<User size={19} />} label="Full Name" value={user.name} />
                <InfoCard icon={<Calendar size={19} />} label="Date of Birth" value={formatDate(user.birthDate)} />
                <InfoCard icon={<Phone size={19} />} label="Phone" value={user.phoneNumber} />
                <InfoCard icon={<Mail size={19} />} label="Email" value={user.email} />
              </div>
            )}

            {activeTab === 'academic' && userType === 'student' && (
              <div className="details-grid">
                <InfoCard icon={<Award size={19} />} label="Student Code" value={user.studentCode} />
                <InfoCard icon={<Building2 size={19} />} label="Faculty" value={user.facultyName} />
                <InfoCard icon={<BookOpen size={19} />} label="Program" value={user.programName} />
                <InfoCard icon={<Award size={19} />} label="Level" value={user.levelName} />
                <InfoCard icon={<Shield size={19} />} label="Academic Status" value={user.status || 'Active'} />
              </div>
            )}

            {activeTab === 'employment' && userType === 'staff' && (
              <div className="details-grid">
                <InfoCard icon={<Award size={19} />} label="Employee Code" value={user.employeeCode} />
                <InfoCard icon={<Shield size={19} />} label="Role" value={user.role} />
                <InfoCard icon={<Briefcase size={19} />} label="Job Title" value={user.jobTitle} />
                <InfoCard icon={<Building2 size={19} />} label="Faculty / Department" value={user.facultyName || user.structureNodeName} />
              </div>
            )}

            {activeTab === 'account' && (
              <div className="details-grid">
                <InfoCard icon={<Calendar size={19} />} label="Account Created" value={formatDateTime(user.createdAt)} />
                <InfoCard icon={<Calendar size={19} />} label="Last Updated" value={formatDateTime(user.updatedAt)} />
                <InfoCard icon={<Key size={19} />} label="Password Status" value={user.passwordStatus || (isPasswordExpired ? 'Expired' : 'Valid')} />
                <InfoCard icon={<CheckCircle size={19} />} label="Account Status" value={user.isActive ? 'Active' : 'Inactive'} />
              </div>
            )}
          </div>

          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button className="bottom-action-btn gold" onClick={() => navigate(userType === 'student' ? `/admin/users/edit-student/${id}` : `/admin/users/edit-staff/${id}`)}>
                <Edit3 size={18} /> Edit User
              </button>
              <span className="action-separator"></span>
              <button className={`bottom-action-btn ${user.isActive ? 'soft-red' : 'soft-green'}`} onClick={handleToggleActive}>
                {user.isActive ? <XCircle size={18} /> : <CheckCircle size={18} />}
                {user.isActive ? 'Deactivate' : 'Activate'}
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
};

export default UserDetails;