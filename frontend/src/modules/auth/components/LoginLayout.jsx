import { useState } from "react";
import { useTranslation } from "react-i18next";
import ForgotPasswordModal from "./ForgotPasswordModal";
import LoginForm from "./LoginForm";
import UniversityLogo from "./UniversityLogo";
import LanguageSwitcher from "./LanguageSwitcher";
import "../styles/login.css";

function LoginLayout({ type, redirectPath }) {
  const { t } = useTranslation();
  const [showForgot, setShowForgot] = useState(false);

  const title = type === "admin" ? t("admin_portal") : t("student_portal");
  const subtitle = t("enter_credentials");
  const contactText = type === "admin" ? t("contact_system_admin") : t("contact_admission_office");

  return (
    <div className="login-page">
      <LanguageSwitcher />
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
            {type === "admin" ? t("no_account") : t("need_help")}{" "}
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
