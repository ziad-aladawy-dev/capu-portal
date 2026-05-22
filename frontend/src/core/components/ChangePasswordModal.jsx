import PropTypes from "prop-types";
import { useState } from "react";
import { X, Lock, KeyRound, AlertTriangle, CheckCircle } from "lucide-react";
import * as authService from "../auth/authService";

function ChangePasswordModal({ onClose, onSuccess }) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (!currentPassword) { setError("Current password is required"); return; }
    if (!newPassword) { setError("New password is required"); return; }
    if (newPassword.length < 6) { setError("New password must be at least 6 characters"); return; }
    if (newPassword !== confirmPassword) { setError("Passwords do not match"); return; }
    if (newPassword === currentPassword) { setError("New password must differ from current password"); return; }

    setSaving(true);
    try {
      await authService.changePassword(currentPassword, newPassword);
      setSuccess(true);
      setTimeout(() => { onSuccess?.(); onClose(); }, 1500);
    } catch (err) {
      setError(err.response?.data?.message || err.message || "Failed to change password");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="ac-modal-overlay" onClick={onClose}>
      <div className="ac-modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 420 }}>
        <div className="ac-modal-header">
          <h2>Change Password</h2>
          <button className="ac-modal-close" onClick={onClose}><X size={16} /></button>
        </div>
        {success ? (
          <div style={{ padding: "32px 20px", textAlign: "center" }}>
            <CheckCircle size={40} color="#16a34a" style={{ marginBottom: 12 }} />
            <h3 style={{ color: "#1a1f5e", fontFamily: "'Space Mono', monospace", fontSize: 15, margin: 0 }}>Password Changed</h3>
            <p style={{ fontSize: 12, color: "#6b7280", marginTop: 6 }}>Your password has been updated successfully.</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <div className="ac-modal-body">
              {error && (
                <div className="ac-form-error-banner">
                  <AlertTriangle size={12} /> {error}
                </div>
              )}
              <div className="ac-form-group">
                <label>Current Password</label>
                <div className="cp-input-wrap">
                  <Lock size={14} />
                  <input
                    type="password" className="ac-form-input"
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                    placeholder="Enter current password"
                    autoFocus
                    style={{ paddingLeft: 34 }}
                  />
                </div>
              </div>
              <div className="ac-form-group">
                <label>New Password</label>
                <div className="cp-input-wrap">
                  <KeyRound size={14} />
                  <input
                    type="password" className="ac-form-input"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="At least 6 characters"
                    style={{ paddingLeft: 34 }}
                  />
                </div>
              </div>
              <div className="ac-form-group">
                <label>Confirm New Password</label>
                <div className="cp-input-wrap">
                  <KeyRound size={14} />
                  <input
                    type="password" className="ac-form-input"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="Re-enter new password"
                    style={{ paddingLeft: 34 }}
                  />
                </div>
              </div>
            </div>
            <div className="ac-modal-footer">
              <button type="button" className="ac-btn ac-btn-ghost" onClick={onClose} disabled={saving}>Cancel</button>
              <button type="submit" className="ac-btn ac-btn-primary" disabled={saving}>
                {saving ? "Changing…" : "Change Password"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

export default ChangePasswordModal;

ChangePasswordModal.propTypes = {
  onClose: PropTypes.func.isRequired,
  onSuccess: PropTypes.func,
};
