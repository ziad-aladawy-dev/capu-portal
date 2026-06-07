import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ArrowLeft, User, Mail, Phone, Calendar, Shield, BookOpen, Building2,
  Edit3, Key, Trash2, RefreshCw, Award, Hash, AtSign, CheckCircle, XCircle, Briefcase
} from 'lucide-react';
import userService from '../services/userService';
import LoadingSpinner from '../components/LoadingSpinner';
import ErrorMessage from '../components/ErrorMessage';
import '../styles/UserDetails.css';

const UserDetails = () => {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const navigate = useNavigate();

  const [user, setUser] = useState(null);
  const [userType, setUserType] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState('personal');

  const getLocalizedUserName = (userObj) => {
    if (!userObj || !userObj.name) return '';
    try {
      const parsed = JSON.parse(userObj.name);
      const lang = i18n.language === 'ar' ? 'ar' : 'en';
      return parsed[lang] || parsed.ar || parsed.en || userObj.name;
    } catch {
      return userObj.name;
    }
  };

  const getAvatarInitial = () => {
    if (!user) return 'U';
    const localizedName = getLocalizedUserName(user);
    return localizedName.charAt(0).toUpperCase();
  };

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
    if (!date) return t('not_specified');
    const locale = i18n.language === 'ar' ? 'ar-EG' : 'en-US';
    return new Date(date).toLocaleDateString(locale, {
      year: 'numeric', month: 'long', day: 'numeric'
    });
  };

  const formatDateTime = (date) => {
    if (!date) return t('never');
    const locale = i18n.language === 'ar' ? 'ar-EG' : 'en-US';
    return new Date(date).toLocaleString(locale, {
      year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  };

  const isPasswordExpired = user?.passwordStatus === 'Expired';

  const handleToggleActive = async () => {
    try {
      if (user.isActive) {
        await userService.deactivateUser(id, userType === 'student' ? 'Student' : 'Staff', t('deactivated_from_details'));
      } else {
        await userService.activateUser(id, userType === 'student' ? 'Student' : 'Staff');
      }
      const updatedUser = await (userType === 'student' ? userService.getStudentById(id) : userService.getStaffById(id));
      setUser(updatedUser);
      alert(user.isActive ? t('user_deactivated_success') : t('user_activated_success'));
    } catch (err) {
      alert(err.message);
    }
  };

  const handleSoftDelete = async () => {
    if (window.confirm(t('confirm_delete_user'))) {
      try {
        if (userType === 'student') {
          await userService.deleteStudent(id);
        } else {
          await userService.deleteStaff(id);
        }
        alert(t('user_deleted_success'));
        navigate('/admin/users');
      } catch (err) {
        alert(err.message);
      }
    }
  };

  if (loading) return <LoadingSpinner fullPage message={t('loading_user_details')} />;
  if (error || !user) return <ErrorMessage message={error || t('user_not_found')} />;

  const InfoCard = ({ icon, label, value }) => (
    <div className="detail-card">
      <div className="detail-icon">{icon}</div>
      <div className="detail-content">
        <span className="detail-label">{label}</span>
        <h4 className="detail-value">{value || t('not_specified')}</h4>
      </div>
    </div>
  );

  const localizedName = getLocalizedUserName(user);
  const userRoleLabel = userType === 'student' ? t('student') : t('staff');
  const statusLabel = user.isActive ? t('active') : t('inactive');
  const passwordStatusLabel = isPasswordExpired ? t('expired') : t('valid');
  const passwordBadgeClass = isPasswordExpired ? 'password-expired' : 'password-valid';

  return (
    <div className="user-details-layout">
      <div className="page-content">
        <div className="left-column animated-fade">
          <div className="profile-card">
            <div className="avatar-shell">
              <div className="avatar-main">{getAvatarInitial()}</div>
            </div>
            <h1 className="profile-name">{localizedName}</h1>
            <p className="profile-email">{user.email}</p>
            <div className="profile-badges">
              <span className={`role-badge ${userType === 'student' ? 'role-student' : 'role-professor'}`}>
                {userRoleLabel}
              </span>
              <span className={`status-badge ${user.isActive ? 'status-active' : 'status-inactive'}`}>
                <span className="status-dot"></span>
                {statusLabel}
              </span>
              <span className={`password-badge ${passwordBadgeClass}`}>
                {passwordStatusLabel}
              </span>
            </div>
            <div className="mini-stats">
              <div className="mini-stat">
                <span>{t('user_type')}</span>
                <strong>{userRoleLabel}</strong>
              </div>
              <div className="mini-stat">
                <span>{t('created_at')}</span>
                <strong>{formatDate(user.createdAt)}</strong>
              </div>
            </div>
          </div>
        </div>

        <div className="right-column animated-fade delay-2">
          <div className="tabs-container">
            <div className="tabs-row">
              <button className={`tab-item ${activeTab === 'personal' ? 'active' : ''}`} onClick={() => setActiveTab('personal')}>
                {t('personal_information')}
              </button>
              {userType === 'student' && (
                <button className={`tab-item ${activeTab === 'academic' ? 'active' : ''}`} onClick={() => setActiveTab('academic')}>
                  {t('academic_information')}
                </button>
              )}
              {userType === 'staff' && (
                <button className={`tab-item ${activeTab === 'employment' ? 'active' : ''}`} onClick={() => setActiveTab('employment')}>
                  {t('employment_information')}
                </button>
              )}
              <button className={`tab-item ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>
                {t('account_details')}
              </button>
            </div>
          </div>

          <div className="tab-content-card tab-switch-animate">
            {activeTab === 'personal' && (
              <div className="details-grid">
                <InfoCard icon={<Hash size={19} />} label={t('national_id')} value={user.nationalId} />
                <InfoCard icon={<User size={19} />} label={t('full_name')} value={localizedName} />
                <InfoCard icon={<Calendar size={19} />} label={t('date_of_birth')} value={formatDate(user.birthDate)} />
                <InfoCard icon={<Phone size={19} />} label={t('phone')} value={user.phoneNumber} />
                <InfoCard icon={<Mail size={19} />} label={t('email')} value={user.email} />
              </div>
            )}

            {activeTab === 'academic' && userType === 'student' && (
              <div className="details-grid">
                <InfoCard icon={<Award size={19} />} label={t('student_code')} value={user.studentCode} />
                <InfoCard icon={<Building2 size={19} />} label={t('faculty')} value={user.facultyName} />
                <InfoCard icon={<BookOpen size={19} />} label={t('program')} value={user.programName} />
                <InfoCard icon={<Award size={19} />} label={t('level')} value={user.levelName} />
                <InfoCard icon={<Shield size={19} />} label={t('academic_status')} value={user.status || t('active')} />
              </div>
            )}

            {activeTab === 'employment' && userType === 'staff' && (
              <div className="details-grid">
                <InfoCard icon={<Award size={19} />} label={t('employee_code')} value={user.employeeCode} />
                <InfoCard icon={<Shield size={19} />} label={t('role')} value={user.role} />
                <InfoCard icon={<Briefcase size={19} />} label={t('job_title')} value={user.jobTitle} />
                <InfoCard icon={<Building2 size={19} />} label={t('faculty_department')} value={user.facultyName || user.structureNodeName} />
              </div>
            )}

            {activeTab === 'account' && (
              <div className="details-grid">
                <InfoCard icon={<Calendar size={19} />} label={t('account_created')} value={formatDateTime(user.createdAt)} />
                <InfoCard icon={<Calendar size={19} />} label={t('last_updated')} value={formatDateTime(user.updatedAt)} />
                <InfoCard icon={<Key size={19} />} label={t('password_status')} value={user.passwordStatus || (isPasswordExpired ? t('expired') : t('valid'))} />
                <InfoCard icon={<CheckCircle size={19} />} label={t('account_status')} value={user.isActive ? t('active') : t('inactive')} />
              </div>
            )}
          </div>

          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button className="bottom-action-btn gold" onClick={() => navigate(userType === 'student' ? `/admin/users/students/${id}/edit` : `/admin/users/staff/${id}/edit`)}>
                <Edit3 size={18} /> {t('edit_user')}
              </button>
              <span className="action-separator"></span>
              <button className={`bottom-action-btn ${user.isActive ? 'soft-red' : 'soft-green'}`} onClick={handleToggleActive}>
                {user.isActive ? <XCircle size={18} /> : <CheckCircle size={18} />}
                {user.isActive ? t('deactivate') : t('activate')}
              </button>
              <button className="bottom-action-btn soft-red" onClick={handleSoftDelete}>
                <Trash2 size={18} /> {t('delete')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default UserDetails;