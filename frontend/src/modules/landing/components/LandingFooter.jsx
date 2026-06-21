import { useTranslation } from "react-i18next";

function LandingFooter() {
  const { t } = useTranslation();

  return (
    <footer className="landing-footer">
      <div className="footer-container">
        <div className="footer-copyright">
          <p>© {new Date().getFullYear()} {t("landing.footer.copyright")}</p>
        </div>
      </div>
    </footer>
  );
}

export default LandingFooter;