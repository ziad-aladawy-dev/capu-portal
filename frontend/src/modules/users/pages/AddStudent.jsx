import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  User,
  Mail,
  Lock,
  Phone,
  Shield,
  CheckCircle2,
  UserPlus,
  KeyRound,
  BookOpen,
  Building2,
  Calendar,
  Award,
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
    nationalId: "",
    fullNameAr: "",
    fullNameEn: "",
    email: "",
    password: "",
    confirmPassword: "",
    facultyId: "",
    programId: "",
    levelId: "",
    phone: "",
    dateOfBirth: "",
    status: "Active",
    gpa: "",
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
        console.error("Error loading faculties:", error);
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
        setFormData((prev) => ({ ...prev, programId: "", levelId: "" }));
        return;
      }

      try {
        const programsData = await userService.getDepartments(formData.facultyId);
        setFilteredPrograms(programsData);
        setFilteredLevels([]);
        setFormData((prev) => ({ ...prev, programId: "", levelId: "" }));
      } catch {
        setFilteredPrograms([]);
      }
    };

    loadPrograms();
  }, [formData.facultyId]);

  useEffect(() => {
    const loadLevels = async () => {
      if (!formData.programId) {
        setFilteredLevels([]);
        setFormData((prev) => ({ ...prev, levelId: "" }));
        return;
      }

      try {
        const levelsData = await userService.getLevels(formData.programId);
        setFilteredLevels(levelsData);

        if (levelsData.length > 0 && !formData.levelId) {
          setFormData((prev) => ({ ...prev, levelId: levelsData[0].id }));
        }
      } catch {
        setFilteredLevels([]);
      }
    };

    loadLevels();
  }, [formData.programId]);

  const handleChange = (e) => {
    const { name, value } = e.target;

    setFormData((prev) => ({ ...prev, [name]: value }));

    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const checkEmailUnique = async (email) => {
    if (!email) return true;

    setCheckingEmail(true);

    try {
      const result = await userService.checkEmailUnique(email, "Student");
      return result.isUnique;
    } catch {
      return false;
    } finally {
      setCheckingEmail(false);
    }
  };

  const checkNationalIdUnique = async (nationalId) => {
    if (!nationalId) return true;

    setCheckingNationalId(true);

    try {
      const result = await userService.checkNationalIdUnique(nationalId, "Student");
      return result.isUnique;
    } catch {
      return false;
    } finally {
      setCheckingNationalId(false);
    }
  };

  const validateStep = async () => {
    const newErrors = {};

    if (currentStep === 0) {
      if (!formData.nationalId) {
        newErrors.nationalId = "National ID is required";
      } else if (formData.nationalId.length !== 14) {
        newErrors.nationalId = "National ID must be 14 digits";
      } else if (!/^\d+$/.test(formData.nationalId)) {
        newErrors.nationalId = "National ID must contain only numbers";
      } else {
        const isUnique = await checkNationalIdUnique(formData.nationalId);
        if (!isUnique) newErrors.nationalId = "National ID already exists";
      }

      if (!formData.fullNameAr.trim()) newErrors.fullNameAr = "Arabic name is required";
      if (!formData.fullNameEn.trim()) newErrors.fullNameEn = "English name is required";

      if (!formData.email) {
        newErrors.email = "Email is required";
      } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
        newErrors.email = "Invalid email format";
      } else {
        const isUnique = await checkEmailUnique(formData.email);
        if (!isUnique) newErrors.email = "Email already exists";
      }
    }

    if (currentStep === 1) {
      if (!formData.password) {
        newErrors.password = "Password is required";
      } else if (formData.password.length < 6) {
        newErrors.password = "Password must be at least 6 characters";
      }

      if (formData.password !== formData.confirmPassword) {
        newErrors.confirmPassword = "Passwords do not match";
      }
    }

    if (currentStep === 2) {
      if (!formData.facultyId) newErrors.facultyId = "Faculty is required";
      if (!formData.programId) newErrors.programId = "Program is required";
      if (!formData.levelId) newErrors.levelId = "Level is required";

      if (formData.gpa && (formData.gpa < 0 || formData.gpa > 5)) {
        newErrors.gpa = "GPA must be between 0 and 5";
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const nextStep = async () => {
    const isValid = await validateStep();

    if (!isValid) return;

    setCurrentStep((prev) => Math.min(prev + 1, steps.length - 1));
  };

  const prevStep = () => {
    setCurrentStep((prev) => Math.max(prev - 1, 0));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const isValid = await validateStep();
    if (!isValid) return;

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
        gpa: formData.gpa ? parseFloat(formData.gpa) : 0,
      };

      const result = await userService.createStudent(studentData);

      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate("/admin/users"), 1600);
      } else {
        alert(result.message || "Error occurred while adding student");
      }
    } catch (error) {
      alert(error.message || "Error occurred while adding student");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="form-loading">
        <div className="button-spinner"></div>
        <p>Loading data...</p>
      </div>
    );
  }

  return (
    <div className="add-user-page">
      <div className="page-container compact-wizard">
        <div className="page-header compact-header">
          <div className="header-content">
            <div className="header-icon">
              <UserPlus size={22} />
            </div>

            <div className="header-text">
              <h1>Add New Student</h1>
              <div className="gold-line" />
              <p>Create a new student account step by step</p>
            </div>
          </div>
        </div>

        <div className="wizard-steps">
          {steps.map((step, index) => (
            <button
              type="button"
              key={step}
              className={`wizard-step ${index === currentStep ? "active" : ""} ${
                index < currentStep ? "done" : ""
              }`}
              onClick={() => setCurrentStep(index)}
            >
              <span>{index + 1}</span>
              {step}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit}>
          {currentStep === 0 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon">
                  <User size={18} color="#e0c06a" />
                </div>

                <div className="form-title-wrapper">
                  <h3>Basic Information</h3>
                  <p>Personal identification and contact details</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">National ID *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="nationalId"
                        className={`form-input ${errors.nationalId ? "error" : ""}`}
                        placeholder="14-digit national ID"
                        value={formData.nationalId}
                        onChange={handleChange}
                        maxLength="14"
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {checkingNationalId && <span className="input-hint">Checking...</span>}
                    {errors.nationalId && <span className="error-message">{errors.nationalId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Full Name Arabic *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="fullNameAr"
                        className={`form-input ${errors.fullNameAr ? "error" : ""}`}
                        placeholder="الاسم بالعربية"
                        value={formData.fullNameAr}
                        onChange={handleChange}
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.fullNameAr && <span className="error-message">{errors.fullNameAr}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Full Name English *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="fullNameEn"
                        className={`form-input ${errors.fullNameEn ? "error" : ""}`}
                        placeholder="Name in English"
                        value={formData.fullNameEn}
                        onChange={handleChange}
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.fullNameEn && <span className="error-message">{errors.fullNameEn}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Email *</label>
                    <div className="input-wrapper">
                      <input
                        type="email"
                        name="email"
                        className={`form-input ${errors.email ? "error" : ""}`}
                        placeholder="student@university.edu"
                        value={formData.email}
                        onChange={handleChange}
                      />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {checkingEmail && <span className="input-hint">Checking...</span>}
                    {errors.email && <span className="error-message">{errors.email}</span>}
                  </div>
                </div>
              </div>
            </div>
          )}

          {currentStep === 1 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon">
                  <KeyRound size={18} color="#e0c06a" />
                </div>

                <div className="form-title-wrapper">
                  <h3>Account Security</h3>
                  <p>Password for student login</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid two-cols">
                  <div className="form-group">
                    <label className="form-label">Password *</label>
                    <div className="input-wrapper">
                      <input
                        type="password"
                        name="password"
                        className={`form-input ${errors.password ? "error" : ""}`}
                        placeholder="Minimum 6 characters"
                        value={formData.password}
                        onChange={handleChange}
                      />
                      <Lock size={15} className="input-icon" />
                    </div>
                    {errors.password && <span className="error-message">{errors.password}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Confirm Password *</label>
                    <div className="input-wrapper">
                      <input
                        type="password"
                        name="confirmPassword"
                        className={`form-input ${errors.confirmPassword ? "error" : ""}`}
                        placeholder="Re-enter password"
                        value={formData.confirmPassword}
                        onChange={handleChange}
                      />
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
                <div className="form-icon">
                  <BookOpen size={18} color="#e0c06a" />
                </div>

                <div className="form-title-wrapper">
                  <h3>Academic Information</h3>
                  <p>Faculty, program, level and academic details</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Faculty *</label>
                    <div className="input-wrapper">
                      <select
                        name="facultyId"
                        className={`form-select ${errors.facultyId ? "error" : ""}`}
                        value={formData.facultyId}
                        onChange={handleChange}
                      >
                        <option value="">Select Faculty</option>
                        {faculties.map((f) => (
                          <option key={f.id} value={f.id}>
                            {f.nameEn}
                          </option>
                        ))}
                      </select>
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.facultyId && <span className="error-message">{errors.facultyId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Program *</label>
                    <div className="input-wrapper">
                      <select
                        name="programId"
                        className={`form-select ${errors.programId ? "error" : ""}`}
                        value={formData.programId}
                        onChange={handleChange}
                        disabled={!formData.facultyId}
                      >
                        <option value="">Select Program</option>
                        {filteredPrograms.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.nameEn}
                          </option>
                        ))}
                      </select>
                      <BookOpen size={15} className="input-icon" />
                    </div>
                    {errors.programId && <span className="error-message">{errors.programId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Level *</label>
                    <div className="input-wrapper">
                      <select
                        name="levelId"
                        className={`form-select ${errors.levelId ? "error" : ""}`}
                        value={formData.levelId}
                        onChange={handleChange}
                        disabled={!formData.programId}
                      >
                        <option value="">Select Level</option>
                        {filteredLevels.map((l) => (
                          <option key={l.id} value={l.id}>
                            {l.nameEn}
                          </option>
                        ))}
                      </select>
                      <Award size={15} className="input-icon" />
                    </div>
                    {errors.levelId && <span className="error-message">{errors.levelId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Phone</label>
                    <div className="input-wrapper">
                      <input
                        type="tel"
                        name="phone"
                        className="form-input"
                        placeholder="Phone number"
                        value={formData.phone}
                        onChange={handleChange}
                      />
                      <Phone size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">Date of Birth</label>
                    <div className="input-wrapper">
                      <input
                        type="date"
                        name="dateOfBirth"
                        className="form-input"
                        value={formData.dateOfBirth}
                        onChange={handleChange}
                      />
                      <Calendar size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">Academic Status</label>
                    <div className="input-wrapper">
                      <select
                        name="status"
                        className="form-select"
                        value={formData.status}
                        onChange={handleChange}
                      >
                        <option value="Active">Active</option>
                        <option value="Graduated">Graduated</option>
                        <option value="Suspended">Suspended</option>
                        <option value="Probation">Probation</option>
                      </select>
                      <Shield size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">GPA</label>
                    <div className="input-wrapper">
                      <input
                        type="number"
                        name="gpa"
                        className={`form-input ${errors.gpa ? "error" : ""}`}
                        placeholder="0.00 - 5.00"
                        value={formData.gpa}
                        onChange={handleChange}
                        step="0.01"
                        min="0"
                        max="5"
                      />
                      <Award size={15} className="input-icon" />
                    </div>
                    {errors.gpa && <span className="error-message">{errors.gpa}</span>}
                  </div>
                </div>
              </div>
            </div>
          )}

          <div className="wizard-actions">
            <button
              type="button"
              className="wizard-btn secondary"
              onClick={prevStep}
              disabled={currentStep === 0}
            >
              Back
            </button>

            {currentStep < steps.length - 1 ? (
              <button type="button" className="wizard-btn primary" onClick={nextStep}>
                Next
              </button>
            ) : (
              <button type="submit" className="wizard-btn primary" disabled={submitting}>
                {submitting ? "Creating..." : "Create Student"}
              </button>
            )}
          </div>
        </form>
      </div>

      {showSuccess && (
        <>
          <div className="success-overlay" />
          <div className="success-message">
            <div className="success-icon">
              <CheckCircle2 size={38} color="#e0c06a" />
            </div>
            <div className="success-text">Student Created Successfully!</div>
            <p style={{ color: "var(--text-muted)" }}>Redirecting to users list...</p>
          </div>
        </>
      )}
    </div>
  );
};

export default AddStudent;