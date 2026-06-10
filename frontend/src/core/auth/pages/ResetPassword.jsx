import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Link, useSearchParams } from "react-router-dom";
import { Lock, Eye, EyeOff, CheckCircle } from "lucide-react";
import { resetPassword } from "../authService";
import "../styles/login.css";

const resetSchema = z
  .object({
    newPassword: z
      .string()
      .min(8, "At least 8 characters")
      .regex(/[A-Z]/, "Include an uppercase letter")
      .regex(/[a-z]/, "Include a lowercase letter")
      .regex(/[0-9]/, "Include a digit")
      .regex(/[^A-Za-z0-9]/, "Include a special character"),
    confirmPassword: z.string(),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

function ResetPassword() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") || "";

  const [showPassword, setShowPassword] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [success, setSuccess] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(resetSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
    mode: "onSubmit",
  });

  const onSubmit = async (values) => {
    setSubmitError("");
    try {
      await resetPassword(token, values.newPassword);
      setSuccess(true);
    } catch (err) {
      setSubmitError(
        err.response?.data?.message ||
          err.message ||
          "This reset link is invalid or has expired."
      );
    }
  };

  return (
    <div className="login-page">
      <div className="bg-shape shape-1" />
      <div className="bg-shape shape-2" />

      <div className="login-container">
        <div className="right-panel" style={{ margin: "0 auto" }}>
          <div className="form-header">
            <h1 className="welcome-text">Set a New Password</h1>
            <div className="gold-line" />
            <p className="form-subtitle">Choose a strong password for your account.</p>
          </div>

          {!token ? (
            <div className="error-message">
              Missing reset token. Please use the link from your reset email.
            </div>
          ) : success ? (
            <div style={{ textAlign: "center", padding: "12px 0" }}>
              <CheckCircle size={40} color="#16a34a" />
              <p style={{ margin: "12px 0" }}>Your password has been reset.</p>
              <Link to="/" className="submit-btn" style={{ display: "inline-flex" }}>
                Continue to login
              </Link>
            </div>
          ) : (
          <form className="login-form" onSubmit={handleSubmit(onSubmit)} noValidate>
            {submitError && <div className="error-message">{submitError}</div>}

            <div className="input-group">
              <label className="input-label">New Password</label>
              <div className="input-wrapper">
                <input
                  type={showPassword ? "text" : "password"}
                  className="form-input"
                  placeholder="Min 8 chars: upper, lower, number, symbol"
                  aria-invalid={errors.newPassword ? "true" : "false"}
                  {...register("newPassword")}
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
              {errors.newPassword && (
                <span className="field-error">{errors.newPassword.message}</span>
              )}
            </div>

            <div className="input-group">
              <label className="input-label">Confirm New Password</label>
              <div className="input-wrapper">
                <input
                  type="password"
                  className="form-input"
                  placeholder="Re-enter new password"
                  aria-invalid={errors.confirmPassword ? "true" : "false"}
                  {...register("confirmPassword")}
                />
                <Lock size={18} className="input-icon" />
              </div>
              {errors.confirmPassword && (
                <span className="field-error">{errors.confirmPassword.message}</span>
              )}
            </div>

            <button type="submit" className="submit-btn" disabled={isSubmitting}>
              {isSubmitting ? "Resetting…" : "Reset Password"}
            </button>
          </form>
          )}
        </div>
      </div>
    </div>
  );
}

export default ResetPassword;
