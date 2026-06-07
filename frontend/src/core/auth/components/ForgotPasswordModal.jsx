import { useState } from "react";
import { forgotPassword } from "../authService";
import "../styles/forgotPasswordModal.css";

function ForgotPasswordModal({ onClose }) {
  const [formData, setFormData] = useState({
    universityCode: "",
    nationalId: "",
    email: "",
  });

  const [message, setMessage] = useState("");
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

    try {
      await forgotPassword(formData);
      setMessage("Reset link sent to your email. Please check your inbox.");
      setTimeout(onClose, 3000);
    } catch (err) {
      setMessage(err.message || "Failed to send reset link");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h3>Reset Password</h3>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>University Code</label>
            <input
              type="text"
              name="universityCode"
              value={formData.universityCode}
              onChange={handleChange}
              required
            />
          </div>

          <div className="form-group">
            <label>National ID</label>
            <input
              type="text"
              name="nationalId"
              value={formData.nationalId}
              onChange={handleChange}
              required
            />
          </div>

          <div className="form-group">
            <label>Email</label>
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
              Cancel
            </button>

            <button type="submit" disabled={loading}>
              {loading ? "Sending..." : "Send Reset Link"}
            </button>
          </div>

          {message && <p className="message">{message}</p>}
        </form>
      </div>
    </div>
  );
}

export default ForgotPasswordModal;