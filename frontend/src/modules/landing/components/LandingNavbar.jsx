import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Menu, X, Globe } from "lucide-react";

function LandingNavbar() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);

  const isMobile = windowWidth < 768;

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    const handleScroll = () => setScrolled(window.scrollY > 20);

    window.addEventListener("resize", handleResize);
    window.addEventListener("scroll", handleScroll);

    return () => {
      window.removeEventListener("resize", handleResize);
      window.removeEventListener("scroll", handleScroll);
    };
  }, []);

  const closeMobileMenu = () => setMobileMenuOpen(false);

  const goToLogin = () => {
    navigate("/admin/login");
    closeMobileMenu();
  };

  const toggleLanguage = () => {
    i18n.changeLanguage(i18n.language === "ar" ? "en" : "ar");
  };

  return (
    <header className={`landing-navbar ${scrolled ? "scrolled" : ""}`}>
      <div className="navbar-container">
        <div className="navbar-brand">
          <img
            src="/images/capital-uni-logo-nobackground.png"
            alt="Capital University"
            className="navbar-logo-img"
          />
        </div>

        {!isMobile && (
          <nav className="navbar-links">
            <span>{t("landing.nav.home")}</span>
            <span>{t("landing.nav.about")}</span>
            <span>{t("landing.nav.faculties")}</span>
            <span>{t("landing.nav.admissions")}</span>
            <span>{t("landing.nav.services")}</span>
            <span>{t("landing.nav.news")}</span>
            <span>{t("landing.nav.contact")}</span>
          </nav>
        )}

        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          {!isMobile && (
            <button className="login-btn" onClick={goToLogin}>
              {t("landing.nav.login")}
            </button>
          )}

          <button
            onClick={toggleLanguage}
            style={{
              background: "rgba(255,255,255,0.1)",
              border: "1px solid rgba(255,255,255,0.2)",
              borderRadius: "8px",
              color: "#fff",
              padding: "8px 12px",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "4px",
              fontSize: "13px",
              fontWeight: "600",
            }}
          >
            <Globe size={16} />
            {i18n.language === "ar" ? "EN" : "عربي"}
          </button>
        </div>

        {isMobile && (
          <button
            className="mobile-menu-btn"
            onClick={() => setMobileMenuOpen(true)}
          >
            <Menu size={22} />
          </button>
        )}
      </div>

      {isMobile && mobileMenuOpen && (
        <div className="mobile-menu">
          <div className="mobile-menu-header">
            <span>Menu</span>
            <button onClick={closeMobileMenu}>
              <X size={18} />
            </button>
          </div>

          <div className="mobile-menu-links">
            <span>{t("landing.nav.home")}</span>
            <span>{t("landing.nav.about")}</span>
            <span>{t("landing.nav.faculties")}</span>
            <span>{t("landing.nav.admissions")}</span>
            <span>{t("landing.nav.services")}</span>
            <span>{t("landing.nav.news")}</span>
            <span>{t("landing.nav.contact")}</span>

            <button onClick={goToLogin}>{t("landing.nav.login")}</button>

            <button
              onClick={toggleLanguage}
              style={{
                background: "rgba(255,255,255,0.1)",
                border: "none",
                borderRadius: "8px",
                color: "#fff",
                padding: "10px",
                cursor: "pointer",
                fontSize: "14px",
                fontWeight: "600",
              }}
            >
              {i18n.language === "ar" ? "English" : "العربية"}
            </button>
          </div>
        </div>
      )}
    </header>
  );
}

export default LandingNavbar;