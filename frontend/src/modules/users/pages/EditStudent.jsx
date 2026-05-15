import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  User,
  Mail,
  Phone,
  Shield,
  CheckCircle2,
  UserCircle2,
  BookOpen,
  Building2,
  Calendar,
  Award,
  XCircle,
  AlertCircle,
} from "lucide-react";

import userService from "../services/userService";
import LoadingSpinner from "../components/LoadingSpinner";
import "../styles/userForms.css";

const EditStudent = () => {
  const { id } = useParams();
  const navigate = useNavigate();

  const steps = ["Basic Information", "Academic Information"];

  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [validationErrors, setValidationErrors] = useState({});
  const [showSuccess, setShowSuccess] = useState(false);

  const [faculties, setFaculties] = useState([]);
  const [filteredPrograms, setFilteredPrograms] = useState([]);
  const [filteredLevels, setFilteredLevels] = useState([]);

  const [formData, setFormData] = useState({
    fullNameAr: "",
    fullNameEn: "",
    email: "",
    phone: "",
    levelId: "",
    facultyId: "",
    programId: "",
    status: "Active",
    gpa: "",
    dateOfBirth: "",
    isActive: true,
  });

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setError(null);

      try {
        const [studentData, facultiesData] = await Promise.all([
          userService.getStudentById(id),
          userService.getFaculties(),
        ]);

        if (!studentData) {
          setError("Student not found");
          return;
        }

        setFaculties(facultiesData);

        // Resolve IDs from names since backend returns only name strings
        const matchedFaculty = facultiesData.find((f) => f.name === studentData.facultyName);
        const resolvedFacultyId = matchedFaculty?.id || "";

        let resolvedProgramId = "";
        let resolvedLevelId = studentData.levelId || "";
        if (resolvedFacultyId) {
          const programsData = await userService.getDepartments(resolvedFacultyId);
          setFilteredPrograms(programsData);
          const matchedProgram = programsData.find((p) => p.name === studentData.programName);
          resolvedProgramId = matchedProgram?.id || "";

          if (resolvedProgramId) {
            const levelsData = await userService.getLevels(resolvedProgramId);
            setFilteredLevels(levelsData);
          }
        }

        setFormData({
          fullNameAr: studentData.fullNameAr || "",
          fullNameEn: studentData.fullNameEn || "",
          email: studentData.email || "",
          phone: studentData.phone || "",
          levelId: resolvedLevelId,
          facultyId: resolvedFacultyId,
          programId: resolvedProgramId,
          status: studentData.status || "Active",
          gpa: studentData.gpa || "",
          dateOfBirth: studentData.dateOfBirth ? studentData.dateOfBirth.split("T")[0] : "",
          isActive: studentData.isActive !== undefined ? studentData.isActive : true,
        });
      } catch (err) {
        setError(err.message || "Failed to load student data");
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, [id]);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;

    setFormData((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));

    if (validationErrors[name]) {
      setValidationErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const handleFacultyChange = async (e) => {
    const facultyId = e.target.value;

    setFormData((prev) => ({ ...prev, facultyId, programId: "", levelId: "" }));

    if (!facultyId) {
      setFilteredPrograms([]);
      setFilteredLevels([]);
      return;
    }

    try {
      const programsData = await userService.getDepartments(facultyId);
      setFilteredPrograms(programsData);
      setFilteredLevels([]);
    } catch {
      setFilteredPrograms([]);
    }
  };

  const handleProgramChange = async (e) => {
    const programId = e.target.value;

    setFormData((prev) => ({ ...prev, programId, levelId: "" }));

    if (!programId) {
      setFilteredLevels([]);
      return;
    }

    try {
      const levelsData = await userService.getLevels(programId);
      setFilteredLevels(levelsData);
    } catch {
      setFilteredLevels([]);
    }
  };

  const validateStep = () => {
    const errors = {};

    if (currentStep === 0) {
      if (!formData.fullNameAr.trim()) errors.fullNameAr = "Arabic name is required";
      if (!formData.fullNameEn.trim()) errors.fullNameEn = "English name is required";

      if (!formData.email.trim()) {
        errors.email = "Email is required";
      } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
        errors.email = "Invalid email format";
      }
    }

    if (currentStep === 1) {
      if (!formData.levelId) errors.levelId = "Level is required";

      if (formData.gpa && (formData.gpa < 0 || formData.gpa > 5)) {
        errors.gpa = "GPA must be between 0 and 5";
      }
    }

    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const nextStep = () => {
    if (!validateStep()) return;
    setCurrentStep((prev) => Math.min(prev + 1, steps.length - 1));
  };

  const prevStep = () => {
    setCurrentStep((prev) => Math.max(prev - 1, 0));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!validateStep()) return;

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
        isActive: formData.isActive,
      };

      const result = await userService.updateStudent(id, updateData);

      if (result.success) {
        setShowSuccess(true);
        setTimeout(() => navigate(`/admin/users/${id}`), 1400);
      } else {
        setError(result.message || "Failed to update student");
      }
    } catch (err) {
      setError(err.message || "An error occurred while updating");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSpinner fullPage message="Loading student data..." />;

  if (error) {
    return (
      <div className="form-loading">
        <AlertCircle size={42} color="#dc2626" />
        <h2 style={{ color: "#dc2626", marginTop: 12 }}>Error</h2>
        <p>{error}</p>
      </div>
    );
  }

  return (
    <div className="edit-user-page add-user-page">
      <div className="page-container compact-wizard">
        <div className="page-header compact-header wizard-header-with-status">
          <div className="header-content">
            <div className="header-icon">
              <UserCircle2 size={22} />
            </div>

            <div className="header-text">
              <h1>Edit Student</h1>
              <div className="gold-line" />
              <p>Update student information step by step</p>
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
                {formData.isActive ? <CheckCircle2 size={11} /> : <XCircle size={11} />}
              </span>
            </span>

            <span className="account-status-text">
              {formData.isActive ? "Active Account" : "Inactive Account"}
            </span>
          </label>
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
                  <p>Personal identification details</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Full Name Arabic *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="fullNameAr"
                        className={`form-input ${validationErrors.fullNameAr ? "error" : ""}`}
                        value={formData.fullNameAr}
                        onChange={handleChange}
                        placeholder="الاسم بالعربية"
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {validationErrors.fullNameAr && <span className="error-message">{validationErrors.fullNameAr}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Full Name English *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="fullNameEn"
                        className={`form-input ${validationErrors.fullNameEn ? "error" : ""}`}
                        value={formData.fullNameEn}
                        onChange={handleChange}
                        placeholder="Name in English"
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {validationErrors.fullNameEn && <span className="error-message">{validationErrors.fullNameEn}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Email *</label>
                    <div className="input-wrapper">
                      <input
                        type="email"
                        name="email"
                        className={`form-input ${validationErrors.email ? "error" : ""}`}
                        value={formData.email}
                        onChange={handleChange}
                        placeholder="student@university.edu"
                      />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {validationErrors.email && <span className="error-message">{validationErrors.email}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Phone</label>
                    <div className="input-wrapper">
                      <input
                        type="tel"
                        name="phone"
                        className="form-input"
                        value={formData.phone}
                        onChange={handleChange}
                        placeholder="+20 100 123 4567"
                      />
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
                <div className="form-icon">
                  <BookOpen size={18} color="#e0c06a" />
                </div>

                <div className="form-title-wrapper">
                  <h3>Academic Information</h3>
                  <p>Faculty, program, level and academic status</p>
                </div>
              </div>

              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Faculty</label>
                    <div className="input-wrapper">
                      <select
                        name="facultyId"
                        className="form-select"
                        value={formData.facultyId}
                        onChange={handleFacultyChange}
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
                  </div>

                  <div className="form-group">
                    <label className="form-label">Program</label>
                    <div className="input-wrapper">
                      <select
                        name="programId"
                        className="form-select"
                        value={formData.programId}
                        onChange={handleProgramChange}
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
                  </div>

                  <div className="form-group">
                    <label className="form-label">Level *</label>
                    <div className="input-wrapper">
                      <select
                        name="levelId"
                        className={`form-select ${validationErrors.levelId ? "error" : ""}`}
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
                    {validationErrors.levelId && <span className="error-message">{validationErrors.levelId}</span>}
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
                        className={`form-input ${validationErrors.gpa ? "error" : ""}`}
                        value={formData.gpa}
                        onChange={handleChange}
                        step="0.01"
                        min="0"
                        max="5"
                        placeholder="0.00 - 5.00"
                      />
                      <Award size={15} className="input-icon" />
                    </div>
                    {validationErrors.gpa && <span className="error-message">{validationErrors.gpa}</span>}
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
                {submitting ? "Saving..." : "Save Changes"}
              </button>
            )}
          </div>
        </form>

        {showSuccess && (
          <>
            <div className="success-overlay" onClick={() => setShowSuccess(false)} />
            <div className="success-message">
              <div className="success-icon">
                <CheckCircle2 size={38} color="#e0c06a" />
              </div>
              <div className="success-text">Updated Successfully!</div>
              <p style={{ color: "#6b7280" }}>Redirecting to user details...</p>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default EditStudent;
