
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutDashboard, ClipboardList, Briefcase, Clock, CheckCircle, Banknote, AlertCircle, TrendingUp, UserCheck } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { useStaffDashboard } from "../../hooks/useStaffDashboard";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import "../../styles/admin/StaffDashboard.css";

const STAT_CONFIG = [
  { key: "totalServices", icon: Briefcase, tone: "navy" },
  { key: "activeServices", icon: CheckCircle, tone: "gold" },
  { key: "totalRequests", icon: ClipboardList, tone: "blue" },
  { key: "pendingRequests", icon: Clock, tone: "orange" },
  { key: "awaitingApproval", icon: AlertCircle, tone: "purple" },
  { key: "completedRequests", icon: CheckCircle, tone: "green" },
  { key: "paidRequests", icon: Banknote, tone: "teal" },
  { key: "totalRevenue", icon: TrendingUp, tone: "gold", isRevenue: true },
];

const STAT_LABELS = {
  totalServices: "total_services",
  activeServices: "active_services",
  totalRequests: "total_requests",
  pendingRequests: "pending_requests",
  awaitingApproval: "awaiting_approval",
  completedRequests: "completed_requests",
  paidRequests: "paid_requests",
  totalRevenue: "revenue_summary",
};

const StaffDashboard = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { stats, recentRequests, assignedToMeCount, loading, error } = useStaffDashboard();

  if (loading && !stats) return <LoadingSpinner fullPage />;
  if (error) return <ErrorMessage message={error} />;

  return (
    <div className="sd-container">
      <PageHeader
        icon={LayoutDashboard}
        title={t("staff_dashboard")}
        subtitle={t("overview_of_student_services")}
        actions={
          <button className="btn-primary" onClick={() => navigate("/admin/student-services/requests")}>
            {t("view_all_requests")}
          </button>
        }
      />
      <div className="sd-stats-grid">
        {STAT_CONFIG.map((cfg) => {
          const Icon = cfg.icon;
          const raw = stats?.[cfg.key] ?? 0;
          const value = cfg.isRevenue ? `${fmtAmount(raw)} EGP` : raw;
          return (
            <div key={cfg.key} className={`sd-stat-card sd-tone-${cfg.tone}`}>
              <div className="sd-stat-icon-wrap">
                <Icon size={20} />
              </div>
              <div className="sd-stat-content">
                <span>{t(STAT_LABELS[cfg.key])}</span>
                <h3>{value}</h3>
              </div>
            </div>
          );
        })}
        <div
          className="sd-stat-card sd-tone-indigo sd-clickable"
          onClick={() => navigate("/admin/student-services/assigned-to-me")}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => e.key === "Enter" && navigate("/admin/student-services/assigned-to-me")}
        >
          <div className="sd-stat-icon-wrap">
            <UserCheck size={20} />
          </div>
          <div className="sd-stat-content">
            <span>{t("my_queue")}</span>
            <h3>{assignedToMeCount}</h3>
          </div>
        </div>
      </div>
      <div className="sd-recent-card">
        <h3>{t("recent_requests")}</h3>
        {recentRequests.length === 0 ? (
          <EmptyState icon={ClipboardList} title={t("no_requests")} />
        ) : (
          <div className="sd-table-wrapper">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>{t("request_number")}</th>
                  <th>{t("student")}</th>
                  <th>{t("service")}</th>
                  <th>{t("status")}</th>
                  <th>{t("date")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {recentRequests.map((req) => (
                  <tr key={req.requestId}>
                    <td className="sd-number">{req.requestNumber}</td>
                    <td>{getLocalized(req.studentName, i18n.language)}</td>
                    <td>{getLocalized(req.serviceName, i18n.language)}</td>
                    <td><StatusBadge status={req.status} /></td>
                    <td>{new Date(req.submittedAt).toLocaleDateString()}</td>
                    <td>
                      <button className="btn-outline" onClick={() => navigate(`/admin/student-services/requests/${req.requestId}`)}>
                        {t("view")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

export default StaffDashboard;
