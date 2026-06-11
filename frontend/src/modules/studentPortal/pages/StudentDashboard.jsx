import DashboardHero from "../components/dashboard/DashboardHero";
import DashboardGrid from "../components/widgets/DashboardGrid";

/**
 * Student dashboard / command center. The hero handles greeting, semester
 * context, GPA and alert chips; the customizable widget grid below owns all
 * data fetching per widget (TanStack Query).
 */
function StudentDashboard() {
  return (
    <div className="student-dashboard">
      <DashboardHero />
      <DashboardGrid />
    </div>
  );
}

export default StudentDashboard;
