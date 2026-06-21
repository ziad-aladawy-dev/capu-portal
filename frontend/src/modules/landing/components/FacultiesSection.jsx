import { useTranslation } from "react-i18next";
import { faculties } from "../data/landingData";
import Reveal from "./Reveal";

function FacultiesSection() {
  const { t } = useTranslation();

  const keyMap = ["cs", "engineering", "business", "pharmacy", "science", "arts"];

  return (
    <section className="section-container faculties-section">
      <Reveal>
        <div className="section-header">
          <h2>{t("landing.faculties.title", "University Faculties")}</h2>
          <p>
            {t("landing.faculties.subtitle", "Explore a wide range of faculties designed to support academic excellence, innovation, and future career opportunities.")}
          </p>
        </div>
      </Reveal>

      <div className="faculties-grid">
        {faculties.map((faculty, index) => {
          const key = keyMap[index] || `faculty_${index}`;
          return (
            <Reveal key={index}>
              <div className="faculty-card">
                <div className="faculty-shape" />
                <div className="faculty-icon-wrap">
                  <faculty.icon size={28} />
                </div>
                <h3>{t(`landing.faculties.${key}`)}</h3>
                <p>{t(`landing.faculties.${key}_desc`)}</p>
              </div>
            </Reveal>
          );
        })}
      </div>
    </section>
  );
}

export default FacultiesSection;