import { useState } from "react";
import { useTranslation } from "react-i18next";
import authService from "../../../core/auth/authService";
import "../styles/forgotPasswordModal.css";

function ForgotPasswordModal({ onClose }) {
  const { t } = useTranslation();
  const [formData, setFormData] = useState({
    universityCode: "",
    nationalId: "",
    email: "",
  });

  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setMessage("");
    setError("");

    try {
      await authService.forgotPassword({
        universityCode: formData.universityCode,
        nationalId: formData.nationalId,
        email: formData.email,
      });
      setMessage(t("reset_link_sent"));
      setTimeout(onClose, 3000);
    } catch (err) {
      setMessage(err.response?.data?.message || t("reset_link_failed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h3>{t("reset_password")}</h3>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>{t("university_code")}</label>
            <input
              type="text"
              name="universityCode"
              value={formData.universityCode}
              onChange={handleChange}
              required
            />
          </div>

          <div className="form-group">
            <label>{t("national_id")}</label>
            <input
              type="text"
              name="nationalId"
              value={formData.nationalId}
              onChange={handleChange}
              required
              maxLength="14"
            />
          </div>

          <div className="form-group">
            <label>{t("email")}</label>
            <input
              type="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              required
            />
          </div>

          <div className="modal-actions">
            <button type="button" onClick={onClose}>
              {t("cancel")}
            </button>

            <button type="submit" disabled={loading}>
              {loading ? t("sending") : t("send_reset_link")}
            </button>
          </div>

          {message && <p className="message">{message}</p>}
          {error && <p className="error-message">{error}</p>}
        </form>
      </div>
    </div>
  );
}

export default ForgotPasswordModal;