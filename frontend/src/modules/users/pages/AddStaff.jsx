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
  Building2,
  Briefcase,
} from "lucide-react";

import userService from "../services/userService";
import "../styles/userForms.css";

const AddStaff = () => {
  const navigate = useNavigate();

  const steps = ["Basic Information", "Account Security", "Employment Information"];

  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const [roles, setRoles] = useState([]);
  const [universities, setUniversities] = useState([]);
  const [generatedCode, setGeneratedCode] = useState("");

  const [formData, setFormData] = useState({
    nationalId: "",
    fullNameAr: "",
    fullNameEn: "",
    email: "",
    password: "",
    confirmPassword: "",
    staffRoleId: "",
    universityId: "",
    position: "",
    phone: "",
  });

  const [errors, setErrors] = useState({});
  const [checkingEmail, setCheckingEmail] = useState(false);
  const [checkingNationalId, setCheckingNationalId] = useState(false);

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);

      try {
        const [rolesData, universitiesData] = await Promise.all([
          userService.getRoles(),
          userService.getUniversities(),
        ]);

        setRoles(rolesData);
        setUniversities(universitiesData);

        if (universitiesData.length > 0) {
          setFormData((prev) => ({
            ...prev,
            universityId: universitiesData[0].id,
          }));
        }
      } catch (error) {
        console.error("Error loading data:", error);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  useEffect(() => {
    const generateCode = async () => {
      if (!formData.universityId) {
        setGeneratedCode("");
        return;
      }

      try {
        const result = await userService.generateStaffCode(formData.universityId);
        setGeneratedCode(result.staffCode);
      } catch {
        setGeneratedCode(`STAFF${Date.now()}`);
      }
    };

    generateCode();
  }, [formData.universityId]);

  const handleChange = (e) => {
    const { name, value } = e.target;

    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));

    if (errors[name]) {
      setErrors((prev) => ({
        ...prev,
        [name]: "",
      }));
    }
  };

  const checkEmailUnique = async (email) => {
    if (!email) return true;

    setCheckingEmail(true);

    try {
      const result = await userService.checkEmailUnique(email, "Staff");
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
      const result = await userService.checkNationalIdUnique(nationalId, "Staff");
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
      if (!formData.staffRoleId) newErrors.staffRoleId = "Role is required";
      if (!formData.universityId) newErrors.universityId = "University is required";
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
      const selectedRole = roles.find((r) => r.id === formData.staffRoleId);
      const staffData = {
        nationalId: formData.nationalId,
        fullNameAr: formData.fullNameAr,
        fullNameEn: formData.fullNameEn,
        email: formData.email,
        password: formData.password,
        staffCode: generatedCode || `STAFF${Date.now()}`,
        staffRoleId: formData.staffRoleId,
        staffRoleName: selectedRole?.name,
        universityId: formData.universityId,
        position: formData.position || null,
        phone: formData.phone || null,
      };

      const result = await userService.createStaff(staffData);

      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate("/admin/staff"), 1600);
      } else {
        alert(result.message || "Error occurred while adding staff");
      }
    } catch (error) {
      if (error.response?.data?.errors) {
        const errorMessages = [];

        for (const [field, messages] of Object.entries(error.response.data.errors)) {
          if (Array.isArray(messages)) {
            errorMessages.push(`${field}: ${messages.join(", ")}`);
          } else {
            errorMessages.push(`${field}: ${messages}`);
          }
        }

        alert(`Validation errors:\n${errorMessages.join("\n")}`);
      } else if (error.response?.data?.message) {
        alert(error.response.data.message);
      } else {
        alert(error.message || "Error occurred while adding staff");
      }
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
              <h1>Add New Staff Member</h1>
              <div className="gold-line" />
              <p>Create a new staff account step by step</p>
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
                        placeholder="staff@university.edu"
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
                  <p>Password for staff login</p>
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
                  <Briefcase size={18} color="#e0c06a" />
                </div>

                <div className="form-title-wrapper">
                  <h3>Employment Information</h3>
                  <p>Role, university, position and staff code</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Role *</label>
                    <div className="input-wrapper">
                      <select
                        name="staffRoleId"
                        className={`form-select ${errors.staffRoleId ? "error" : ""}`}
                        value={formData.staffRoleId}
                        onChange={handleChange}
                      >
                        <option value="">Select Role</option>
                        {roles.map((role) => (
                          <option key={role.id} value={role.id}>
                            {role.name}
                          </option>
                        ))}
                      </select>
                      <Shield size={15} className="input-icon" />
                    </div>
                    {errors.staffRoleId && <span className="error-message">{errors.staffRoleId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">University *</label>
                    <div className="input-wrapper">
                      <select
                        name="universityId"
                        className={`form-select ${errors.universityId ? "error" : ""}`}
                        value={formData.universityId}
                        onChange={handleChange}
                      >
                        <option value="">Select University</option>
                        {universities.map((u) => (
                          <option key={u.id} value={u.id}>
                            {u.nameEn}
                          </option>
                        ))}
                      </select>
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.universityId && <span className="error-message">{errors.universityId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Staff Code</label>
                    <div className="code-display">
                      {generatedCode || "Select university to generate code"}
                    </div>
                    <span className="input-hint">Auto-generated based on university</span>
                  </div>

                  <div className="form-group">
                    <label className="form-label">Position</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="position"
                        className="form-input"
                        placeholder="e.g., Department Head"
                        value={formData.position}
                        onChange={handleChange}
                      />
                      <Briefcase size={15} className="input-icon" />
                    </div>
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
                {submitting ? "Creating..." : "Create Staff"}
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
            <div className="success-text">Staff Created Successfully!</div>
            <p style={{ color: "var(--text-muted)" }}>Redirecting to users list...</p>
          </div>
        </>
      )}
    </div>
  );
};

export default AddStaff;
