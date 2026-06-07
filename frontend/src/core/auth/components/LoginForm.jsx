import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Mail, Lock, Eye, EyeOff, ArrowRight } from "lucide-react";
import { useAuth } from "../useAuth";

function LoginForm({ type, redirectPath, onForgotClick }) {
  const navigate = useNavigate();
  const { login } = useAuth();

  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [formData, setFormData] = useState({
    nationalId: "",
    password: "",
    remember: false,
  });

  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;

    setFormData((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    setIsLoading(true);
    setError("");

    try {
      await login(formData.nationalId, formData.password);
      navigate(redirectPath);
    } catch (err) {
      setError(err.response?.data?.message || err.message || "Invalid National ID or Password");
      setIsLoading(false);
    }
  };

  return (
    <form className="login-form" onSubmit={handleSubmit}>
      {error && <div className="error-message">{error}</div>}

      <div className="input-group">
        <label className="input-label">National ID</label>

        <div className="input-wrapper">
          <input
            type="text"
            name="nationalId"
            className="form-input"
            placeholder="Enter your national ID"
            value={formData.nationalId}
            onChange={handleChange}
            required
            maxLength="14"
          />

          <Mail size={18} className="input-icon" />
        </div>
      </div>

      <div className="input-group">
        <label className="input-label">Password</label>

        <div className="input-wrapper">
          <input
            type={showPassword ? "text" : "password"}
            name="password"
            className="form-input"
            placeholder="Enter your password"
            value={formData.password}
            onChange={handleChange}
            required
          />

          <Lock size={18} className="input-icon" />

          <button
            type="button"
            className="password-toggle"
            onClick={() => setShowPassword(!showPassword)}
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        </div>
      </div>

      <div className="form-options">
        <div className="checkbox-wrapper">
          <input
            type="checkbox"
            name="remember"
            id={`${type}-remember`}
            className="checkbox-input"
            checked={formData.remember}
            onChange={handleChange}
          />

          <label htmlFor={`${type}-remember`} className="checkbox-label">
            Remember me
          </label>
        </div>

        <button type="button" className="forgot-link" onClick={onForgotClick}>
          Forgot Password?
        </button>
      </div>

      <button type="submit" className="submit-btn" disabled={isLoading}>
        {isLoading ? (
          <>
            <div className="spinner" />
            Logging in...
          </>
        ) : (
          <>
            Sign In
            <ArrowRight size={18} />
          </>
        )}
      </button>
    </form>
  );
}

export default LoginForm;
