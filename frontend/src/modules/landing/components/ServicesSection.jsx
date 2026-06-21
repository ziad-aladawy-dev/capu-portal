import { useTranslation } from "react-i18next";
import { services } from "../data/landingData";
import Reveal from "./Reveal";

function ServicesSection() {
  const { t } = useTranslation();

  const serviceKeyMap = [
    "academic_calendar",
    "forms_documents",
    "student_guide",
    "e_learning",
    "library",
    "campus_services",
  ];

  return (
    <section className="section-container services-section">
      <Reveal>
        <div className="services-box">
          <div className="section-header">
            <h2>{t("landing.services.title")}</h2>
            <p>{t("landing.services.subtitle")}</p>
          </div>

          <div className="services-grid">
            {services.map((service, index) => {
              const key = serviceKeyMap[index] || `service_${index}`;
              return (
                <div className="service-item" key={index}>
                  <div className="service-icon">
                    <service.icon size={24} />
                  </div>
                  <h4>{t(`landing.services.${key}`)}</h4>
                </div>
              );
            })}
          </div>
        </div>
      </Reveal>
    </section>
  );
}

export default ServicesSection;