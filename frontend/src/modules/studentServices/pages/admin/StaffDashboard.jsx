import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import {
  Package, TrendingUp, ClipboardList, Clock, Users, CheckCircle,
  CreditCard, DollarSign, Eye
} from "lucide-react";
import { useStaffStatistics } from "../../hooks/useStatistics";
import { getAllRequests } from "../../services/studentServicesService";
import PermissionGate from "../../../../core/auth/PermissionGate";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
import "../../styles/admin/StaffDashboard.css";

const StaffDashboard = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { stats, loading: statsLoading, refresh: refreshStats } = useStaffStatistics();
  const [recentRequests, setRecentRequests] = useState([]);
  const [loadingRequests, setLoadingRequests] = useState(true);

  useEffect(() => {
    loadRecentRequests();
  }, []);

  const loadRecentRequests = async () => {
    setLoadingRequests(true);
    try {
      const data = await getAllRequests();
      const recent = (data || []).slice(0, 5);
      setRecentRequests(recent);
    } catch (error) {
      console.error("Failed to load recent requests", error);
    } finally {
      setLoadingRequests(false);
    }
  };

  const statsCards = [
    { label: t("total_services"), value: stats?.totalServices || 0, icon: Package, color: "navy" },
    { label: t("active_services"), value: stats?.activeServices || 0, icon: TrendingUp, color: "gold" },
    { label: t("total_requests"), value: stats?.totalRequests || 0, icon: ClipboardList, color: "blue" },
    { label: t("pending_requests"), value: stats?.pendingRequests || 0, icon: Clock, color: "orange" },
    { label: t("awaiting_approval"), value: stats?.awaitingApproval || 0, icon: Users, color: "purple" },
    { label: t("completed_requests"), value: stats?.completedRequests || 0, icon: CheckCircle, color: "green" },
    { label: t("paid_requests"), value: stats?.paidRequests || 0, icon: CreditCard, color: "teal" },
    { label: t("revenue_summary"), value: `$${stats?.totalRevenue?.toFixed(2) || 0}`, icon: DollarSign, color: "gold" },
  ];

  const getStatusClass = (status) => {
    const map = {
      "UnderReview": "sd-status-review",
      "Pending": "sd-status-pending",
      "Approved": "sd-status-approved"
    };
    return map[status] || "sd-status-pending";
  };

  if (statsLoading && !stats) return <LoadingSpinner fullPage />;

  return (
    <div className="sd-container">
      <div className="sd-header">
        <div>
          <h1>{t("staff_dashboard")}</h1>
          <p>{t("overview_of_student_services")}</p>
        </div>
        <PermissionGate resource="student-services.requests" minLevel={1}>
          <button className="sd-view-all" onClick={() => navigate("/admin/student-services/requests")}>
            <Eye size={16} /> {t("view_all_requests")}
          </button>
        </PermissionGate>
      </div>

      <div className="sd-stats-grid">
        {statsCards.map((stat, idx) => (
          <div key={idx} className={`sd-stat-card ${stat.color}`}>
            <div className="sd-stat-icon"><stat.icon size={22} /></div>
            <div className="sd-stat-content">
              <span>{stat.label}</span>
              <h3>{stat.value}</h3>
            </div>
          </div>
        ))}
      </div>

      <div className="sd-recent-card">
        <h3>{t("recent_requests")}</h3>
        {loadingRequests ? (
          <LoadingSpinner />
        ) : recentRequests.length === 0 ? (
          <EmptyState message={t("no_requests")} />
        ) : (
          <div className="sd-table-wrapper">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>{t("request_id")}</th><th>{t("student")}</th><th>{t("service")}</th>
                  <th>{t("status")}</th><th>{t("date")}</th><th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {recentRequests.map(req => (
                  <tr key={req.id}>
                    <td className="sd-id">{req.id}</td>
                    <td>{req.studentName}</td>
                    <td>{req.serviceName}</td>
                    <td><span className={`sd-status-badge ${getStatusClass(req.status)}`}>{req.status}</span></td>
                    <td>{new Date(req.submittedDate).toLocaleDateString()}</td>
                    <td><PermissionGate resource="student-services.requests" minLevel={4}><button className="sd-view-btn" onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}>{t("view")}</button></PermissionGate></td>
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