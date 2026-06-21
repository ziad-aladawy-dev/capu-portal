import { useTranslation } from "react-i18next";
import { stats } from "../data/landingData";
import CountUp from "./CountUp";
import Reveal from "./Reveal";

function StatsSection() {
  const { t } = useTranslation();

  const labelMap = {
    "Students": "landing.stats.students",
    "Faculties": "landing.stats.faculties",
    "Programs": "landing.stats.programs",
    "Staff Members": "landing.stats.staff",
  };

  return (
    <section className="section-container stats-section">
      <Reveal>
        <div className="stats-grid">
          {stats.map((item, index) => (
            <div className="stat-card card-hover" key={index}>
              <div className="stat-icon">
                <item.icon size={24} />
              </div>
              <h3>
                <CountUp end={item.value} suffix={item.suffix} />
              </h3>
              <p>{t(labelMap[item.label] || item.label)}</p>
            </div>
          ))}
        </div>
      </Reveal>
    </section>
  );
}

export default StatsSection;