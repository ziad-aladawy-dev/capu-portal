import React from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutDashboard } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { useStaffDashboard } from "../../hooks/useStaffDashboard";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import "../../styles/admin/StaffDashboard.css";

const StaffDashboard = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { stats, recentRequests, loading, error } = useStaffDashboard();

  const statsCards = [
    { label: "total_services", value: stats?.totalServices || 0, color: "navy" },
    { label: "active_services", value: stats?.activeServices || 0, color: "gold" },
    { label: "total_requests", value: stats?.totalRequests || 0, color: "blue" },
    { label: "pending_requests", value: stats?.pendingRequests || 0, color: "orange" },
    { label: "awaiting_approval", value: stats?.awaitingApproval || 0, color: "purple" },
    { label: "completed_requests", value: stats?.completedRequests || 0, color: "green" },
    { label: "paid_requests", value: stats?.paidRequests || 0, color: "teal" },
    { label: "revenue_summary", value: `$${stats?.totalRevenue?.toFixed(2) || 0}`, color: "gold" },
  ];

  if (loading && !stats) return <LoadingSpinner fullPage />;
  if (error) return <div className="error-state">{error}</div>;

  return (
    <div className="sd-container">
      <PageHeader icon={LayoutDashboard} title={t("staff_dashboard")} subtitle={t("overview_of_student_services")} actions={<button className="sd-view-all" onClick={() => navigate("/admin/student-services/requests")}>{t("view_all_requests")}</button>} />
      <div className="sd-stats-grid">
        {statsCards.map((stat, idx) => (
          <div key={idx} className={`sd-stat-card ${stat.color}`}>
            <div className="sd-stat-content"><span>{t(stat.label)}</span><h3>{stat.value}</h3></div>
          </div>
        ))}
      </div>
      <div className="sd-recent-card">
        <h3>{t("recent_requests")}</h3>
        {recentRequests.length === 0 ? <EmptyState title={t("no_requests")} /> : (
          <div className="sd-table-wrapper"><table className="sd-table"><thead><tr><th>{t("request_number")}</th><th>{t("student")}</th><th>{t("service")}</th><th>{t("status")}</th><th>{t("date")}</th><th>{t("actions")}</th></tr></thead><tbody>
            {recentRequests.map(req => (
              <tr key={req.requestId}><td className="sd-number">{req.requestNumber}</td><td>{req.studentName}</td><td>{req.serviceName}</td><td><StatusBadge status={req.status} /></td><td>{new Date(req.submittedAt).toLocaleDateString()}</td><td><button className="sd-view-btn" onClick={() => navigate(`/admin/student-services/requests/${req.requestId}`)}>{t("view")}</button></td></tr>
            ))}
          </tbody></table></div>
        )}
      </div>
    </div>
  );
};

export default StaffDashboard;