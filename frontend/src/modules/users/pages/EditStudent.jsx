import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  User, Mail, Phone, CheckCircle2, UserCircle2, BookOpen, Building2,
  Calendar, XCircle, AlertCircle, Award, Lock
} from "lucide-react";
import userService from "../services/userService";
import LoadingSpinner from "../components/LoadingSpinner";
import { useToast } from "../../../core/components/Toast";
import "../styles/userForms.css";

const EditStudent = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const steps = ["Basic Information", "Academic Information"];
  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [showSuccess, setShowSuccess] = useState(false);

  const [formData, setFormData] = useState({
    name: "",
    nationalId: "",
    birthDate: "",
    phoneNumber: "",
    email: "",
    structureNodeId: "",
    isActive: true,
    password: "",
    confirmPassword: "",
  });

  const [faculties, setFaculties] = useState([]);
  const [programs, setPrograms] = useState([]);
  const [levels, setLevels] = useState([]);
  const [selectedFacultyId, setSelectedFacultyId] = useState("");
  const [selectedProgramId, setSelectedProgramId] = useState("");
  const [errors, setErrors] = useState({});

  useEffect(() => {
    const loadStudent = async () => {
      setLoading(true);
      try {
        const student = await userService.getStudentById(id);
        setFormData({
          name: student.name || "",
          nationalId: student.nationalId || "",
          birthDate: student.birthDate ? student.birthDate.split("T")[0] : "",
          phoneNumber: student.phoneNumber || "",
          email: student.email || "",
          structureNodeId: student.structureNodeId || "",
          isActive: student.isActive !== undefined ? student.isActive : true,
          password: "",
          confirmPassword: "",
        });
        const facultiesData = await userService.getFaculties();
        setFaculties(facultiesData);
        if (student.facultyId) {
          setSelectedFacultyId(student.facultyId);
          const progs = await userService.getPrograms(student.facultyId);
          setPrograms(progs);
          if (student.programId) {
            setSelectedProgramId(student.programId);
            const lvls = await userService.getLevels(student.programId);
            setLevels(lvls);
          }
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    loadStudent();
  }, [id]);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({ ...prev, [name]: type === "checkbox" ? checked : value }));
  };

  const handleFacultyChange = async (e) => {
    const facultyId = e.target.value;
    setSelectedFacultyId(facultyId);
    setSelectedProgramId("");
    setFormData(prev => ({ ...prev, structureNodeId: "" }));
    if (facultyId) {
      const progs = await userService.getPrograms(facultyId);
      setPrograms(progs);
      setLevels([]);
    } else {
      setPrograms([]);
      setLevels([]);
    }
  };

  const handleProgramChange = async (e) => {
    const programId = e.target.value;
    setSelectedProgramId(programId);
    setFormData(prev => ({ ...prev, structureNodeId: "" }));
    if (programId) {
      const lvls = await userService.getLevels(programId);
      setLevels(lvls);
    } else {
      setLevels([]);
    }
  };

  const handleLevelChange = (e) => {
    setFormData(prev => ({ ...prev, structureNodeId: e.target.value }));
  };

  const validateStep = () => {
    const newErrors = {};
    if (currentStep === 0) {
      if (!formData.name) newErrors.name = "Name is required";
      if (!formData.nationalId) newErrors.nationalId = "National ID is required";
      if (!formData.email) newErrors.email = "Email is required";
      else if (!/\S+@\S+\.\S+/.test(formData.email)) newErrors.email = "Invalid email";
      if (!formData.birthDate) newErrors.birthDate = "Date of birth is required";
    }
    if (currentStep === 1) {
      if (!formData.structureNodeId) newErrors.structureNodeId = "Level is required";
      if (formData.password && formData.password !== formData.confirmPassword)
        newErrors.confirmPassword = "Passwords do not match";
      if (formData.password && formData.password.length < 6)
        newErrors.password = "Min 6 characters";
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const nextStep = () => {
    if (validateStep()) setCurrentStep(prev => prev + 1);
  };
  const prevStep = () => setCurrentStep(prev => prev - 1);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validateStep()) return;
    setSubmitting(true);
    const updateData = {
      name: formData.name,
      nationalId: formData.nationalId,
      birthDate: formData.birthDate,
      phoneNumber: formData.phoneNumber || null,
      email: formData.email,
      structureNodeId: formData.structureNodeId,
      isActive: formData.isActive,
      password: formData.password || null,
      confirmPassword: formData.confirmPassword || null,
    };
    try {
      await userService.updateStudent(id, updateData);
      setShowSuccess(true);
      setTimeout(() => navigate(`/admin/users/${id}`), 1400);
    } catch (err) {
      addToast(err.response?.data?.message || err.message, "error");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSpinner fullPage />;
  if (error) return (
    <div className="form-loading">
      <AlertCircle size={42} color="#dc2626" />
      <p>{error}</p>
    </div>
  );

  return (
    <div className="edit-user-page add-user-page">
      <div className="page-container compact-wizard">
        <div className="page-header compact-header wizard-header-with-status">
          <div className="header-content">
            <div className="header-icon"><UserCircle2 size={22} /></div>
            <div className="header-text">
              <h1>Edit Student</h1>
              <div className="gold-line" />
              <p>Update student information</p>
            </div>
          </div>
          <label className="account-status-toggle">
            <input type="checkbox" name="isActive" checked={formData.isActive} onChange={handleChange} />
            <span className="account-status-switch">
              <span className="account-status-dot">
                {formData.isActive ? <CheckCircle2 size={11} /> : <XCircle size={11} />}
              </span>
            </span>
            <span>{formData.isActive ? "Active Account" : "Inactive Account"}</span>
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
                <div className="form-icon"><User size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Basic Information</h3></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">National ID *</label>
                    <div className="input-wrapper">
                      <input type="text" name="nationalId" value={formData.nationalId} onChange={handleChange} className="form-input" />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.nationalId && <span className="error-message">{errors.nationalId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Full Name *</label>
                    <div className="input-wrapper">
                      <input type="text" name="name" value={formData.name} onChange={handleChange} className="form-input" />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.name && <span className="error-message">{errors.name}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Email *</label>
                    <div className="input-wrapper">
                      <input type="email" name="email" value={formData.email} onChange={handleChange} className="form-input" />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {errors.email && <span className="error-message">{errors.email}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Date of Birth *</label>
                    <div className="input-wrapper">
                      <input type="date" name="birthDate" value={formData.birthDate} onChange={handleChange} className="form-input" />
                      <Calendar size={15} className="input-icon" />
                    </div>
                    {errors.birthDate && <span className="error-message">{errors.birthDate}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Phone</label>
                    <div className="input-wrapper">
                      <input type="tel" name="phoneNumber" value={formData.phoneNumber} onChange={handleChange} className="form-input" />
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
                <div className="form-icon"><BookOpen size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Academic Information</h3></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Faculty</label>
                    <div className="input-wrapper">
                      <select value={selectedFacultyId} onChange={handleFacultyChange} className="form-select">
                        <option value="">Select Faculty</option>
                        {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
                      </select>
                      <Building2 size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Program</label>
                    <div className="input-wrapper">
                      <select value={selectedProgramId} onChange={handleProgramChange} disabled={!selectedFacultyId} className="form-select">
                        <option value="">Select Program</option>
                        {programs.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                      </select>
                      <BookOpen size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Level *</label>
                    <div className="input-wrapper">
                      <select value={formData.structureNodeId} onChange={handleLevelChange} disabled={!selectedProgramId} className="form-select">
                        <option value="">Select Level</option>
                        {levels.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                      </select>
                      <Award size={15} className="input-icon" />
                    </div>
                    {errors.structureNodeId && <span className="error-message">{errors.structureNodeId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Password (leave blank to keep current)</label>
                    <div className="input-wrapper">
                      <input type="password" name="password" value={formData.password} onChange={handleChange} className="form-input" />
                      <Lock size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Confirm Password</label>
                    <div className="input-wrapper">
                      <input type="password" name="confirmPassword" value={formData.confirmPassword} onChange={handleChange} className="form-input" />
                      <Lock size={15} className="input-icon" />
                    </div>
                    {errors.confirmPassword && <span className="error-message">{errors.confirmPassword}</span>}
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
      </div>

      {showSuccess && (
        <>
          <div className="success-overlay" />
          <div className="success-message">
            <div className="success-icon"><CheckCircle2 size={38} color="#e0c06a" /></div>
            <div className="success-text">Updated Successfully!</div>
            <p>Redirecting to user details...</p>
          </div>
        </>
      )}
    </div>
  );
};

export default EditStudent;