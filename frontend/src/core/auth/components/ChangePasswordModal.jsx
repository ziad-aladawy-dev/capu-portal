import { useState } from "react";
import { Key, X, Eye, EyeOff, CheckCircle } from "lucide-react";
import api from "../../api/apiClient";
import "../styles/changePassword.css";

function ChangePasswordModal({ onClose, forced = false }) {
  const [form, setForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmNewPassword: "",
  });
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (error) setError("");
  };

  const validate = () => {
    if (!form.currentPassword) return "Current password is required";
    if (!form.newPassword) return "New password is required";
    // Password policy (spec 1.3): min 8, upper, lower, digit, special.
    if (form.newPassword.length < 8) return "New password must be at least 8 characters";
    if (!/[A-Z]/.test(form.newPassword)) return "New password must contain an uppercase letter";
    if (!/[a-z]/.test(form.newPassword)) return "New password must contain a lowercase letter";
    if (!/[0-9]/.test(form.newPassword)) return "New password must contain a digit";
    if (!/[^A-Za-z0-9]/.test(form.newPassword)) return "New password must contain a special character";
    if (form.newPassword !== form.confirmNewPassword) return "Passwords do not match";
    if (form.currentPassword === form.newPassword) return "New password must be different";
    return null;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    const err = validate();
    if (err) { setError(err); return; }

    setSaving(true);
    setError("");
    try {
      await api.post("/auth/change-password", {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
        confirmNewPassword: form.confirmNewPassword,
      });
      setSuccess(true);
      setTimeout(() => onClose(), 1800);
    } catch (err) {
      setError(err.response?.data?.message || err.response?.data?.Message || "Failed to change password");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="cpw-overlay" onClick={forced ? undefined : onClose}>
      <div className="cpw-modal" onClick={(e) => e.stopPropagation()}>
        <div className="cpw-header">
          <Key size={18} />
          <h2>Change Password</h2>
          {!forced && (
            <button className="cpw-close" onClick={onClose}><X size={16} /></button>
          )}
        </div>

        {forced && !success && (
          <div className="cpw-forced-note">
            Your password has expired. Please set a new password to continue.
          </div>
        )}

        {success ? (
          <div className="cpw-success">
            <CheckCircle size={36} />
            <p>Password changed successfully!</p>
            <span>You may need to sign in again.</span>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <div className="cpw-body">
              {error && <div className="cpw-error">{error}</div>}

              <div className="cpw-field">
                <label>Current Password</label>
                <div className="cpw-input-wrap">
                  <input
                    type={showCurrent ? "text" : "password"}
                    name="currentPassword"
                    value={form.currentPassword}
                    onChange={handleChange}
                    placeholder="Enter current password"
                    autoFocus
                  />
                  <button type="button" className="cpw-eye" onClick={() => setShowCurrent(!showCurrent)}>
                    {showCurrent ? <EyeOff size={14} /> : <Eye size={14} />}
                  </button>
                </div>
              </div>

              <div className="cpw-field">
                <label>New Password</label>
                <div className="cpw-input-wrap">
                  <input
                    type={showNew ? "text" : "password"}
                    name="newPassword"
                    value={form.newPassword}
                    onChange={handleChange}
                    placeholder="Min 8 chars: upper, lower, number, symbol"
                  />
                  <button type="button" className="cpw-eye" onClick={() => setShowNew(!showNew)}>
                    {showNew ? <EyeOff size={14} /> : <Eye size={14} />}
                  </button>
                </div>
              </div>

              <div className="cpw-field">
                <label>Confirm New Password</label>
                <input
                  type="password"
                  name="confirmNewPassword"
                  value={form.confirmNewPassword}
                  onChange={handleChange}
                  placeholder="Re-enter new password"
                />
              </div>
            </div>

            <div className="cpw-footer">
              {!forced && (
                <button type="button" className="cpw-btn cpw-btn-cancel" onClick={onClose}>Cancel</button>
              )}
              <button type="submit" className="cpw-btn cpw-btn-save" disabled={saving}>
                {saving ? "Saving…" : "Change Password"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

export default ChangePasswordModal;
