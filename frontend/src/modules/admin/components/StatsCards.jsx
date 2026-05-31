import { useTranslation } from "react-i18next";
import { statsConfig, iconColors, statsValues } from "../data/dashboardData";

function StatsCards() {
  const { t } = useTranslation();

  return (
    <div className="stats-grid">
      {statsConfig.map((item, index) => (
        <div className={`stat-card anim-s${index}`} key={item.key}>
          <div className="stat-card-top">
            <span>{t(item.key)}</span>
            <div className="stat-icon" style={iconColors[item.colorClass]}>
              <item.icon size={17} />
            </div>
          </div>
          <h2>{statsValues[item.key]}</h2>
          <p style={{ color: item.trendKey === "trend_stable" ? "#c9a84c" : "#16a34a" }}>
            {t(item.trendKey)}
          </p>
        </div>
      ))}
    </div>
  );
}

export default StatsCards;