import DashboardHeader from "../components/DashboardHeader";
import DashboardSearch from "../components/DashboardSearch";
import StatsCards from "../components/StatsCards";
import RecentActivities from "../components/RecentActivities";
import QuickActions from "../components/QuickActions";
import "../styles/adminDashboard.css";

function AdminDashboard() {
  return (
    <div className="admin-dashboard-page">
      <DashboardHeader />
      <DashboardSearch />
      <StatsCards />
      <div className="content-grid">
        <RecentActivities />
        <QuickActions />
      </div>
    </div>
  );
}

export default AdminDashboard;