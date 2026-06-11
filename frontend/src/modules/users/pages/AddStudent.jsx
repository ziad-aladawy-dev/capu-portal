import React, { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useToast } from "../../../core/components/Toast";
import {
  User, Mail, Lock, Phone, CheckCircle2, UserPlus, KeyRound, BookOpen,
  Building2, Calendar, Award, Hash, Globe, Users, Camera, RefreshCw
} from "lucide-react";
import userService from "../services/userService";
import { PhotoValidationOverlay } from "../../../core/components/PhotoValidationOverlay";
import { usePhotoValidator } from "../../../core/hooks/usePhotoValidator";
import "../styles/userForms.css";

const AddStudent = () => {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const navigate = useNavigate();
  const steps = [t("basic_information"), t("account_security"), t("academic_information")];
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
    nameAr: "",
    nameEn: "",
    email: "",
    password: "",
    confirmPassword: "",
    facultyId: "",
    programId: "",
    levelId: "",
    phone: "",
    dateOfBirth: "",
    gender: "",
    guardianName: "",
    guardianPhone: "",
  });

  const [photoFile, setPhotoFile] = useState(null);
  const [photoPreview, setPhotoPreview] = useState(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [showPhotoValidation, setShowPhotoValidation] = useState(false);
  const [pendingPhotoFile, setPendingPhotoFile] = useState(null);
  const fileInputRef = useRef(null);
  const photoValidator = usePhotoValidator();

  const [errors, setErrors] = useState({});
  const [checkingEmail, setCheckingEmail] = useState(false);
  const [checkingNationalId, setCheckingNationalId] = useState(false);

  const getLocalizedItemName = (item) => {
    return item?.localizedName || item?.name || "";
  };

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
      if (!formData.nationalId) newErrors.nationalId = t("national_id_required");
      else if (formData.nationalId.length !== 14) newErrors.nationalId = t("national_id_length");
      else if (!/^\d+$/.test(formData.nationalId)) newErrors.nationalId = t("national_id_numbers_only");
      else {
        const isUnique = await checkNationalIdUnique(formData.nationalId);
        if (!isUnique) newErrors.nationalId = t("national_id_exists");
      }
      if (!formData.nameAr) newErrors.nameAr = t("full_name_required");
      if (!formData.email) newErrors.email = t("email_required");
      else if (!/\S+@\S+\.\S+/.test(formData.email)) newErrors.email = t("invalid_email");
      else {
        const isUnique = await checkEmailUnique(formData.email);
        if (!isUnique) newErrors.email = t("email_exists");
      }
      if (!formData.dateOfBirth) newErrors.dateOfBirth = t("dob_required");
    }
    if (currentStep === 1) {
      if (!formData.password) newErrors.password = t("password_required");
      else if (formData.password.length < 6) newErrors.password = t("password_min_length");
      if (formData.password !== formData.confirmPassword) newErrors.confirmPassword = t("passwords_do_not_match");
    }
    if (currentStep === 2) {
      if (!formData.facultyId) newErrors.facultyId = t("faculty_required");
      if (!formData.programId) newErrors.programId = t("program_required");
      if (!formData.levelId) newErrors.levelId = t("level_required");
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
      nameAr: formData.nameAr,
      nameEn: formData.nameEn || formData.nameAr,
      birthDate: formData.dateOfBirth,
      phoneNumber: formData.phone || null,
      email: formData.email,
      password: formData.password,
      confirmPassword: formData.confirmPassword,
      structureNodeId: formData.levelId,
      gender: formData.gender || null,
      guardianName: formData.guardianName || null,
      guardianPhone: formData.guardianPhone || null,
    };
    try {
      const result = await userService.createStudent(payload);
      if (photoFile && result?.id) {
        setUploadingPhoto(true);
        await userService.uploadStudentPhoto(result.id, photoFile);
        setUploadingPhoto(false);
      }
      setShowSuccess(true);
      setTimeout(() => navigate("/admin/users"), 1600);
    } catch (err) {
      addToast(err.response?.data?.message || err.message, "error");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <div className="form-loading">{t("loading")}...</div>;

  return (
    <div className="add-user-page">
      <div className="page-container compact-wizard">
        <div className="uf-hero">
          <div className="uf-hero-avatar"><UserPlus size={24} /></div>
          <div className="uf-hero-body">
            <h1>{t("add_new_student")}</h1>
            <p className="uf-hero-subtitle">{t("create_student_step_by_step")}</p>
          </div>
        </div>

        <div className="wizard-steps">
          {steps.map((step, idx) => (
            <button
              key={idx}
              type="button"
              className={`wizard-step ${idx === currentStep ? "active" : ""} ${idx < currentStep ? "done" : ""}`}
              onClick={() => setCurrentStep(idx)}
            >
              <span>{idx + 1}</span>{step}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit}>
          {/* Step 0: Basic Information */}
          {currentStep === 0 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><User size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>{t("basic_information")}</h3><p>{t("personal_details")}</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">{t("student_code")}</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="studentCode"
                        value={formData.studentCode}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("leave_empty_auto_generate")}
                      />
                      <Hash size={15} className="input-icon" />
                    </div>
                    <span className="input-hint">{t("optional_auto_generate")}</span>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("national_id")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="nationalId"
                        value={formData.nationalId}
                        onChange={handleChange}
                        className={`form-input ${errors.nationalId ? "error" : ""}`}
                        placeholder={t("national_id_placeholder")}
                        maxLength="14"
                      />
                      <User size={15} className="input-icon" />
                    </div>
                    {checkingNationalId && <span className="input-hint">{t("checking")}</span>}
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
                        placeholder={t("full_name_placeholder")}
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
                        placeholder={t("english_name_placeholder")}
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
                        placeholder={t("email_placeholder")}
                      />
                      <Mail size={15} className="input-icon" />
                    </div>
                    {checkingEmail && <span className="input-hint">{t("checking")}</span>}
                    {errors.email && <span className="error-message">{errors.email}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("date_of_birth")} *</label>
                    <div className="input-wrapper">
                      <input
                        type="date"
                        name="dateOfBirth"
                        value={formData.dateOfBirth}
                        onChange={handleChange}
                        className={`form-input ${errors.dateOfBirth ? "error" : ""}`}
                      />
                      <Calendar size={15} className="input-icon" />
                    </div>
                    {errors.dateOfBirth && <span className="error-message">{errors.dateOfBirth}</span>}
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
                        name="phone"
                        value={formData.phone}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("phone_placeholder")}
                      />
                      <Phone size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("guardian_name")}</label>
                    <div className="input-wrapper">
                      <input
                        type="text"
                        name="guardianName"
                        value={formData.guardianName}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("guardian_name_placeholder")}
                      />
                      <Users size={15} className="input-icon" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("guardian_phone")}</label>
                    <div className="input-wrapper">
                      <input
                        type="tel"
                        name="guardianPhone"
                        value={formData.guardianPhone}
                        onChange={handleChange}
                        className="form-input"
                        placeholder={t("guardian_phone_placeholder")}
                      />
                      <Phone size={15} className="input-icon" />
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
                      <button type="button" className="photo-remove-btn" onClick={() => { setPhotoFile(null); setPhotoPreview(null); }}>
                        {t("remove")}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Step 1: Account Security */}
          {currentStep === 1 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><KeyRound size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>{t("account_security")}</h3><p>{t("set_password")}</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid two-cols">
                  <div className="form-group">
                    <label className="form-label">{t("password")} *</label>
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
                    <label className="form-label">{t("confirm_password")} *</label>
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

          {/* Step 2: Academic Information */}
          {currentStep === 2 && (
            <div className="form-card wizard-card">
              <div className="form-header">
                <div className="form-icon"><BookOpen size={18} color="#e0c06a" /></div>
                <div className="form-title-wrapper"><h3>{t("academic_information")}</h3><p>{t("select_faculty_program_level")}</p></div>
              </div>
              <div className="form-body">
                <div className="form-grid compact-grid">
                  <div className="form-group">
                    <label className="form-label">{t("faculty")} *</label>
                    <div className="input-wrapper">
                      <select
                        name="facultyId"
                        value={formData.facultyId}
                        onChange={handleChange}
                        className={`form-select ${errors.facultyId ? "error" : ""}`}
                      >
                        <option value="">{t("select_faculty")}</option>
                        {faculties.map(f => (
                          <option key={f.id} value={f.id}>{getLocalizedItemName(f)}</option>
                        ))}
                      </select>
                      <Building2 size={15} className="input-icon" />
                    </div>
                    {errors.facultyId && <span className="error-message">{errors.facultyId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("program")} *</label>
                    <div className="input-wrapper">
                      <select
                        name="programId"
                        value={formData.programId}
                        onChange={handleChange}
                        disabled={!formData.facultyId}
                        className={`form-select ${errors.programId ? "error" : ""}`}
                      >
                        <option value="">{t("select_program")}</option>
                        {filteredPrograms.map(p => (
                          <option key={p.id} value={p.id}>{getLocalizedItemName(p)}</option>
                        ))}
                      </select>
                      <BookOpen size={15} className="input-icon" />
                    </div>
                    {errors.programId && <span className="error-message">{errors.programId}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">{t("level")} *</label>
                    <div className="input-wrapper">
                      <select
                        name="levelId"
                        value={formData.levelId}
                        onChange={handleChange}
                        disabled={!formData.programId}
                        className={`form-select ${errors.levelId ? "error" : ""}`}
                      >
                        <option value="">{t("select_level")}</option>
                        {filteredLevels.map(l => (
                          <option key={l.id} value={l.id}>{getLocalizedItemName(l)}</option>
                        ))}
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
                {submitting ? t("creating") : t("create_student")}
              </button>
            )}
          </div>
        </form>
      </div>

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
            <div className="success-text">{t("student_created_successfully")}</div>
            <p>{t("redirecting_to_users_list")}</p>
          </div>
        </>
      )}
    </div>
  );
};

export default AddStudent;