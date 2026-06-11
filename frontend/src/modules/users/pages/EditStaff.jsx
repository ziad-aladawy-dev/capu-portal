import React, { useState, useEffect, useRef, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useToast } from "../../../core/components/Toast";
import {
  User, Mail, Lock, Phone, Shield, CheckCircle2, Save, KeyRound,
  Building2, Briefcase, Calendar, Hash, Users, GraduationCap, Camera, RefreshCw,
  AlertCircle, Globe, UserCircle2, XCircle
} from "lucide-react";
import userService from "../services/userService";
import LoadingSpinner from "../../../core/components/LoadingSpinner";
import { parseLocalizedValue } from "../../../core/utils/getLocalized";
import { ScopeTreeModal } from "../../../core/components/ScopeTreeModal";
import { PhotoValidationOverlay } from "../../../core/components/PhotoValidationOverlay";
import { usePhotoValidator } from "../../../core/hooks/usePhotoValidator";
import "../styles/userForms.css";

const EditStaff = () => {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { id } = useParams();
  const navigate = useNavigate();
  const steps = [t("basic_information"), t("employment_information")];
  const [currentStep, setCurrentStep] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [showSuccess, setShowSuccess] = useState(false);
  const [showStructureModal, setShowStructureModal] = useState(false);

  const [formData, setFormData] = useState({
    nameAr: "",
    nameEn: "",
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
    gender: "",
    qualification: "",
  });

  const [photoFile, setPhotoFile] = useState(null);
  const [photoPreview, setPhotoPreview] = useState(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [showPhotoValidation, setShowPhotoValidation] = useState(false);
  const [pendingPhotoFile, setPendingPhotoFile] = useState(null);
  const fileInputRef = useRef(null);
  const photoValidator = usePhotoValidator();
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace('/api', '') || 'http://localhost:5256';
  const getPhotoUrl = (url) => {
    if (!url) return null;
    if (url.startsWith('http')) return url;
    return `${apiBaseUrl}${url}`;
  };
  const [roles, setRoles] = useState([]);
  const [selectedNodeName, setSelectedNodeName] = useState("");
  const [errors, setErrors] = useState({});

  const getLocalizedItemName = (item) => {
    return item?.localizedName || item?.name || "";
  };

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      try {
        const staff = await userService.getStaffById(id);

        const { ar: nameAr, en: nameEn } = parseLocalizedValue(staff.name);
        setFormData({
          nameAr: nameAr,
          nameEn: nameEn,
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
          gender: staff.gender || "",
          qualification: staff.qualification || "",
        });
        if (staff.photoUrl) {
          setPhotoPreview(getPhotoUrl(staff.photoUrl));
        }

        const nodeName = staff.structureNodeName || "";
        setSelectedNodeName(nodeName);
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
    if (errors[name]) setErrors(prev => ({ ...prev, [name]: "" }));
  };

  const handleNodeSelect = (node) => {
    setFormData(prev => ({ ...prev, structureNodeId: node.id }));
    setSelectedNodeName(getLocalizedItemName(node));
    setShowStructureModal(false);
  };

  const validateStep = () => {
    const newErrors = {};
    if (currentStep === 0) {
      if (!formData.nameAr) newErrors.nameAr = t("full_name_required");
      if (!formData.nationalId) newErrors.nationalId = t("national_id_required");
      if (!formData.email) newErrors.email = t("email_required");
      else if (!/\S+@\S+\.\S+/.test(formData.email)) newErrors.email = t("invalid_email");
      if (!formData.birthDate) newErrors.birthDate = t("dob_required");
    }
    if (currentStep === 1) {
      if (!formData.role) newErrors.role = t("role_required");
      if (!formData.structureNodeId) newErrors.structureNodeId = t("structure_node_required");
      if (formData.password && formData.password !== formData.confirmPassword)
        newErrors.confirmPassword = t("passwords_do_not_match");
      if (formData.password && formData.password.length < 6)
        newErrors.password = t("password_min_length");
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
      nameAr: formData.nameAr,
      nameEn: formData.nameEn || formData.nameAr,
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
      gender: formData.gender || null,
      qualification: formData.qualification || null,
    };
    try {
      await userService.updateStaff(id, updateData);
      if (photoFile) {
        setUploadingPhoto(true);
        await userService.uploadStaffPhoto(id, photoFile);
        setUploadingPhoto(false);
      }
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
        <div className="uf-hero">
          <div className="uf-hero-avatar"><UserCircle2 size={24} /></div>
          <div className="uf-hero-body">
            <h1>{t("edit_staff_member")}</h1>
            <p className="uf-hero-subtitle">{t("update_staff_information")}</p>
          </div>
          <div className="uf-hero-actions">
            <label className="account-status-toggle">
              <input type="checkbox" name="isActive" checked={formData.isActive} onChange={handleChange} />
              <span className="account-status-switch">
                <span className="account-status-dot">
                  {formData.isActive ? <CheckCircle2 size={11} /> : <XCircle size={11} />}
                </span>
              </span>
              <span>{formData.isActive ? t("active_account") : t("inactive_account")}</span>
            </label>
          </div>
        </div>

        <div className="wizard-steps">
          {steps.map((step, index) => (
            <button
              type="button"
              key={index}
              className={`wizard-step ${index === currentStep ? "active" : ""} ${index < currentStep ? "done" : ""}`}
              onClick={() => setCurrentStep(index)}
            >
              <span>{index + 1}</span>
              {step}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit}>
          {/* Step 0: Basic Information */}
          {currentStep === 0 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><User size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>{t("basic_information")}</h3></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">{t("national_id")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="nationalId"
                        value={formData.nationalId}
                        onChange={handleChange}
                        className={`form-input ${errors.nationalId ? "error" : ""}`}
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.nationalId && <span className="error-message">{errors.nationalId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("full_name_arabic")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="nameAr"
                        value={formData.nameAr}
                        onChange={handleChange}
                        className={`form-input ${errors.nameAr ? "error" : ""}`}
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {errors.nameAr && <span className="error-message">{errors.nameAr}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("full_name_english")}</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="nameEn"
                        value={formData.nameEn}
                        onChange={handleChange}
                        className="form-input"
                      />
                      <Globe size={15} className="input-icon" />
                    </div>
                    <span className="input-hint">{t("english_name_hint")}</span>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("email")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="email"
                        name="email"
                        value={formData.email}
                        onChange={handleChange}
                        className={`form-input ${errors.email ? "error" : ""}`}
                      />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {errors.email && <span className="error-message">{errors.email}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("date_of_birth")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="date"
                        name="birthDate"
                        value={formData.birthDate}
                        onChange={handleChange}
                        className={`form-input ${errors.birthDate ? "error" : ""}`}
                      />
                      <Calendar size={15} className="input-icon" />
                    </div>
                    {errors.birthDate && <span className="error-message">{errors.birthDate}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("gender")}</label>
                    <div className="input-wrapper">
                      <select name="gender" value={formData.gender} onChange={handleChange} className="form-select">
                        <option value="">{t("select_gender")}</option>
                        <option value="Male">{t("male")}</option>
                        <option value="Female">{t("female")}</option>
                      </select>
                      <Users size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("phone")}</label>
                    <div className="input-wrapper">
                      <input
                        type="tel"
                        name="phoneNumber"
                        value={formData.phoneNumber}
                        onChange={handleChange}
                        className="form-input"
                      />
                      <Phone size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("qualification")}</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="qualification"
                        value={formData.qualification}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("qualification_placeholder")}
                      />
                      <GraduationCap size={15} className="input-icon" />
                    </div>
                  </div>
                </div>
                <div className="photo-upload-section">
                  <div className="photo-upload-label">{t("profile_photo")}</div>
                  <div className="photo-upload-area">
                    {photoPreview ? (
                      <img src={photoPreview} alt="preview" className="photo-preview" />
                    ) : (
                      <div className="photo-placeholder">
                        <Camera size={32} />
                        <span>{t("click_to_upload")}</span>
                      </div>
                    )}
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      className="photo-file-input"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) {
                          if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
                            addToast(t('invalid_image_type'), "error");
                            return;
                          }
                          if (file.size > 5 * 1024 * 1024) {
                            addToast(t('image_too_large'), "error");
                            return;
                          }
                          setPhotoPreview(URL.createObjectURL(file));
                          setPendingPhotoFile(file);
                          setShowPhotoValidation(true);
                          photoValidator.reset();
                          photoValidator.loadModel().then(() => photoValidator.validate(file));
                        }
                      }}
                    />
                    {photoPreview && (
                      <button type="button" className="photo-remove-btn" onClick={() => setPhotoFile(null)}>
                        {t("remove")}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Step 1: Employment Information */}
          {currentStep === 1 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><Briefcase size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>{t("employment_information")}</h3></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">{t("role")} *</label>
                    <div className="input-wrapper">
                      <select
                        name="role"
                        value={formData.role}
                        onChange={handleChange}
                        className={`form-select ${errors.role ? "error" : ""}`}
                      >
                        <option value="">{t("select_role")}</option>
                        {roles.map(r => (
                          <option key={r.id} value={r.id}>{getLocalizedItemName(r)}</option>
                        ))}
                      </select>
                      <Shield size={15} className="input-icon" />
                    </div>
                    {errors.role && <span className="error-message">{errors.role}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("job_title")}</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="jobTitle"
                        value={formData.jobTitle}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("job_title_placeholder")}
                      />
                      <Briefcase size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("structure_node")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        value={selectedNodeName}
                        readOnly
                        onClick={() => setShowStructureModal(true)}
                        className={`form-input ${errors.structureNodeId ? "error" : ""}`}
                        placeholder={t("select_node")}
                      />
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.structureNodeId && <span className="error-message">{errors.structureNodeId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("password_optional")}</label>
                    <div className="input-wrapper">
                      <input
                        type="password"
                        name="password"
                        value={formData.password}
                        onChange={handleChange}
                        className={`form-input ${errors.password ? "error" : ""}`}
                        placeholder={t("password_placeholder")}
                      />
                      <Lock size={15} className="input-icon" />
                    </div>
                    {errors.password && <span className="error-message">{errors.password}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("confirm_password")}</label>
                    <div className="input-wrapper">
                      <input
                        type="password"
                        name="confirmPassword"
                        value={formData.confirmPassword}
                        onChange={handleChange}
                        className={`form-input ${errors.confirmPassword ? "error" : ""}`}
                        placeholder={t("confirm_password_placeholder")}
                      />
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
              {t("back")}
            </button>
            {currentStep < steps.length - 1 ? (
              <button type="button" className="wizard-btn primary" onClick={nextStep}>
                {t("next")}
              </button>
            ) : (
              <button type="submit" className="wizard-btn primary" disabled={submitting}>
                {submitting ? t("saving") : t("save_changes")}
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

      {showPhotoValidation && (
        <PhotoValidationOverlay
          results={photoValidator.results}
          previewUrl={photoPreview}
          isProcessing={photoValidator.isProcessing}
          error={photoValidator.error}
          onAccept={() => {
            setPhotoFile(pendingPhotoFile);
            setShowPhotoValidation(false);
          }}
          onReject={() => {
            setShowPhotoValidation(false);
            setPendingPhotoFile(null);
            setPhotoPreview(null);
            photoValidator.reset();
          }}
          onRetry={() => photoValidator.validate(pendingPhotoFile)}
        />
      )}

      {showSuccess && (
        <>
          <div className="success-overlay" />
          <div className="success-message">
            <div className="success-icon"><CheckCircle2 size={38} color="#e0c06a" /></div>
            <div className="success-text">{t("updated_successfully")}</div>
            <p>{t("redirecting_to_user_details")}</p>
          </div>
        </>
      )}
    </div>
  );
};

export default EditStaff;