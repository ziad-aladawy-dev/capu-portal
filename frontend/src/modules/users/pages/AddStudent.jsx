import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  User, Mail, Lock, Phone, CheckCircle2, UserPlus, KeyRound, BookOpen, Building2, Calendar, Award, Hash
} from "lucide-react";
import userService from "../services/userService";
import "../styles/userForms.css";

const AddStudent = () => {
  const navigate = useNavigate();
  const steps = ["Basic Information", "Account Security", "Academic Information"];
  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const [faculties, setFaculties] = useState([]);
  const [filteredPrograms, setFilteredPrograms] = useState([]);
  const [filteredLevels, setFilteredLevels] = useState([]);

  const [formData, setFormData] = useState({
    studentCode: "",
    nationalId: "",
    name: "",
    email: "",
    password: "",
    confirmPassword: "",
    facultyId: "",
    programId: "",
    levelId: "",
    phone: "",
    dateOfBirth: "",
  });

  const [errors, setErrors] = useState({});
  const [checkingEmail, setCheckingEmail] = useState(false);
  const [checkingNationalId, setCheckingNationalId] = useState(false);

  useEffect(() => {
    const loadFaculties = async () => {
      setLoading(true);
      try {
        const data = await userService.getFaculties();
        setFaculties(data);
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
      }
    };
    loadFaculties();
  }, []);

  useEffect(() => {
    const loadPrograms = async () => {
      if (!formData.facultyId) {
        setFilteredPrograms([]);
        setFilteredLevels([]);
        setFormData(prev => ({ ...prev, programId: "", levelId: "" }));
        return;
      }
      try {
        const programs = await userService.getPrograms(formData.facultyId);
        setFilteredPrograms(programs);
        setFilteredLevels([]);
        setFormData(prev => ({ ...prev, programId: "", levelId: "" }));
      } catch (err) {
        console.error(err);
        setFilteredPrograms([]);
      }
    };
    loadPrograms();
  }, [formData.facultyId]);

  useEffect(() => {
    const loadLevels = async () => {
      if (!formData.programId) {
        setFilteredLevels([]);
        setFormData(prev => ({ ...prev, levelId: "" }));
        return;
      }
      try {
        const levels = await userService.getLevels(formData.programId);
        setFilteredLevels(levels);
        if (levels.length > 0 && !formData.levelId) {
          setFormData(prev => ({ ...prev, levelId: levels[0].id }));
        }
      } catch (err) {
        console.error(err);
        setFilteredLevels([]);
      }
    };
    loadLevels();
  }, [formData.programId]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    if (errors[name]) setErrors(prev => ({ ...prev, [name]: "" }));
  };

  const checkEmailUnique = async (email) => {
    if (!email) return true;
    setCheckingEmail(true);
    try {
      const result = await userService.checkEmailUnique(email, "Student");
      return result.isUnique;
    } catch { return false; }
    finally { setCheckingEmail(false); }
  };

  const checkNationalIdUnique = async (nationalId) => {
    if (!nationalId) return true;
    setCheckingNationalId(true);
    try {
      const result = await userService.checkNationalIdUnique(nationalId, "Student");
      return result.isUnique;
    } catch { return false; }
    finally { setCheckingNationalId(false); }
  };

  const validateStep = async () => {
    const newErrors = {};
    if (currentStep === 0) {
      if (!formData.nationalId) newErrors.nationalId = "National ID is required";
      else if (formData.nationalId.length !== 14) newErrors.nationalId = "Must be 14 digits";
      else if (!/^\d+$/.test(formData.nationalId)) newErrors.nationalId = "Only numbers";
      else {
        const isUnique = await checkNationalIdUnique(formData.nationalId);
        if (!isUnique) newErrors.nationalId = "National ID already exists";
      }
      if (!formData.name) newErrors.name = "Full name is required";
      if (!formData.email) newErrors.email = "Email is required";
      else if (!/\S+@\S+\.\S+/.test(formData.email)) newErrors.email = "Invalid email format";
      else {
        const isUnique = await checkEmailUnique(formData.email);
        if (!isUnique) newErrors.email = "Email already exists";
      }
      if (!formData.dateOfBirth) newErrors.dateOfBirth = "Date of birth is required";
    }
    if (currentStep === 1) {
      if (!formData.password) newErrors.password = "Password is required";
      else if (formData.password.length < 6) newErrors.password = "At least 6 characters";
      if (formData.password !== formData.confirmPassword) newErrors.confirmPassword = "Passwords do not match";
    }
    if (currentStep === 2) {
      if (!formData.facultyId) newErrors.facultyId = "Faculty is required";
      if (!formData.programId) newErrors.programId = "Program is required";
      if (!formData.levelId) newErrors.levelId = "Level is required";
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const nextStep = async () => {
    if (await validateStep()) setCurrentStep(prev => Math.min(prev + 1, steps.length - 1));
  };
  const prevStep = () => setCurrentStep(prev => Math.max(prev - 1, 0));

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!(await validateStep())) return;
    setSubmitting(true);
    const payload = {
      studentCode: formData.studentCode || undefined,
      nationalId: formData.nationalId,
      name: formData.name,
      birthDate: formData.dateOfBirth,
      phoneNumber: formData.phone || null,
      email: formData.email,
      password: formData.password,
      confirmPassword: formData.confirmPassword,
      structureNodeId: formData.levelId,
    };
    try {
      await userService.createStudent(payload);
      setShowSuccess(true);
      setTimeout(() => navigate("/admin/users"), 1600);
    } catch (err) {
      alert(err.response?.data?.message || err.message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <div className="form-loading">Loading...</div>;

  return (
    <div className="add-user-page">
      <div className="page-container compact-wizard">
        <div className="page-header compact-header">
          <div className="header-content">
            <div className="header-icon"><UserPlus size={22} /></div>
            <div className="header-text">
              <h1>Add New Student</h1>
              <div className="gold-line" />
              <p>Create a new student account step by step</p>
            </div>
          </div>
        </div>

        <div className="wizard-steps">
          {steps.map((step, idx) => (
            <button key={step} type="button" className={`wizard-step ${idx === currentStep ? "active" : ""} ${idx < currentStep ? "done" : ""}`} onClick={() => setCurrentStep(idx)}>
              <span>{idx + 1}</span>{step}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit}>
          {currentStep === 0 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><User size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Basic Information</h3><p>Personal details</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Student Code</label>
                    <div className="input-wrapper">
                      <input type="text" name="studentCode" value={formData.studentCode} onChange={handleChange} className="form-input" placeholder="Leave empty to auto-generate" />
                      <Hash size={15} className="input-icon" />
                    </div>
                    <span className="input-hint">Optional; will be auto-generated if left empty</span>
                  </div>
                  <div className="form-group">
                    <label className="form-label">National ID *</label>
                    <div className="input-wrapper">
                      <input type="text" name="nationalId" value={formData.nationalId} onChange={handleChange} className={`form-input ${errors.nationalId ? "error" : ""}`} placeholder="14-digit national ID" maxLength="14" />
                      <User size={15} className="input-icon" />
                    </div>
                    {checkingNationalId && <span className="input-hint">Checking...</span>}
                    {errors.nationalId && <span className="error-message">{errors.nationalId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Full Name *</label>
                    <div className="input-wrapper">
                      <input type="text" name="name" value={formData.name} onChange={handleChange} className={`form-input ${errors.name ? "error" : ""}`} placeholder="Full name" />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.name && <span className="error-message">{errors.name}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Email *</label>
                    <div className="input-wrapper">
                      <input type="email" name="email" value={formData.email} onChange={handleChange} className={`form-input ${errors.email ? "error" : ""}`} placeholder="student@university.edu" />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {checkingEmail && <span className="input-hint">Checking...</span>}
                    {errors.email && <span className="error-message">{errors.email}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Date of Birth *</label>
                    <div className="input-wrapper">
                      <input type="date" name="dateOfBirth" value={formData.dateOfBirth} onChange={handleChange} className={`form-input ${errors.dateOfBirth ? "error" : ""}`} />
                      <Calendar size={15} className="input-icon" />
                    </div>
                    {errors.dateOfBirth && <span className="error-message">{errors.dateOfBirth}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Phone</label>
                    <div className="input-wrapper">
                      <input type="tel" name="phone" value={formData.phone} onChange={handleChange} className="form-input" placeholder="Phone number" />
                      <Phone size={15} className="input-icon" />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {currentStep === 1 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><KeyRound size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Account Security</h3><p>Set password</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid two-cols">
                  <div className="form-group">
                    <label className="form-label">Password *</label>
                    <div className="input-wrapper">
                      <input type="password" name="password" value={formData.password} onChange={handleChange} className={`form-input ${errors.password ? "error" : ""}`} placeholder="Minimum 6 characters" />
                      <Lock size={15} className="input-icon" />
                    </div>
                    {errors.password && <span className="error-message">{errors.password}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Confirm Password *</label>
                    <div className="input-wrapper">
                      <input type="password" name="confirmPassword" value={formData.confirmPassword} onChange={handleChange} className={`form-input ${errors.confirmPassword ? "error" : ""}`} placeholder="Re-enter password" />
                      <Lock size={15} className="input-icon" />
                    </div>
                    {errors.confirmPassword && <span className="error-message">{errors.confirmPassword}</span>}
                  </div>
                </div>
              </div>
            </div>
          )}

          {currentStep === 2 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><BookOpen size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Academic Information</h3><p>Select Faculty → Program → Level</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Faculty *</label>
                    <div className="input-wrapper">
                      <select name="facultyId" value={formData.facultyId} onChange={handleChange} className={`form-select ${errors.facultyId ? "error" : ""}`}>
                        <option value="">Select Faculty</option>
                        {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
                      </select>
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.facultyId && <span className="error-message">{errors.facultyId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Program *</label>
                    <div className="input-wrapper">
                      <select name="programId" value={formData.programId} onChange={handleChange} disabled={!formData.facultyId} className={`form-select ${errors.programId ? "error" : ""}`}>
                        <option value="">Select Program</option>
                        {filteredPrograms.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                      </select>
                      <BookOpen size={15} className="input-icon" />
                    </div>
                    {errors.programId && <span className="error-message">{errors.programId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Level *</label>
                    <div className="input-wrapper">
                      <select name="levelId" value={formData.levelId} onChange={handleChange} disabled={!formData.programId} className={`form-select ${errors.levelId ? "error" : ""}`}>
                        <option value="">Select Level</option>
                        {filteredLevels.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                      </select>
                      <Award size={15} className="input-icon" />
                    </div>
                    {errors.levelId && <span className="error-message">{errors.levelId}</span>}
                  </div>
                </div>
              </div>
            </div>
          )}

          <div className="wizard-actions">
            <button type="button" className="wizard-btn secondary" onClick={prevStep} disabled={currentStep === 0}>Back</button>
            {currentStep < steps.length - 1 ? (
              <button type="button" className="wizard-btn primary" onClick={nextStep}>Next</button>
            ) : (
              <button type="submit" className="wizard-btn primary" disabled={submitting}>{submitting ? "Creating..." : "Create Student"}</button>
            )}
          </div>
        </form>
      </div>

      {showSuccess && (
        <>
          <div className="success-overlay" />
          <div className="success-message">
            <div className="success-icon"><CheckCircle2 size={38} color="#e0c06a" /></div>
            <div className="success-text">Student Created Successfully!</div>
            <p>Redirecting to users list...</p>
          </div>
        </>
      )}
    </div>
  );
};

export default AddStudent;