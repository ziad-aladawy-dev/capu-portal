import { BarChart3 } from "lucide-react";

function DashboardHeader() {
  return (
    <div className="dashboard-header">
      <div className="dashboard-title-box">
        <div className="dashboard-title-icon">
          <BarChart3 size={24} />
        </div>

        <div>
          <h1>System Overview</h1>
          <div className="gold-line" />
        </div>
      </div>

      <div className="welcome-block">
        <p>Welcome back, Admin</p>
        <span>Here's what's happening today</span>
      </div>
    </div>
  );
}

export default DashboardHeader;