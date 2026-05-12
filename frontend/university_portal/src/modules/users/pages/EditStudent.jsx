import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Menu, User, Mail, Phone, Shield, CheckCircle2, ArrowLeft, Save, Bell, Moon, UserCircle2, BookOpen, Building2, Calendar, Award, XCircle, AlertCircle } from 'lucide-react';
import userService from '../services/userService';
import LoadingSpinner from '../components/LoadingSpinner';
import "../styles/userForms.css";

const EditStudent = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [validationErrors, setValidationErrors] = useState({});
  const [showSuccess, setShowSuccess] = useState(false);
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
        setTimeout(() => navigate(`/admin/users/${id}`), 1500);
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
          <label className="account-status-toggle">
  <input
    type="checkbox"
    name="isActive"
    checked={formData.isActive}
    onChange={handleChange}
  />

  <span className="account-status-switch">
    <span className="account-status-dot">
      {formData.isActive ? (
        <CheckCircle2 size={11} />
      ) : (
        <XCircle size={11} />
      )}
    </span>
  </span>

  <span className="account-status-text">
    {formData.isActive ? "Active Account" : "Inactive Account"}
  </span>
</label>
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