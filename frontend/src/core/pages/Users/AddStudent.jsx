import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Menu, User, Mail, Lock, Phone, Shield, CheckCircle2, ArrowLeft, UserPlus, Bell, Moon, UserCircle2, KeyRound, BookOpen, Building2, Calendar, Award } from 'lucide-react';
import userService from '../../api/userService';
import Sidebar from '../../layouts/Sidebar/Sidebar';

const AddStudent = () => {
  const navigate = useNavigate();
  
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  
  const [faculties, setFaculties] = useState([]);
  const [programs, setPrograms] = useState([]);
  const [levels, setLevels] = useState([]);
  const [filteredPrograms, setFilteredPrograms] = useState([]);
  const [filteredLevels, setFilteredLevels] = useState([]);
  
  const [formData, setFormData] = useState({
    nationalId: '',
    fullNameAr: '',
    fullNameEn: '',
    email: '',
    password: '',
    confirmPassword: '',
    facultyId: '',
    programId: '',
    levelId: '',
    phone: '',
    dateOfBirth: '',
    status: 'Active',
    gpa: ''
  });
  
  const [errors, setErrors] = useState({});
  const [checkingEmail, setCheckingEmail] = useState(false);
  const [checkingNationalId, setCheckingNationalId] = useState(false);

  useEffect(() => {
    const loadFaculties = async () => {
      setLoading(true);
      try {
        const facultiesData = await userService.getFaculties();
        setFaculties(facultiesData);
      } catch (error) {
        console.error('Error loading faculties:', error);
      } finally {
        setLoading(false);
      }
    };
    loadFaculties();
  }, []);

  useEffect(() => {
    const loadPrograms = async () => {
      if (formData.facultyId) {
        try {
          const programsData = await userService.getDepartments(formData.facultyId);
          setFilteredPrograms(programsData);
          setFormData(prev => ({ ...prev, programId: '', levelId: '' }));
          setFilteredLevels([]);
        } catch (error) {
          setFilteredPrograms([]);
        }
      } else {
        setFilteredPrograms([]);
        setFormData(prev => ({ ...prev, programId: '', levelId: '' }));
        setFilteredLevels([]);
      }
    };
    loadPrograms();
  }, [formData.facultyId]);

  useEffect(() => {
    const loadLevels = async () => {
      if (formData.programId) {
        try {
          const levelsData = await userService.getLevels(formData.programId);
          setFilteredLevels(levelsData);
          if (levelsData.length > 0 && !formData.levelId) {
            setFormData(prev => ({ ...prev, levelId: levelsData[0].id }));
          }
        } catch (error) {
          setFilteredLevels([]);
        }
      } else {
        setFilteredLevels([]);
        setFormData(prev => ({ ...prev, levelId: '' }));
      }
    };
    loadLevels();
  }, [formData.programId]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors(prev => ({ ...prev, [name]: '' }));
    }
  };

  const checkEmailUnique = async (email) => {
    if (!email) return true;
    setCheckingEmail(true);
    try {
      const result = await userService.checkEmailUnique(email, 'Student');
      return result.isUnique;
    } catch (error) {
      return false;
    } finally {
      setCheckingEmail(false);
    }
  };

  const checkNationalIdUnique = async (nationalId) => {
    if (!nationalId) return true;
    setCheckingNationalId(true);
    try {
      const result = await userService.checkNationalIdUnique(nationalId, 'Student');
      return result.isUnique;
    } catch (error) {
      return false;
    } finally {
      setCheckingNationalId(false);
    }
  };

  const validateForm = async () => {
    const newErrors = {};

    // National ID
    if (!formData.nationalId) {
      newErrors.nationalId = 'National ID is required';
    } else if (formData.nationalId.length !== 14) {
      newErrors.nationalId = 'National ID must be 14 digits';
    } else if (!/^\d+$/.test(formData.nationalId)) {
      newErrors.nationalId = 'National ID must contain only numbers';
    } else {
      const isUnique = await checkNationalIdUnique(formData.nationalId);
      if (!isUnique) newErrors.nationalId = 'National ID already exists';
    }

    // Names
    if (!formData.fullNameAr?.trim()) newErrors.fullNameAr = 'Arabic name is required';
    if (!formData.fullNameEn?.trim()) newErrors.fullNameEn = 'English name is required';

    // Email
    if (!formData.email) {
      newErrors.email = 'Email is required';
    } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
      newErrors.email = 'Invalid email format';
    } else {
      const isUnique = await checkEmailUnique(formData.email);
      if (!isUnique) newErrors.email = 'Email already exists';
    }

    // Password
    if (!formData.password) {
      newErrors.password = 'Password is required';
    } else if (formData.password.length < 6) {
      newErrors.password = 'Password must be at least 6 characters';
    }

    // Confirm Password
    if (formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = 'Passwords do not match';
    }

    // Academic selections
    if (!formData.facultyId) newErrors.facultyId = 'Faculty is required';
    if (!formData.programId) newErrors.programId = 'Program is required';
    if (!formData.levelId) newErrors.levelId = 'Level is required';

    // GPA validation
    if (formData.gpa && (formData.gpa < 0 || formData.gpa > 5)) {
      newErrors.gpa = 'GPA must be between 0 and 5';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    const isValid = await validateForm();
    if (!isValid) {
      const firstError = Object.keys(errors)[0];
      const element = document.querySelector(`[name="${firstError}"]`);
      if (element) element.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }

    setSubmitting(true);

    try {
      const studentData = {
        nationalId: formData.nationalId,
        fullNameAr: formData.fullNameAr,
        fullNameEn: formData.fullNameEn,
        email: formData.email,
        password: formData.password,
        levelId: formData.levelId,
        phone: formData.phone || null,
        dateOfBirth: formData.dateOfBirth || null,
        status: formData.status,
        gpa: formData.gpa ? parseFloat(formData.gpa) : 0
      };

      const result = await userService.createStudent(studentData);

      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate('/users'), 2000);
      } else {
        alert(result.message || 'Error occurred while adding student');
      }
    } catch (error) {
      alert(error.message || 'Error occurred while adding student');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div style={styles.loadingContainer}>
        <div style={styles.loadingSpinner}></div>
        <p>Loading data...</p>
      </div>
    );
  }

  return (
    <div className="add-user-page">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Space+Mono:wght@400;700&family=DM+Sans:wght@400;500;700&display=swap');

        :root {
          --navy-primary: #1a1f5e;
          --navy-secondary: #252b7a;
          --navy-accent: #2e3591;
          --gold: #c9a84c;
          --gold-light: #e0c06a;
          --gold-pale: #fdf6e3;
          --soft-lavender: #f0f1f8;
          --pure-white: #ffffff;
          --warning-red: #dc2626;
          --success-green: #16a34a;
          --text-primary: #1a1f5e;
          --text-muted: #6b7280;
          --border-color: #e5e7eb;
        }

        * { margin: 0; padding: 0; box-sizing: border-box; }

        body {
          font-family: 'DM Sans', sans-serif;
          background: linear-gradient(135deg, #f4f5f7 0%, #edeef5 100%);
          min-height: 100vh;
        }

        .add-user-page { min-height: 100vh; background: linear-gradient(135deg, #f4f5f7 0%, #edeef5 100%); }

        .top-nav {
          background: linear-gradient(135deg, var(--navy-primary) 0%, var(--navy-accent) 100%);
          height: 60px;
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 0 24px;
          box-shadow: 0 4px 20px rgba(26, 31, 94, 0.25);
          position: sticky;
          top: 0;
          z-index: 1000;
          width: 100%;
        }

        .nav-left { display: flex; align-items: center; gap: 16px; }
        .menu-icon { color: var(--pure-white); cursor: pointer; transition: all 0.3s ease; }
        .menu-icon:hover { color: var(--gold-light); transform: scale(1.1); }
        .back-button {
          background: rgba(201, 168, 76, 0.15);
          border: none;
          color: var(--gold-light);
          padding: 8px 16px;
          border-radius: 8px;
          font-size: 13px;
          font-weight: 600;
          cursor: pointer;
          display: flex;
          align-items: center;
          gap: 6px;
          transition: all 0.3s;
        }
        .back-button:hover { background: rgba(201, 168, 76, 0.25); transform: translateX(-3px); }
        .nav-right { display: flex; align-items: center; gap: 20px; }
        .nav-icon { color: rgba(255,255,255,0.85); cursor: pointer; transition: all 0.3s; }
        .nav-icon:hover { color: var(--gold-light); transform: translateY(-2px); }

        .page-container { max-width: 1400px; margin: 0 auto; padding: 24px 40px; }

        .page-header {
          background: var(--pure-white);
          border-radius: 20px;
          padding: 32px;
          margin-bottom: 32px;
          box-shadow: 0 8px 30px rgba(26, 31, 94, 0.08);
          display: flex;
          align-items: center;
          border: 1px solid var(--border-color);
          animation: slideDown 0.6s ease;
        }

        .header-content { display: flex; align-items: center; gap: 16px; }
        .header-icon {
          width: 56px;
          height: 56px;
          background: linear-gradient(135deg, var(--navy-primary), var(--navy-accent));
          border-radius: 16px;
          display: flex;
          align-items: center;
          justify-content: center;
          color: var(--gold-light);
          box-shadow: 0 8px 20px rgba(26, 31, 94, 0.25);
        }
        .header-text h1 { font-size: 28px; font-weight: 700; color: var(--navy-primary); font-family: 'Space Mono', monospace; }
        .gold-line { width: 48px; height: 3px; background: linear-gradient(90deg, var(--gold), var(--gold-light)); border-radius: 2px; margin-top: 6px; }
        .header-text p { font-size: 14px; color: var(--text-muted); margin-top: 8px; }

        @keyframes slideDown { from { opacity: 0; transform: translateY(-20px); } to { opacity: 1; transform: translateY(0); } }
        @keyframes slideUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }

        .form-card {
          background: var(--pure-white);
          border-radius: 20px;
          overflow: hidden;
          border: 1px solid var(--border-color);
          margin-bottom: 24px;
          animation: slideUp 0.5s ease;
        }

        .form-header {
          background: linear-gradient(135deg, var(--navy-primary) 0%, var(--navy-accent) 100%);
          padding: 18px 24px;
          display: flex;
          align-items: center;
          gap: 16px;
        }

        .form-icon {
          width: 42px;
          height: 42px;
          background: rgba(201, 168, 76, 0.2);
          border-radius: 10px;
          display: flex;
          align-items: center;
          justify-content: center;
        }

        .form-title-wrapper h3 {
          font-size: 12px;
          font-weight: 700;
          color: var(--gold-light);
          font-family: 'Space Mono', monospace;
          text-transform: uppercase;
          letter-spacing: 0.8px;
        }
        .form-title-wrapper p { font-size: 12px; color: rgba(255,255,255,0.6); margin-top: 4px; }

        .form-body { padding: 28px 32px; }
        .form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }
        .form-label {
          display: block;
          font-size: 12px;
          font-weight: 700;
          color: var(--navy-primary);
          margin-bottom: 10px;
          text-transform: uppercase;
          letter-spacing: 0.8px;
          font-family: 'Space Mono', monospace;
        }

        .input-wrapper { position: relative; }
        .input-icon {
          position: absolute;
          left: 16px;
          top: 50%;
          transform: translateY(-50%);
          color: var(--text-muted);
          z-index: 1;
          pointer-events: none;
        }

        .form-input, .form-select {
          width: 100%;
          padding: 14px 16px 14px 48px;
          border: 2px solid var(--border-color);
          border-radius: 12px;
          font-size: 14px;
          font-family: 'DM Sans', sans-serif;
          transition: all 0.3s;
          background: var(--soft-lavender);
          color: var(--text-primary);
        }

        .form-select {
          cursor: pointer;
          appearance: none;
          background-image: url("data:image/svg+xml,%3Csvg width='12' height='8' viewBox='0 0 12 8' fill='none' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M1 1.5L6 6.5L11 1.5' stroke='%236b7280' stroke-width='2' stroke-linecap='round'/%3E%3C/svg%3E");
          background-repeat: no-repeat;
          background-position: right 16px center;
          padding-right: 48px;
        }

        .form-input:focus, .form-select:focus {
          outline: none;
          border-color: var(--gold);
          background: var(--pure-white);
          box-shadow: 0 0 0 4px rgba(201, 168, 76, 0.12);
        }

        .form-input.error, .form-select.error { border-color: var(--warning-red); background: #fef2f2; }
        .error-message { color: var(--warning-red); font-size: 12px; margin-top: 6px; display: flex; align-items: center; gap: 4px; }
        .input-hint { font-size: 11px; color: var(--text-muted); margin-top: 6px; display: block; }

        .submit-section {
          background: linear-gradient(135deg, var(--navy-primary) 0%, var(--navy-accent) 100%);
          border-radius: 20px;
          padding: 36px;
          text-align: center;
          animation: slideUp 0.6s ease 0.3s backwards;
        }

        .submit-button {
          background: linear-gradient(135deg, var(--gold), var(--gold-light));
          color: var(--navy-primary);
          border: none;
          padding: 14px 48px;
          border-radius: 12px;
          font-size: 16px;
          font-weight: 700;
          cursor: pointer;
          display: inline-flex;
          align-items: center;
          gap: 10px;
          transition: all 0.3s;
          box-shadow: 0 6px 20px rgba(201, 168, 76, 0.35);
        }
        .submit-button:hover:not(:disabled) { transform: translateY(-2px); box-shadow: 0 10px 30px rgba(201, 168, 76, 0.45); }
        .submit-button:disabled { opacity: 0.7; cursor: not-allowed; }

        .button-spinner {
          width: 18px;
          height: 18px;
          border: 3px solid rgba(26, 31, 94, 0.3);
          border-top-color: var(--navy-primary);
          border-radius: 50%;
          animation: spin 0.8s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }

        .success-overlay {
          position: fixed;
          top: 0; left: 0; right: 0; bottom: 0;
          background: rgba(26, 31, 94, 0.6);
          backdrop-filter: blur(8px);
          z-index: 9998;
        }

        .success-message {
          position: fixed;
          top: 50%; left: 50%;
          transform: translate(-50%, -50%);
          background: var(--pure-white);
          padding: 56px 72px;
          border-radius: 24px;
          box-shadow: 0 25px 70px rgba(26, 31, 94, 0.3);
          z-index: 9999;
          text-align: center;
          animation: scaleIn 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
        }
        @keyframes scaleIn {
          from { transform: translate(-50%, -50%) scale(0); opacity: 0; }
          to { transform: translate(-50%, -50%) scale(1); opacity: 1; }
        }
        .success-icon {
          width: 90px; height: 90px;
          background: linear-gradient(135deg, var(--navy-primary), var(--navy-accent));
          border-radius: 50%;
          display: flex; align-items: center; justify-content: center;
          margin: 0 auto 24px;
        }
        .success-text { color: var(--navy-primary); font-size: 24px; font-weight: 700; margin-bottom: 10px; font-family: 'Space Mono', monospace; }
        .full-width { grid-column: 1 / -1; }

        @media (max-width: 1200px) { .form-grid { grid-template-columns: repeat(2, 1fr); } }
        @media (max-width: 768px) {
          .page-container { padding: 24px 16px; }
          .page-header { padding: 24px; }
          .form-grid { grid-template-columns: 1fr; }
          .form-body { padding: 20px; }
          .submit-section { padding: 28px 20px; }
          .success-message { margin: 0 20px; padding: 40px 32px; }
        }
      `}</style>

      <div className="top-nav">
        <div className="nav-left">
          <Menu size={24} className="menu-icon" onClick={() => setSidebarOpen(true)} />
          {/* <button className="back-button" onClick={() => navigate('/users')}>
            <ArrowLeft size={16} /> Back to Users
          </button> */}
        </div>
        <div className="nav-right">
          <Moon size={20} className="nav-icon" />
          <Bell size={20} className="nav-icon" />
        </div>
      </div>

      <div className="page-container">
        <div className="page-header">
          <div className="header-content">
            <div className="header-icon"><UserPlus size={26} /></div>
            <div className="header-text">
              <h1>Add New Student</h1>
              <div className="gold-line" />
              <p>Create a new student account</p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          {/* Basic Information */}
          <div className="form-card">
            <div className="form-header">
              <div className="form-icon"><User size={22} color="#e0c06a" /></div>
              <div className="form-title-wrapper">
                <h3>Basic Information</h3>
                <p>Personal identification and login credentials</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">National ID *</label>
                  <div className="input-wrapper">
                    <input type="text" name="nationalId" className={`form-input ${errors.nationalId ? 'error' : ''}`} placeholder="14-digit national ID" value={formData.nationalId} onChange={handleChange} maxLength="14" />
                    <User size={18} className="input-icon" />
                  </div>
                  {checkingNationalId && <span className="input-hint">Checking availability...</span>}
                  {errors.nationalId && <span className="error-message">{errors.nationalId}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Full Name (Arabic) *</label>
                  <div className="input-wrapper">
                    <input type="text" name="fullNameAr" className={`form-input ${errors.fullNameAr ? 'error' : ''}`} placeholder="الاسم بالعربية" value={formData.fullNameAr} onChange={handleChange} />
                    <User size={18} className="input-icon" />
                  </div>
                  {errors.fullNameAr && <span className="error-message">{errors.fullNameAr}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Full Name (English) *</label>
                  <div className="input-wrapper">
                    <input type="text" name="fullNameEn" className={`form-input ${errors.fullNameEn ? 'error' : ''}`} placeholder="Name in English" value={formData.fullNameEn} onChange={handleChange} />
                    <User size={18} className="input-icon" />
                  </div>
                  {errors.fullNameEn && <span className="error-message">{errors.fullNameEn}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Email *</label>
                  <div className="input-wrapper">
                    <input type="email" name="email" className={`form-input ${errors.email ? 'error' : ''}`} placeholder="student@university.edu" value={formData.email} onChange={handleChange} />
                    <Mail size={18} className="input-icon" />
                  </div>
                  {checkingEmail && <span className="input-hint">Checking availability...</span>}
                  {errors.email && <span className="error-message">{errors.email}</span>}
                </div>
              </div>
            </div>
          </div>

          {/* Account Security */}
          <div className="form-card">
            <div className="form-header">
              <div className="form-icon"><KeyRound size={22} color="#e0c06a" /></div>
              <div className="form-title-wrapper">
                <h3>Account Security</h3>
                <p>Password for student login</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">Password *</label>
                  <div className="input-wrapper">
                    <input type="password" name="password" className={`form-input ${errors.password ? 'error' : ''}`} placeholder="Create password (min 6 characters)" value={formData.password} onChange={handleChange} />
                    <Lock size={18} className="input-icon" />
                  </div>
                  {errors.password && <span className="error-message">{errors.password}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Confirm Password *</label>
                  <div className="input-wrapper">
                    <input type="password" name="confirmPassword" className={`form-input ${errors.confirmPassword ? 'error' : ''}`} placeholder="Re-enter password" value={formData.confirmPassword} onChange={handleChange} />
                    <Lock size={18} className="input-icon" />
                  </div>
                  {errors.confirmPassword && <span className="error-message">{errors.confirmPassword}</span>}
                </div>
              </div>
            </div>
          </div>

          {/* Academic Information */}
          <div className="form-card">
            <div className="form-header">
              <div className="form-icon"><BookOpen size={22} color="#e0c06a" /></div>
              <div className="form-title-wrapper">
                <h3>Academic Information</h3>
                <p>Faculty, program, level, and academic details</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">Faculty *</label>
                  <div className="input-wrapper">
                    <select name="facultyId" className={`form-select ${errors.facultyId ? 'error' : ''}`} value={formData.facultyId} onChange={handleChange}>
                      <option value="">Select Faculty</option>
                      {faculties.map(f => (<option key={f.id} value={f.id}>{f.nameEn}</option>))}
                    </select>
                    <Building2 size={18} className="input-icon" />
                  </div>
                  {errors.facultyId && <span className="error-message">{errors.facultyId}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Program *</label>
                  <div className="input-wrapper">
                    <select name="programId" className={`form-select ${errors.programId ? 'error' : ''}`} value={formData.programId} onChange={handleChange} disabled={!formData.facultyId}>
                      <option value="">Select Program</option>
                      {filteredPrograms.map(p => (<option key={p.id} value={p.id}>{p.nameEn}</option>))}
                    </select>
                    <BookOpen size={18} className="input-icon" />
                  </div>
                  {errors.programId && <span className="error-message">{errors.programId}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Level *</label>
                  <div className="input-wrapper">
                    <select name="levelId" className={`form-select ${errors.levelId ? 'error' : ''}`} value={formData.levelId} onChange={handleChange} disabled={!formData.programId}>
                      <option value="">Select Level</option>
                      {filteredLevels.map(l => (<option key={l.id} value={l.id}>{l.nameEn}</option>))}
                    </select>
                    <Award size={18} className="input-icon" />
                  </div>
                  {errors.levelId && <span className="error-message">{errors.levelId}</span>}
                </div>

                <div className="form-group">
                  <label className="form-label">Phone</label>
                  <div className="input-wrapper">
                    <input type="tel" name="phone" className="form-input" placeholder="Phone number" value={formData.phone} onChange={handleChange} />
                    <Phone size={18} className="input-icon" />
                  </div>
                </div>

                <div className="form-group">
                  <label className="form-label">Date of Birth</label>
                  <div className="input-wrapper">
                    <input type="date" name="dateOfBirth" className="form-input" value={formData.dateOfBirth} onChange={handleChange} />
                    <Calendar size={18} className="input-icon" />
                  </div>
                </div>

                <div className="form-group">
                  <label className="form-label">Academic Status</label>
                  <div className="input-wrapper">
                    <select name="status" className="form-select" value={formData.status} onChange={handleChange}>
                      <option value="Active">Active</option>
                      <option value="Graduated">Graduated</option>
                      <option value="Suspended">Suspended</option>
                      <option value="Probation">Probation</option>
                    </select>
                    <Shield size={18} className="input-icon" />
                  </div>
                </div>

                <div className="form-group">
                  <label className="form-label">GPA</label>
                  <div className="input-wrapper">
                    <input type="number" name="gpa" className={`form-input ${errors.gpa ? 'error' : ''}`} placeholder="0.00 - 5.00" value={formData.gpa} onChange={handleChange} step="0.01" min="0" max="5" />
                    <Award size={18} className="input-icon" />
                  </div>
                  {errors.gpa && <span className="error-message">{errors.gpa}</span>}
                </div>
              </div>
            </div>
          </div>

          {/* Submit Button */}
          <div className="submit-section">
            <button type="submit" className="submit-button" disabled={submitting}>
              {submitting ? <><div className="button-spinner"></div> Creating Student...</> : <><UserPlus size={20} /> Create Student Account</>}
            </button>
          </div>
        </form>
      </div>

      {showSuccess && (
        <>
          <div className="success-overlay" />
          <div className="success-message">
            <div className="success-icon"><CheckCircle2 size={44} color="#e0c06a" /></div>
            <div className="success-text">Student Created Successfully!</div>
            <p style={{ color: 'var(--text-muted)' }}>Redirecting to users list...</p>
          </div>
        </>
      )}
    </div>
  );
};

const styles = {
  loadingContainer: { minHeight: '100vh', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, #f4f5f7 0%, #edeef5 100%)' },
  loadingSpinner: { width: '50px', height: '50px', border: '5px solid #f3f3f3', borderTop: '5px solid #1a1f5e', borderRadius: '50%', animation: 'spin 1s linear infinite', marginBottom: '20px' }
};

export default AddStudent;