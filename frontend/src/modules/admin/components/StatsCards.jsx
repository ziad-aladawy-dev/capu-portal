import { stats, iconColors } from "../data/dashboardData";

function StatsCards() {
  return (
    <div className="stats-grid">
      {stats.map((item, index) => (
        <div className={`stat-card anim-s${index}`} key={index}>
          <div className="stat-card-top">
            <span>{item.label}</span>

            <div
              className="stat-icon"
              style={iconColors[item.iconClass]}
            >
              <item.icon size={17} />
            </div>
          </div>

          <h2>{item.value}</h2>

          <p style={{ color: item.trendColor }}>{item.trend}</p>
        </div>
      ))}
    </div>
  );
}

export default StatsCards;