import { useState } from "react";
import { forgotPassword } from "../authService";
import "../styles/forgotPasswordModal.css";

function ForgotPasswordModal({ onClose }) {
  const [nationalId, setNationalId] = useState("");
  const [message, setMessage] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setMessage("");

    try {
      await forgotPassword(nationalId.trim());
      // The backend always responds the same way whether or not the account
      // exists, so the UI must not imply existence either.
      setSubmitted(true);
      setMessage(
        "If an account matches that National ID, a password reset link has been sent."
      );
    } catch (err) {
      setMessage(err.response?.data?.message || err.message || "Something went wrong. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h3>Reset Password</h3>

        {submitted ? (
          <>
            <p className="message">{message}</p>
            <div className="modal-actions">
              <button type="button" onClick={onClose}>Close</button>
            </div>
          </>
        ) : (
          <form onSubmit={handleSubmit}>
            <p className="modal-subtitle">
              Enter your National ID and we'll send a reset link to the email on file.
            </p>

            <div className="form-group">
              <label>National ID</label>
              <input
                type="text"
                name="nationalId"
                inputMode="numeric"
                maxLength="14"
                value={nationalId}
                onChange={(e) => setNationalId(e.target.value.replace(/\D/g, "").slice(0, 14))}
                required
                autoFocus
              />
            </div>

            <div className="modal-actions">
              <button type="button" onClick={onClose}>Cancel</button>
              <button type="submit" disabled={loading}>
                {loading ? "Sending..." : "Send Reset Link"}
              </button>
            </div>

            {message && <p className="message">{message}</p>}
          </form>
        )}
      </div>
    </div>
  );
}

export default ForgotPasswordModal;
