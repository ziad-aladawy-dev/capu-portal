import React, { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ArrowLeft, User, Mail, Phone, Calendar, Shield, BookOpen, Building2,
  Edit3, Key, Trash2, RefreshCw, Award, Hash, AtSign, CheckCircle, XCircle, Briefcase, Globe,
  UserCircle, Lock, AlertCircle, Camera, Users, GraduationCap
} from 'lucide-react';
import { getLocalized, parseLocalizedValue } from '../../../core/utils/getLocalized';
import userService from '../services/userService';
import LoadingSpinner from '../components/LoadingSpinner';
import ErrorMessage from '../components/ErrorMessage';
import '../styles/userDetails.css';

const UserDetails = () => {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const navigate = useNavigate();

  const [user, setUser] = useState(null);
  const [userType, setUserType] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState('personal');
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const fileInputRef = useRef(null);

  const getApiBaseUrl = () => {
    return import.meta.env.VITE_API_BASE_URL?.replace('/api', '') || 'http://localhost:5256';
  };

  const getPhotoUrl = () => {
    if (!user?.photoUrl) return null;
    if (user.photoUrl.startsWith('http')) return user.photoUrl;
    return `${getApiBaseUrl()}${user.photoUrl}`;
  };

  const getAvatarInitial = () => {
    if (!user) return 'U';
    const localizedName = getLocalized(user.name, i18n.language);
    return localizedName.charAt(0).toUpperCase();
  };

  const handlePhotoClick = () => {
    fileInputRef.current?.click();
  };

  const handlePhotoChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      alert(t('invalid_image_type'));
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      alert(t('image_too_large'));
      return;
    }

    setUploadingPhoto(true);
    try {
      const result = userType === 'student'
        ? await userService.uploadStudentPhoto(id, file)
        : await userService.uploadStaffPhoto(id, file);
      const updatedUser = await (userType === 'student' ? userService.getStudentById(id) : userService.getStaffById(id));
      setUser(updatedUser);
    } catch (err) {
      alert(err.response?.data?.message || err.message);
    } finally {
      setUploadingPhoto(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
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

  if (loading) {
    return (
      <div className="user-details-page">
        <div className="ud-loading">
          <div className="ud-spinner" />
          <p>{t('loading_user_details')}</p>
        </div>
      </div>
    );
  }

  if (error || !user) {
    return (
      <div className="user-details-page">
        <div className="ud-error">
          <AlertCircle size={36} />
          <h3>{t('error')}</h3>
          <p>{error || t('user_not_found')}</p>
          <button className="ud-btn ud-btn-outline" onClick={() => navigate('/admin/users')}>
            <ArrowLeft size={13} /> {t('back')}
          </button>
        </div>
      </div>
    );
  }

  const localizedName = getLocalized(user.name, i18n.language);
  const { ar: nameAr, en: nameEn } = parseLocalizedValue(user.name);
  const userRoleLabel = userType === 'student' ? t('student') : t('staff');
  const statusLabel = user.isActive ? t('active') : t('inactive');
  const passwordStatusLabel = isPasswordExpired ? t('expired') : t('valid');

  const InfoCard = ({ icon, label, value }) => (
    <div className="ud-info-card">
      <div className="ud-info-icon">{icon}</div>
      <div className="ud-info-content">
        <span className="ud-info-label">{label}</span>
        <div className="ud-info-value">{value || t('not_specified')}</div>
      </div>
    </div>
  );

  return (
    <div className="user-details-page">
      {/* ─── Hero ─── */}
      <div className="ud-hero ud-fade-in">
        <div className="ud-hero-avatar-wrapper">
          {getPhotoUrl() ? (
            <img src={getPhotoUrl()} alt={localizedName} className="ud-hero-photo" />
          ) : (
            <div className="ud-hero-avatar">{getAvatarInitial()}</div>
          )}
          <button className="ud-photo-upload-btn" onClick={handlePhotoClick} disabled={uploadingPhoto} title={t('upload_photo')}>
            {uploadingPhoto ? <RefreshCw size={14} className="ud-spin-icon" /> : <Camera size={14} />}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            style={{ display: 'none' }}
            onChange={handlePhotoChange}
          />
        </div>
        <div className="ud-hero-body">
          <h1>{localizedName}</h1>
          <p className="ud-hero-email">{user.email}</p>
          <div className="ud-hero-badges">
            <span className={`ud-hero-badge type-${userType}`}>
              <UserCircle size={12} /> {userRoleLabel}
            </span>
            <span className={`ud-hero-badge status-${user.isActive ? 'active' : 'inactive'}`}>
              <span className="ud-hero-badge-dot" /> {statusLabel}
            </span>
            <span className={`ud-hero-badge password-${isPasswordExpired ? 'expired' : 'valid'}`}>
              <Lock size={12} /> {passwordStatusLabel}
            </span>
          </div>
        </div>
        <div className="ud-hero-actions">
          <button className="ud-btn ud-btn-ghost" onClick={() => navigate(userType === 'student' ? `/admin/users/students/${id}/edit` : `/admin/users/staff/${id}/edit`)}>
            <Edit3 size={14} /> {t('edit')}
          </button>
          <button className={`ud-btn ${user.isActive ? 'ud-btn-ghost' : 'ud-btn-ghost'}`} onClick={handleToggleActive}>
            {user.isActive ? <XCircle size={14} /> : <CheckCircle size={14} />}
            {user.isActive ? t('deactivate') : t('activate')}
          </button>
          <button className="ud-btn ud-btn-ghost" onClick={handleSoftDelete}>
            <Trash2 size={14} /> {t('delete')}
          </button>
        </div>
      </div>

      {/* ─── Tabs ─── */}
      <div className="ud-tabs">
        <button className={`ud-tab ${activeTab === 'personal' ? 'active' : ''}`} onClick={() => setActiveTab('personal')}>
          <User size={14} /> {t('personal_information')}
        </button>
        {userType === 'student' && (
          <button className={`ud-tab ${activeTab === 'academic' ? 'active' : ''}`} onClick={() => setActiveTab('academic')}>
            <BookOpen size={14} /> {t('academic_information')}
          </button>
        )}
        {userType === 'staff' && (
          <button className={`ud-tab ${activeTab === 'employment' ? 'active' : ''}`} onClick={() => setActiveTab('employment')}>
            <Briefcase size={14} /> {t('employment_information')}
          </button>
        )}
        <button className={`ud-tab ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>
          <Shield size={14} /> {t('account_details')}
        </button>
      </div>

      {/* ─── Tab Content ─── */}
      <div className="ud-fade-in" key={activeTab}>
        {activeTab === 'personal' && (
          <div className="ud-section">
            <h3 className="ud-section-title"><User size={16} /> {t('personal_information')}</h3>
            <div className="ud-info-grid">
              <InfoCard icon={<Hash size={17} />} label={t('national_id')} value={user.nationalId} />
              <InfoCard icon={<User size={17} />} label={t('full_name_arabic')} value={nameAr || localizedName} />
              <InfoCard icon={<Globe size={17} />} label={t('full_name_english')} value={nameEn || t('not_specified')} />
              <InfoCard icon={<Calendar size={17} />} label={t('date_of_birth')} value={formatDate(user.birthDate)} />
              <InfoCard icon={<Users size={17} />} label={t('gender')} value={user.gender ? t(user.gender.toLowerCase()) : t('not_specified')} />
              <InfoCard icon={<Phone size={17} />} label={t('phone')} value={user.phoneNumber} />
              <InfoCard icon={<Mail size={17} />} label={t('email')} value={user.email} />
              {userType === 'student' && (
                <>
                  <InfoCard icon={<Users size={17} />} label={t('guardian_name')} value={user.guardianName || t('not_specified')} />
                  <InfoCard icon={<Phone size={17} />} label={t('guardian_phone')} value={user.guardianPhone || t('not_specified')} />
                </>
              )}
              {userType === 'staff' && (
                <InfoCard icon={<GraduationCap size={17} />} label={t('qualification')} value={user.qualification || t('not_specified')} />
              )}
            </div>
          </div>
        )}

        {activeTab === 'academic' && userType === 'student' && (
          <div className="ud-section">
            <h3 className="ud-section-title"><BookOpen size={16} /> {t('academic_information')}</h3>
            <div className="ud-info-grid">
              <InfoCard icon={<Award size={17} />} label={t('student_code')} value={user.studentCode} />
              <InfoCard icon={<Building2 size={17} />} label={t('faculty')} value={user.facultyName} />
              <InfoCard icon={<BookOpen size={17} />} label={t('program')} value={user.programName} />
              <InfoCard icon={<Award size={17} />} label={t('level')} value={user.levelName} />
              <InfoCard icon={<Shield size={17} />} label={t('academic_status')} value={user.status || t('active')} />
            </div>
          </div>
        )}

        {activeTab === 'employment' && userType === 'staff' && (
          <div className="ud-section">
            <h3 className="ud-section-title"><Briefcase size={16} /> {t('employment_information')}</h3>
            <div className="ud-info-grid">
              <InfoCard icon={<Award size={17} />} label={t('employee_code')} value={user.employeeCode} />
              <InfoCard icon={<Shield size={17} />} label={t('role')} value={user.role} />
              <InfoCard icon={<Briefcase size={17} />} label={t('job_title')} value={user.jobTitle} />
              <InfoCard icon={<Building2 size={17} />} label={t('faculty_department')} value={user.facultyName || user.structureNodeName} />
            </div>
          </div>
        )}

        {activeTab === 'account' && (
          <div className="ud-section">
            <h3 className="ud-section-title"><Shield size={16} /> {t('account_details')}</h3>
            <div className="ud-info-grid">
              <InfoCard icon={<Calendar size={17} />} label={t('account_created')} value={formatDateTime(user.createdAt)} />
              <InfoCard icon={<Calendar size={17} />} label={t('last_updated')} value={formatDateTime(user.updatedAt)} />
              <InfoCard icon={<Key size={17} />} label={t('password_status')} value={user.passwordStatus || (isPasswordExpired ? t('expired') : t('valid'))} />
              <InfoCard icon={<CheckCircle size={17} />} label={t('account_status')} value={user.isActive ? t('active') : t('inactive')} />
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default UserDetails;
