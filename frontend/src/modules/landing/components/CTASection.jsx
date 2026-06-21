import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { GraduationCap, ArrowRight } from "lucide-react";
import Reveal from "./Reveal";

function CTASection() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  return (
    <section className="section-container cta-section">
      <Reveal>
        <div className="cta-box">
          <div className="floating-shape cta-shape-one" />
          <div className="floating-shape delay-2 cta-shape-two" />

          <div className="cta-icon">
            <GraduationCap size={32} />
          </div>

          <h2>{t("landing.cta.title")}</h2>

          <p>{t("landing.cta.desc")}</p>

          <button className="primary-btn" onClick={() => navigate("/admin/login")}>
            {t("landing.cta.button")}
            <ArrowRight size={18} />
          </button>
        </div>
      </Reveal>
    </section>
  );
}

export default CTASection;