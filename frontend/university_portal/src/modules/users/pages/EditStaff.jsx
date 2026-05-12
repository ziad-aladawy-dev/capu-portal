import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Menu, User, Mail, Phone, Shield, CheckCircle2, ArrowLeft, Save, Bell, Moon, UserCircle2, Briefcase, Building2, XCircle, AlertCircle } from 'lucide-react';
import userService from '../services/userService';
import LoadingSpinner from '../components/LoadingSpinner';
import "../styles/userForms.css";

const EditStaff = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [validationErrors, setValidationErrors] = useState({});
  const [showSuccess, setShowSuccess] = useState(false);
const [roles, setRoles] = useState([]);
  const [universities, setUniversities] = useState([]);
  
  const [formData, setFormData] = useState({
    fullNameAr: '',
    fullNameEn: '',
    email: '',
    phone: '',
    staffRoleId: '',
    universityId: '',
    position: '',
    isActive: true
  });

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setError(null);
      try {
        const [staffData, rolesData, universitiesData] = await Promise.all([
          userService.getStaffById(id),
          userService.getRoles(),
          userService.getUniversities()
        ]);
        
        if (!staffData) {
          setError('Staff not found');
          return;
        }
        
        setRoles(rolesData);
        setUniversities(universitiesData);
        
        setFormData({
          fullNameAr: staffData.fullNameAr || '',
          fullNameEn: staffData.fullNameEn || '',
          email: staffData.email || '',
          phone: staffData.phone || '',
          staffRoleId: staffData.staffRoleId || '',
          universityId: staffData.universityId || '',
          position: staffData.position || '',
          isActive: staffData.isActive !== undefined ? staffData.isActive : true
        });
        
      } catch (err) {
        console.error('Error loading staff:', err);
        setError(err.message || 'Failed to load staff data');
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

  const validateForm = () => {
    const errors = {};
    
    if (!formData.fullNameAr?.trim()) errors.fullNameAr = 'Arabic name is required';
    if (!formData.fullNameEn?.trim()) errors.fullNameEn = 'English name is required';
    
    if (!formData.email?.trim()) {
      errors.email = 'Email is required';
    } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
      errors.email = 'Invalid email format';
    }
    
    if (!formData.staffRoleId) errors.staffRoleId = 'Role is required';
    if (!formData.universityId) errors.universityId = 'University is required';
    
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
        staffRoleId: formData.staffRoleId || null,
        position: formData.position || null,
        isActive: formData.isActive
      };
      
      const result = await userService.updateStaff(id, updateData);
      
      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate(`/admin/users/${id}`), 1500);
      } else {
        setError(result.message || 'Failed to update staff');
      }
    } catch (err) {
      console.error('Update error:', err);
      setError(err.message || 'An error occurred while updating');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSpinner fullPage message="Loading staff data..." />;

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
              <h1>Edit Staff Member</h1>
              <div className="gold-line" />
              <p>Update staff information and employment details</p>
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
                    <input type="email" name="email" className={`form-input ${validationErrors.email ? 'error' : ''}`} value={formData.email} onChange={handleChange} placeholder="staff@university.edu" />
                    <Mail size={18} className="input-icon" />
                  </div>
                  {validationErrors.email && <div className="error-message">{validationErrors.email}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">Phone</label>
                  <div className="input-wrapper">
                    <input type="tel" name="phone" className="form-input" value={formData.phone} onChange={handleChange} placeholder="Phone number" />
                    <Phone size={18} className="input-icon" />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Employment Information */}
          <div className="form-card">
            <div className="form-header">
              <div className="form-icon"><Briefcase size={22} color="#e0c06a" /></div>
              <div className="form-title-wrapper">
                <h3>Employment Information</h3>
                <p>Role, university, and position details</p>
              </div>
            </div>
            <div className="form-body">
              <div className="form-grid">
                <div className="form-group">
                  <label className="form-label">Role *</label>
                  <div className="input-wrapper">
                    <select name="staffRoleId" className={`form-select ${validationErrors.staffRoleId ? 'error' : ''}`} value={formData.staffRoleId} onChange={handleChange}>
                      <option value="">Select Role</option>
                      {roles.map(role => (<option key={role.id} value={role.id}>{role.name}</option>))}
                    </select>
                    <Shield size={18} className="input-icon" />
                  </div>
                  {validationErrors.staffRoleId && <div className="error-message">{validationErrors.staffRoleId}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">University *</label>
                  <div className="input-wrapper">
                    <select name="universityId" className={`form-select ${validationErrors.universityId ? 'error' : ''}`} value={formData.universityId} onChange={handleChange}>
                      <option value="">Select University</option>
                      {universities.map(u => (<option key={u.id} value={u.id}>{u.nameEn}</option>))}
                    </select>
                    <Building2 size={18} className="input-icon" />
                  </div>
                  {validationErrors.universityId && <div className="error-message">{validationErrors.universityId}</div>}
                </div>

                <div className="form-group">
                  <label className="form-label">Position</label>
                  <div className="input-wrapper">
                    <input type="text" name="position" className="form-input" value={formData.position} onChange={handleChange} placeholder="e.g., Department Head" />
                    <Briefcase size={18} className="input-icon" />
                  </div>
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

export default EditStaff;