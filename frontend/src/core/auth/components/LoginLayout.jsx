import { useState } from "react";
import ForgotPasswordModal from "./ForgotPasswordModal";
import LoginForm from "./LoginForm";
import UniversityLogo from "./UniversityLogo";
import "../styles/login.css";

function LoginLayout({
  type,
  title,
  subtitle,
  redirectPath,
  contactText,
}) {
  const [showForgot, setShowForgot] = useState(false);

  return (
    <div className="login-page">
      <div className="bg-shape shape-1" />
      <div className="bg-shape shape-2" />

      <div className="login-container">
        <div className="left-panel">
          <UniversityLogo />
        </div>

        <div className="right-panel">
          <div className="form-header">
            <h1 className="welcome-text">{title}</h1>
            <div className="gold-line" />
            <p className="form-subtitle">{subtitle}</p>
          </div>

          <LoginForm
            type={type}
            redirectPath={redirectPath}
            onForgotClick={() => setShowForgot(true)}
          />

          <div className="signup-text">
            Don&apos;t have an account?{" "}
            <a href="#" className="signup-link">
              {contactText}
            </a>
          </div>
        </div>
      </div>

      {showForgot && (
        <ForgotPasswordModal onClose={() => setShowForgot(false)} />
      )}
    </div>
  );
}

export default LoginLayout;
