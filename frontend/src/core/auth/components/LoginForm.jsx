import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useSearchParams } from "react-router-dom";
import { User, Lock, Eye, EyeOff, ArrowRight } from "lucide-react";
import { useAuth } from "../useAuth";

const SESSION_NOTICES = {
  revoked:
    "Your session was ended — you may have signed in elsewhere or changed your password. Please sign in again.",
  expired: "Your session expired. Please sign in again.",
};

// Egyptian National ID: exactly 14 numeric digits.
// Students authenticate with their National ID. Admins/staff use a
// generic identifier (National ID or username/email), so we keep their
// rule lenient to avoid locking valid accounts out.
function buildLoginSchema(type) {
  return z.object({
    nationalId:
      type === "student"
        ? z
            .string()
            .regex(/^[0-9]{14}$/, "National ID must be exactly 14 digits")
        : z.string().min(1, "This field is required"),
    // Login only checks presence — password complexity is enforced on
    // change/creation, never on login.
    password: z.string().min(1, "Password is required"),
    remember: z.boolean().optional(),
  });
}

function LoginForm({ type, redirectPath, onForgotClick }) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { login } = useAuth();

  const sessionNotice = SESSION_NOTICES[searchParams.get("session")] || "";

  const [showPassword, setShowPassword] = useState(false);
  const [submitError, setSubmitError] = useState("");

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(buildLoginSchema(type)),
    defaultValues: { nationalId: "", password: "", remember: false },
    mode: "onSubmit",
  });

  const onSubmit = async (values) => {
    setSubmitError("");
    try {
      await login(values.nationalId, values.password);
      navigate(redirectPath);
    } catch (err) {
      setSubmitError(
        err.response?.data?.message ||
          err.message ||
          "Invalid National ID or Password"
      );
    }
  };

  // Students: keep the field numeric-only and capped at 14 chars.
  const handleNationalIdInput = (e) => {
    if (type === "student") {
      e.target.value = e.target.value.replace(/\D/g, "").slice(0, 14);
    }
    setValue("nationalId", e.target.value, { shouldValidate: false });
  };

  return (
    <form className="login-form" onSubmit={handleSubmit(onSubmit)} noValidate>
      {sessionNotice && !submitError && (
        <div className="session-notice">{sessionNotice}</div>
      )}
      {submitError && <div className="error-message">{submitError}</div>}

      <div className="input-group">
        <label className="input-label">National ID</label>

        <div className="input-wrapper">
          <input
            type="text"
            inputMode={type === "student" ? "numeric" : "text"}
            className="form-input"
            placeholder="Enter your national ID"
            maxLength="14"
            aria-invalid={errors.nationalId ? "true" : "false"}
            {...register("nationalId", { onChange: handleNationalIdInput })}
          />

          <User size={18} className="input-icon" />
        </div>
        {errors.nationalId && (
          <span className="field-error">{errors.nationalId.message}</span>
        )}
      </div>

      <div className="input-group">
        <label className="input-label">Password</label>

        <div className="input-wrapper">
          <input
            type={showPassword ? "text" : "password"}
            className="form-input"
            placeholder="Enter your password"
            aria-invalid={errors.password ? "true" : "false"}
            {...register("password")}
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
        {errors.password && (
          <span className="field-error">{errors.password.message}</span>
        )}
      </div>

      <div className="form-options">
        <div className="checkbox-wrapper">
          <input
            type="checkbox"
            id={`${type}-remember`}
            className="checkbox-input"
            {...register("remember")}
          />

          <label htmlFor={`${type}-remember`} className="checkbox-label">
            Remember me
          </label>
        </div>

        <button type="button" className="forgot-link" onClick={onForgotClick}>
          Forgot Password?
        </button>
      </div>

      <button type="submit" className="submit-btn" disabled={isSubmitting}>
        {isSubmitting ? (
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
