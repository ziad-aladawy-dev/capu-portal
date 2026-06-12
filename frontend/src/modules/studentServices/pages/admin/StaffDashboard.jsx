
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutDashboard, ClipboardList } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { useStaffDashboard } from "../../hooks/useStaffDashboard";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import "../../styles/admin/StaffDashboard.css";

const StaffDashboard = () => {
  const { t, i18n } = useTranslation();
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
    { label: "revenue_summary", value: `${fmtAmount(stats?.totalRevenue)} EGP`, color: "gold" },
  ];

  if (loading && !stats) return <LoadingSpinner fullPage />;
  if (error) return <ErrorMessage message={error} />;

  return (
    <div className="sd-container">
      <PageHeader icon={LayoutDashboard} title={t("staff_dashboard")} subtitle={t("overview_of_student_services")} actions={<button className="btn-primary" onClick={() => navigate("/admin/student-services/requests")}>{t("view_all_requests")}</button>} />
      <div className="sd-stats-grid">
        {statsCards.map((stat, idx) => (
          <div key={idx} className={`sd-stat-card ${stat.color}`}>
            <div className="sd-stat-content"><span>{t(stat.label)}</span><h3>{stat.value}</h3></div>
          </div>
        ))}
      </div>
      <div className="sd-recent-card">
        <h3>{t("recent_requests")}</h3>
        {recentRequests.length === 0 ? <EmptyState icon={ClipboardList} title={t("no_requests")} /> : (
          <div className="sd-table-wrapper"><table className="sd-table"><thead><tr><th>{t("request_number")}</th><th>{t("student")}</th><th>{t("service")}</th><th>{t("status")}</th><th>{t("date")}</th><th>{t("actions")}</th></tr></thead><tbody>
            {recentRequests.map(req => (
              <tr key={req.requestId}><td className="sd-number">{req.requestNumber}</td><td>{getLocalized(req.studentName, i18n.language)}</td><td>{getLocalized(req.serviceName, i18n.language)}</td><td><StatusBadge status={req.status} /></td><td>{new Date(req.submittedAt).toLocaleDateString()}</td><td><button className="btn-outline" onClick={() => navigate(`/admin/student-services/requests/${req.requestId}`)}>{t("view")}</button></td></tr>
            ))}
          </tbody></table></div>
        )}
      </div>
    </div>
  );
};

export default StaffDashboard;