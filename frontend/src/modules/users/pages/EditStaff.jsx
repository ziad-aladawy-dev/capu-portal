import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  User, Mail, Phone, Shield, CheckCircle2, UserCircle2, Briefcase,
  Building2, XCircle, AlertCircle, Calendar, Lock
} from "lucide-react";
import userService from "../services/userService";
import LoadingSpinner from "../components/LoadingSpinner";
import { ScopeTreeModal } from "../../university/components/ScopeTreeModal";
import "../styles/userForms.css";

const EditStaff = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const steps = ["Basic Information", "Employment Information"];
  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [showSuccess, setShowSuccess] = useState(false);
  const [showStructureModal, setShowStructureModal] = useState(false);

  const [formData, setFormData] = useState({
    name: "",
    nationalId: "",
    birthDate: "",
    phoneNumber: "",
    email: "",
    role: "",
    jobTitle: "",
    structureNodeId: "",
    isActive: true,
    password: "",
    confirmPassword: "",
  });
  const [roles, setRoles] = useState([]);
  const [selectedNodeName, setSelectedNodeName] = useState("");
  const [errors, setErrors] = useState({});

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      try {
        const staff = await userService.getStaffById(id);
        setFormData({
          name: staff.name || "",
          nationalId: staff.nationalId || "",
          birthDate: staff.birthDate ? staff.birthDate.split("T")[0] : "",
          phoneNumber: staff.phoneNumber || "",
          email: staff.email || "",
          role: staff.role || "",
          jobTitle: staff.jobTitle || "",
          structureNodeId: staff.structureNodeId || "",
          isActive: staff.isActive !== undefined ? staff.isActive : true,
          password: "",
          confirmPassword: "",
        });
        setSelectedNodeName(`${staff.structureNodeName || "Node"} (${staff.structureNodeType || ""})`);
        const rolesData = await userService.getRoles();
        setRoles(rolesData);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    loadData();
  }, [id]);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({ ...prev, [name]: type === "checkbox" ? checked : value }));
  };

  const handleNodeSelect = (node) => {
    setFormData(prev => ({ ...prev, structureNodeId: node.id }));
    setSelectedNodeName(`${node.name} (${node.type})`);
    setShowStructureModal(false);
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
      if (!formData.role) newErrors.role = "Role is required";
      if (!formData.structureNodeId) newErrors.structureNodeId = "Structure node required";
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
      role: formData.role,
      jobTitle: formData.jobTitle || null,
      structureNodeId: formData.structureNodeId,
      isActive: formData.isActive,
      password: formData.password || null,
      confirmPassword: formData.confirmPassword || null,
    };
    try {
      await userService.updateStaff(id, updateData);
      setShowSuccess(true);
      setTimeout(() => navigate(`/admin/users/${id}`), 1400);
    } catch (err) {
      alert(err.response?.data?.message || err.message);
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
              <h1>Edit Staff Member</h1>
              <div className="gold-line" />
              <p>Update staff information</p>
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
                  </div>
                  <div className="form-group">
                    <label className="form-label">Full Name *</label>
                    <div className="input-wrapper">
                      <input type="text" name="name" value={formData.name} onChange={handleChange} className="form-input" />
                      <User size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Email *</label>
                    <div className="input-wrapper">
                      <input type="email" name="email" value={formData.email} onChange={handleChange} className="form-input" />
                      <Mail size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Date of Birth *</label>
                    <div className="input-wrapper">
                      <input type="date" name="birthDate" value={formData.birthDate} onChange={handleChange} className="form-input" />
                      <Calendar size={15} className="input-icon" />
                    </div>
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
                <div className="form-icon"><Briefcase size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>Employment Information</h3></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">Role *</label>
                    <div className="input-wrapper">
                      <select name="role" value={formData.role} onChange={handleChange} className="form-select">
                        <option value="">Select Role</option>
                        {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                      </select>
                      <Shield size={15} className="input-icon" />
                    </div>
                    {errors.role && <span className="error-message">{errors.role}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Job Title</label>
                    <div className="input-wrapper">
                      <input type="text" name="jobTitle" value={formData.jobTitle} onChange={handleChange} className="form-input" />
                      <Briefcase size={15} className="input-icon" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Structure Node *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        value={selectedNodeName}
                        readOnly
                        onClick={() => setShowStructureModal(true)}
                        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setShowStructureModal(true); } }}
                        className="form-input"
                        placeholder="Select node"
                        aria-label="Select structure node"
                      />
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.structureNodeId && <span className="error-message">{errors.structureNodeId}</span>}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Password (optional)</label>
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

      <ScopeTreeModal
        isOpen={showStructureModal}
        onClose={() => setShowStructureModal(false)}
        onSelect={handleNodeSelect}
        initialScopeId={formData.structureNodeId}
      />

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

export default EditStaff;