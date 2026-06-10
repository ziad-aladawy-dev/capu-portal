import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Search, Filter, RefreshCw } from "lucide-react";
import { useStaffRequests } from "../../hooks/useStaffRequests";
import PermissionGate from "../../../../core/auth/PermissionGate";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/admin/RequestsManagement.css";

const RequestsManagement = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { requests, loading, loadAllRequests } = useStaffRequests();
  const [search, setSearch] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => {
    loadAllRequests();
  }, []);

  let filtered = requests || [];
  if (search) {
    filtered = filtered.filter(r => 
      r.serviceName?.toLowerCase().includes(search.toLowerCase()) || 
      r.id?.includes(search)
    );
  }
  if (filterStatus) {
    filtered = filtered.filter(r => r.status === filterStatus);
  }

  if (loading && !requests.length) return <LoadingSpinner />;

  return (
    <div className="rm-container">
      <div className="rm-header">
        <h1>{t("student_requests")}</h1>
        <button className="rm-refresh-btn" onClick={loadAllRequests}>
          <RefreshCw size={16} /> {t("refresh")}
        </button>
      </div>
      <div className="rm-filters-bar">
        <div className="rm-search-box">
          <Search size={16} />
          <input 
            type="text" 
            placeholder={t("search_by_student_or_id")} 
            value={search} 
            onChange={e => setSearch(e.target.value)} 
          />
        </div>
        <button className="rm-filter-toggle" onClick={() => setShowFilters(!showFilters)}>
          <Filter size={16} /> {t("filters")}
        </button>
        <button className="rm-clear-filters" onClick={() => { setSearch(""); setFilterStatus(""); }}>
          {t("clear_filters")}
        </button>
        <button className="rm-apply-btn" onClick={loadAllRequests}>
          {t("apply")}
        </button>
      </div>
      {showFilters && (
        <div className="rm-advanced-filters">
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
            <option value="">{t("all_statuses")}</option>
            <option value="Pending">Pending</option>
            <option value="UnderReview">Under Review</option>
            <option value="Approved">Approved</option>
            <option value="Rejected">Rejected</option>
            <option value="Completed">Completed</option>
          </select>
        </div>
      )}
      {filtered.length === 0 ? (
        <EmptyState message={t("no_requests")} />
      ) : (
        <div className="rm-table-container">
          <table className="rm-table">
            <thead>
              <tr>
                <th>{t("request_id")}</th>
                <th>{t("service")}</th>
                <th>{t("submitted_date")}</th>
                <th>{t("status")}</th>
                <th>{t("payment")}</th>
                <th>{t("actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(req => (
                <tr key={req.id}>
                  <td className="rm-id">{req.id}</td>
                  <td>{req.serviceName}</td>
                  <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td>
                  <td><StatusBadge status={req.status} /></td>
                  <td><StatusBadge status={req.paymentStatus} /></td>
                  <td>
                    <PermissionGate resource="student-services.requests" minLevel={4}>
                      <button 
                        className="rm-review-btn" 
                        onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}
                      >
                        {t("review")}
                      </button>
                    </PermissionGate>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default RequestsManagement;