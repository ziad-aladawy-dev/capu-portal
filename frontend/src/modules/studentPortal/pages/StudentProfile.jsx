import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Edit2, Save, X, AlertCircle } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import * as studentService from "../../../core/services/studentService";
import "../styles/studentProfile.css";

function StudentProfile() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [studentData, setStudentData] = useState(null);
  const [isEditing, setIsEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  const [formData, setFormData] = useState({
    firstName: "",
    lastName: "",
    email: "",
    phoneNumber: "",
    dateOfBirth: "",
    address: "",
    city: "",
    country: "",
    studentNumber: "",
  });

  useEffect(() => {
    const fetchStudentData = async () => {
      try {
        if (user?.id) {
          const data = await studentService.fetchStudentById(user.id);
          setStudentData(data);
          setFormData({
            firstName: data.firstName || "",
            lastName: data.lastName || "",
            email: data.email || "",
            phoneNumber: data.phoneNumber || "",
            dateOfBirth: data.dateOfBirth ? data.dateOfBirth.split("T")[0] : "",
            address: data.address || "",
            city: data.city || "",
            country: data.country || "",
            studentNumber: data.studentNumber || "",
          });
        }
      } catch (err) {
        setError(err.message || t("failed_to_load_data"));
      } finally {
        setLoading(false);
      }
    };

    fetchStudentData();
  }, [user?.id]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData({
      ...formData,
      [name]: value,
    });
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      await studentService.updateStudent(user.id, formData);
      setStudentData({ ...studentData, ...formData });
      setSuccess(t("profile_updated"));
      setIsEditing(false);
      setTimeout(() => setSuccess(null), 3000);
    } catch (err) {
      setError(err.message || t("failed_to_load_data"));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="student-profile-container">
        <div className="sp-loading">
          <div className="spinner"></div>
          <p>{t("loading_profile")}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="student-profile-container">
      <div className="sp-header">
        <h1>{t("student_profile")}</h1>
        <button
          className={`btn-edit-profile ${isEditing ? "editing" : ""}`}
          onClick={() => (isEditing ? handleSave() : setIsEditing(true))}
          disabled={saving}
        >
          {isEditing ? (
            <>
              <Save size={18} /> {saving ? t("saving") : t("save_changes")}
            </>
          ) : (
            <>
              <Edit2 size={18} /> {t("edit_profile")}
            </>
          )}
        </button>
        {isEditing && (
          <button
            className="btn-cancel-edit"
            onClick={() => setIsEditing(false)}
            disabled={saving}
          >
            <X size={18} /> {t("cancel")}
          </button>
        )}
      </div>

      {error && (
        <div className="alert alert-error">
          <AlertCircle size={18} />
          {error}
        </div>
      )}

      {success && (
        <div className="alert alert-success">
          {success}
        </div>
      )}

      {/* Profile Picture */}
      <div className="sp-avatar-section">
        <div className="sp-avatar">
          {formData.firstName?.charAt(0)?.toUpperCase() || "S"}
        </div>
        <div>
          <h2>
            {formData.firstName} {formData.lastName}
          </h2>
          <p>{formData.studentNumber}</p>
        </div>
      </div>

      {/* Form */}
      <div className="sp-form">
        <div className="form-section">
          <h3>{t("personal_details")}</h3>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="firstName">{t("first_name")}</label>
              <input
                id="firstName"
                type="text"
                name="firstName"
                value={formData.firstName}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
            <div className="form-group">
              <label htmlFor="lastName">{t("last_name")}</label>
              <input
                id="lastName"
                type="text"
                name="lastName"
                value={formData.lastName}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="dateOfBirth">{t("date_of_birth")}</label>
              <input
                id="dateOfBirth"
                type="date"
                name="dateOfBirth"
                value={formData.dateOfBirth}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
            <div className="form-group">
              <label htmlFor="studentNumber">{t("student_number")}</label>
              <input
                id="studentNumber"
                type="text"
                name="studentNumber"
                value={formData.studentNumber}
                onChange={handleChange}
                disabled
              />
            </div>
          </div>
        </div>

        <div className="form-section">
          <h3>{t("contact_details")}</h3>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="email">{t("email")}</label>
              <input
                id="email"
                type="email"
                name="email"
                value={formData.email}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
            <div className="form-group">
              <label htmlFor="phoneNumber">{t("phone_number")}</label>
              <input
                id="phoneNumber"
                type="tel"
                name="phoneNumber"
                value={formData.phoneNumber}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
          </div>
        </div>

        <div className="form-section">
          <h3>{t("address")}</h3>
          <div className="form-group">
            <label htmlFor="address">{t("address")}</label>
            <input
              id="address"
              type="text"
              name="address"
              value={formData.address}
              onChange={handleChange}
              disabled={!isEditing}
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="city">{t("city")}</label>
              <input
                id="city"
                type="text"
                name="city"
                value={formData.city}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
            <div className="form-group">
              <label htmlFor="country">{t("country")}</label>
              <input
                id="country"
                type="text"
                name="country"
                value={formData.country}
                onChange={handleChange}
                disabled={!isEditing}
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default StudentProfile;
