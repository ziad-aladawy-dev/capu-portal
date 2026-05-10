import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Menu, User, Mail, Phone, Shield, CheckCircle2, ArrowLeft, Save, Bell, Moon, UserCircle2, BookOpen, Building2, Calendar, Award, XCircle, AlertCircle } from 'lucide-react';
import userService from '../../api/userService';
import Sidebar from '../../layouts/Sidebar/Sidebar';
import LoadingSpinner from '../../components/UI/LoadingSpinner';

const EditStudent = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [validationErrors, setValidationErrors] = useState({});
  const [showSuccess, setShowSuccess] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  
  const [faculties, setFaculties] = useState([]);
  const [programs, setPrograms] = useState([]);
  const [levels, setLevels] = useState([]);
  const [filteredPrograms, setFilteredPrograms] = useState([]);
  const [filteredLevels, setFilteredLevels] = useState([]);
  
  const [formData, setFormData] = useState({
    fullNameAr: '',
    fullNameEn: '',
    email: '',
    phone: '',
    levelId: '',
    facultyId: '',
    programId: '',
    status: 'Active',
    gpa: '',
    dateOfBirth: '',
    isActive: true
  });

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setError(null);
      try {
        const [studentData, facultiesData] = await Promise.all([
          userService.getStudentById(id),
          userService.getFaculties()
        ]);
        
        if (!studentData) {
          setError('Student not found');
          return;
        }
        
        setFaculties(facultiesData);
        
        // Load programs and levels if faculty and program exist
        if (studentData.facultyId) {
          const programsData = await userService.getDepartments(studentData.facultyId);
          setFilteredPrograms(programsData);
          
          if (studentData.programId) {
            const levelsData = await userService.getLevels(studentData.programId);
            setFilteredLevels(levelsData);
          }
        }
        
        setFormData({
          fullNameAr: studentData.fullNameAr || '',
          fullNameEn: studentData.fullNameEn || '',
          email: studentData.email || '',
          phone: studentData.phone || '',
          levelId: studentData.levelId || '',
          facultyId: studentData.facultyId || '',
          programId: studentData.programId || '',
          status: studentData.status || 'Active',
          gpa: studentData.gpa || '',
          dateOfBirth: studentData.dateOfBirth ? studentData.dateOfBirth.split('T')[0] : '',
          isActive: studentData.isActive !== undefined ? studentData.isActive : true
        });
        
      } catch (err) {
        console.error('Error loading student:', err);
        setError(err.message || 'Failed to load student data');
      } finally {
        setLoading(false);
      }
    };
    
    loadData();
  }, [id]);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({ 
      ...prev, 
      [name]: type === 'checkbox' ? checked : value 
    }));
    if (validationErrors[name]) {
      setValidationErrors(prev => ({ ...prev, [name]: '' }));
    }
  };

  const handleFacultyChange = async (e) => {
    const facultyId = e.target.value;
    setFormData(prev => ({ ...prev, facultyId, programId: '', levelId: '' }));
    
    if (facultyId) {
      try {
        const programsData = await userService.getDepartments(facultyId);
        setFilteredPrograms(programsData);
        setFilteredLevels([]);
      } catch (error) {
        console.error('Error loading programs:', error);
        setFilteredPrograms([]);
      }
    } else {
      setFilteredPrograms([]);
      setFilteredLevels([]);
    }
  };

  const handleProgramChange = async (e) => {
    const programId = e.target.value;
    setFormData(prev => ({ ...prev, programId, levelId: '' }));
    
    if (programId) {
      try {
        const levelsData = await userService.getLevels(programId);
        setFilteredLevels(levelsData);
      } catch (error) {
        console.error('Error loading levels:', error);
        setFilteredLevels([]);
      }
    } else {
      setFilteredLevels([]);
    }
  };

  const validateForm = () => {
    const errors = {};
    
    if (!formData.fullNameAr?.trim()) errors.fullNameAr = 'Arabic name is required';
    if (!formData.fullNameEn?.trim()) errors.fullNameEn = 'English name is required';
    
    if (!formData.email?.trim()) {
      errors.email = 'Email is required';
    } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
      errors.email = 'Invalid email format';
    }
    
    if (!formData.levelId) errors.levelId = 'Level is required';
    
    if (formData.gpa && (formData.gpa < 0 || formData.gpa > 5)) {
      errors.gpa = 'GPA must be between 0 and 5';
    }
    
    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateForm()) {
      const firstError = Object.keys(validationErrors)[0];
      const element = document.querySelector(`[name="${firstError}"]`);
      if (element) element.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    
    setSubmitting(true);
    
    try {
      const updateData = {
        fullNameAr: formData.fullNameAr || null,
        fullNameEn: formData.fullNameEn || null,
        email: formData.email || null,
        phone: formData.phone || null,
        levelId: formData.levelId || null,
        status: formData.status || null,
        gpa: formData.gpa ? parseFloat(formData.gpa) : 0,
        dateOfBirth: formData.dateOfBirth || null,
        isActive: formData.isActive
      };
      
      const result = await userService.updateStudent(id, updateData);
      
      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate(`/users/${id}`), 1500);
      } else {
        setError(result.message || 'Failed to update student');
      }
    } catch (err) {
      console.error('Update error:', err);
      setError(err.message || 'An error occurred while updating');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSpinner fullPage message="Loading student data..." />;

  if (error) {
    return (
      <div className="dashboard-container">
        <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
        <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
          <AlertCircle size={48} color="#dc2626" />
          <h2 style={{ color: '#dc2626', marginTop: '16px' }}>Error</h2>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="edit-user-page">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Space+Mono:wght@400;700&family=DM+Sans:wght@400;500;600;700&display=swap');

        :root {
          --navy-primary: #1a1f5e;
          --navy-secondary: #252b7a;
          --navy-accent: #2e3591;
          --gold: #c9a84c;
          --gold-light: #e0c06a;
          --soft-lavender: #f0f1f8;
          --pure-white: #ffffff;
          --warning-red: #dc2626;
          --success-green: #16a34a;
          --text-primary: #1a1f5e;
          --text-muted: #6b7280;
          --border-color: #e5e7eb;
        }

        .edit-user-page { min-height: 100vh; background: linear-gradient(135deg, #f4f5f7 0%, #edeef5 100%); }

        .top-nav {
          background: linear-gradient(135deg, #1a1f5e 0%, #2e3591 100%);
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
        .menu-icon { color: #ffffff; cursor: pointer; transition: all 0.3s ease; }
        .menu-icon:hover { color: #e0c06a; transform: scale(1.1); }
        .back-button {
          background: rgba(201, 168, 76, 0.15);
          border: none;
          color: #e0c06a;
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
        .nav-right { display: flex; align-items: center; gap: 16px; }
        .nav-icon { color: rgba(255,255,255,0.85); cursor: pointer; transition: all 0.3s; }
        .nav-icon:hover { color: #e0c06a; transform: translateY(-2px); }

        .page-container { max-width: 1400px; margin: 0 auto; padding: 24px 40px; }

        .page-header {
          background: #ffffff;
          border-radius: 20px;
          padding: 32px;
          margin-bottom: 32px;
          box-shadow: 0 8px 30px rgba(26, 31, 94, 0.08);
          display: flex;
          align-items: center;
          justify-content: space-between;
          border: 1px solid #e5e7eb;
          animation: slideDown 0.6s ease;
        }

        .header-content { display: flex; align-items: center; gap: 16px; }
        .header-icon {
          width: 56px;
          height: 56px;
          background: linear-gradient(135deg, #1a1f5e, #2e3591);
          border-radius: 16px;
          display: flex;
          align-items: center;
          justify-content: center;
          color: #e0c06a;
        }
        .header-text h1 { font-size: 28px; font-weight: 700; color: #1a1f5e; font-family: 'Space Mono', monospace; }
        .gold-line { width: 48px; height: 3px; background: linear-gradient(90deg, #c9a84c, #e0c06a); border-radius: 2px; margin-top: 6px; }
        .header-text p { font-size: 14px; color: #6b7280; margin-top: 8px; }

        .status-toggle {
          display: flex;
          align-items: center;
          gap: 12px;
          background: #f8f9fb;
          padding: 12px 24px;
          border-radius: 12px;
        }
        .status-toggle label { font-size: 14px; font-weight: 600; color: #1a1f5e; cursor: pointer; display: flex; align-items: center; gap: 8px; }
        .checkbox-input { width: 18px; height: 18px; accent-color: #c9a84c; }

        @keyframes slideDown { from { opacity: 0; transform: translateY(-20px); } to { opacity: 1; transform: translateY(0); } }
        @keyframes slideUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }

        .form-card {
          background: #ffffff;
          border-radius: 20px;
          overflow: hidden;
          border: 1px solid #e5e7eb;
          margin-bottom: 24px;
          animation: slideUp 0.5s ease;
        }

        .form-header {
          background: linear-gradient(135deg, #1a1f5e 0%, #2e3591 100%);
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

        .form-title-wrapper h3 { font-size: 12px; font-weight: 700; color: #e0c06a; font-family: 'Space Mono', monospace; text-transform: uppercase; letter-spacing: 0.8px; }
        .form-title-wrapper p { font-size: 12px; color: rgba(255,255,255,0.6); margin-top: 4px; }

        .form-body { padding: 28px 32px; }
        .form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }
        .form-label {
          display: block;
          font-size: 12px;
          font-weight: 700;
          color: #1a1f5e;
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
          color: #6b7280;
          z-index: 1;
          pointer-events: none;
        }

        .form-input, .form-select {
          width: 100%;
          padding: 14px 16px 14px 48px;
          border: 2px solid #e5e7eb;
          border-radius: 12px;
          font-size: 14px;
          font-family: 'DM Sans', sans-serif;
          transition: all 0.3s;
          background: #f8f9fb;
          color: #1a1f5e;
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
          border-color: #c9a84c;
          background: #ffffff;
          box-shadow: 0 0 0 4px rgba(201, 168, 76, 0.12);
        }

        .form-input.error, .form-select.error { border-color: #dc2626; background: #fef2f2; }
        .error-message { color: #dc2626; font-size: 12px; margin-top: 6px; display: flex; align-items: center; gap: 4px; }

        .submit-section {
          background: linear-gradient(135deg, #1a1f5e 0%, #2e3591 100%);
          border-radius: 20px;
          padding: 36px;
          text-align: center;
          margin-top: 24px;
        }

        .submit-button {
          background: linear-gradient(135deg, #c9a84c, #e0c06a);
          color: #1a1f5e;
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
          border-top-color: #1a1f5e;
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
          background: white;
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
          background: linear-gradient(135deg, #1a1f5e, #2e3591);
          border-radius: 50%;
          display: flex; align-items: center; justify-content: center;
          margin: 0 auto 24px;
        }
        .success-text { color: #1a1f5e; font-size: 24px; font-weight: 700; margin-bottom: 10px; font-family: 'Space Mono', monospace; }
        .full-width { grid-column: 1 / -1; }

        @media (max-width: 1200px) { .form-grid { grid-template-columns: repeat(2, 1fr); } }
        @media (max-width: 768px) {
          .page-container { padding: 16px; }
          .form-grid { grid-template-columns: 1fr; }
          .page-header { flex-direction: column; align-items: flex-start; gap: 16px; }
          .form-body { padding: 20px; }
          .success-message { margin: 0 20px; padding: 40px 32px; }
        }
      `}</style>

      <div className="top-nav">
        <div className="nav-left">
          <Menu size={24} className="menu-icon" onClick={() => setSidebarOpen(true)} />
          <button className="back-button" onClick={() => navigate(`/users/${id}`)}>
            <ArrowLeft size={16} /> Back to Details
          </button>
        </div>
        <div className="nav-right">
          <Moon size={20} className="nav-icon" />
          <Bell size={20} className="nav-icon" />
        </div>
      </div>

      <div className="page-container">
        <div className="page-header">
          <div className="header-content">
            <div className="header-icon"><UserCircle2 size={28} /></div>
            <div className="header-text">
              <h1>Edit Student</h1>
              <div className="gold-line" />
              <p>Update student information and academic details</p>
            </div>
          </div>
          <div className="status-toggle">
            <label>
              <input type="checkbox" name="isActive" checked={formData.isActive} onChange={handleChange} className="checkbox-input" />
              {formData.isActive ? <CheckCircle2 size={18} color="#16a34a" /> : <XCircle size={18} color="#dc2626" />}
              {formData.isActive ? 'Active Account' : 'Inactive Account'}
            </label>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          {/* Basic Information */}
          <div className="form-card">
            <div className="form-header">
              <div className="form-icon"><User size={22} color="#e0c06a" /></div>
              <div className="form-title-wrapper">
                <h3>Basic Information</h3>
                <p>Personal identification details</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">Full Name (Arabic) *</label>
                  <div className="input-wrapper">
                    <input type="text" name="fullNameAr" className={`form-input ${validationErrors.fullNameAr ? 'error' : ''}`} value={formData.fullNameAr} onChange={handleChange} placeholder="الاسم بالعربية" />
                    <User size={18} className="input-icon" />
                  </div>
                  {validationErrors.fullNameAr && <div className="error-message">{validationErrors.fullNameAr}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">Full Name (English) *</label>
                  <div className="input-wrapper">
                    <input type="text" name="fullNameEn" className={`form-input ${validationErrors.fullNameEn ? 'error' : ''}`} value={formData.fullNameEn} onChange={handleChange} placeholder="Name in English" />
                    <User size={18} className="input-icon" />
                  </div>
                  {validationErrors.fullNameEn && <div className="error-message">{validationErrors.fullNameEn}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">Email *</label>
                  <div className="input-wrapper">
                    <input type="email" name="email" className={`form-input ${validationErrors.email ? 'error' : ''}`} value={formData.email} onChange={handleChange} placeholder="student@university.edu" />
                    <Mail size={18} className="input-icon" />
                  </div>
                  {validationErrors.email && <div className="error-message">{validationErrors.email}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">Phone</label>
                  <div className="input-wrapper">
                    <input type="tel" name="phone" className="form-input" value={formData.phone} onChange={handleChange} placeholder="+20 100 123 4567" />
                    <Phone size={18} className="input-icon" />
                  </div>
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
                <p>Faculty, program, level, and academic status</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">Faculty</label>
                  <div className="input-wrapper">
                    <select name="facultyId" className="form-select" value={formData.facultyId} onChange={handleFacultyChange}>
                      <option value="">Select Faculty</option>
                      {faculties.map(f => (<option key={f.id} value={f.id}>{f.nameEn}</option>))}
                    </select>
                    <Building2 size={18} className="input-icon" />
                  </div>
                </div>

                <div className="form-group">
                  <label className="form-label">Program</label>
                  <div className="input-wrapper">
                    <select name="programId" className="form-select" value={formData.programId} onChange={handleProgramChange} disabled={!formData.facultyId}>
                      <option value="">Select Program</option>
                      {filteredPrograms.map(p => (<option key={p.id} value={p.id}>{p.nameEn}</option>))}
                    </select>
                    <BookOpen size={18} className="input-icon" />
                  </div>
                </div>

                <div className="form-group">
                  <label className="form-label">Level *</label>
                  <div className="input-wrapper">
                    <select name="levelId" className={`form-select ${validationErrors.levelId ? 'error' : ''}`} value={formData.levelId} onChange={handleChange} disabled={!formData.programId}>
                      <option value="">Select Level</option>
                      {filteredLevels.map(l => (<option key={l.id} value={l.id}>{l.nameEn}</option>))}
                    </select>
                    <Award size={18} className="input-icon" />
                  </div>
                  {validationErrors.levelId && <div className="error-message">{validationErrors.levelId}</div>}
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
                    <input type="number" name="gpa" className={`form-input ${validationErrors.gpa ? 'error' : ''}`} value={formData.gpa} onChange={handleChange} step="0.01" min="0" max="5" placeholder="0.00 - 5.00" />
                    <Award size={18} className="input-icon" />
                  </div>
                  {validationErrors.gpa && <div className="error-message">{validationErrors.gpa}</div>}
                </div>
              </div>
            </div>
          </div>

          {/* Submit Button */}
          <div className="submit-section">
            <button type="submit" className="submit-button" disabled={submitting}>
              {submitting ? <><div className="button-spinner"></div> Saving Changes...</> : <><Save size={20} /> Save Changes</>}
            </button>
          </div>
        </form>

        {showSuccess && (
          <>
            <div className="success-overlay" onClick={() => setShowSuccess(false)} />
            <div className="success-message">
              <div className="success-icon"><CheckCircle2 size={40} color="#e0c06a" /></div>
              <div className="success-text">Updated Successfully!</div>
              <p style={{ color: '#6b7280' }}>Redirecting to user details...</p>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default EditStudent;