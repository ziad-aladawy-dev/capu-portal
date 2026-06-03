import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/student/MyRequests.css";

const MyRequests = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { requests, loading, loadRequests } = useStudentRequests();
  const [filterStatus, setFilterStatus] = useState("");

  useEffect(() => {
    loadRequests();
  }, [loadRequests]);

  const filteredRequests = filterStatus
    ? (requests || []).filter(req => req.status === filterStatus)
    : requests || [];

  if (loading && !requests.length) return <LoadingSpinner />;
  if (!filteredRequests.length) return <EmptyState message={t("no_requests")} />;

  return (
    <div className="mr-container">
      <h1>{t("my_requests")}</h1>
      <div className="mr-filter">
        <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
          <option value="">{t("all_statuses")}</option>
          <option value="Draft">{t("draft")}</option>
          <option value="Pending">{t("pending")}</option>
          <option value="UnderReview">{t("under_review")}</option>
          <option value="Approved">{t("approved")}</option>
          <option value="Rejected">{t("rejected")}</option>
          <option value="Completed">{t("completed")}</option>
        </select>
      </div>
      <div className="mr-table-wrapper">
        <table className="mr-table">
          <thead>
            <tr>
              <th>{t("request_id")}</th>
              <th>{t("service")}</th>
              <th>{t("submitted_date")}</th>
              <th>{t("status")}</th>
              <th>{t("payment_status")}</th>
              <th>{t("actions")}</th>
            </tr>
          </thead>
          <tbody>
            {filteredRequests.map(req => (
              <tr key={req.id}>
                <td className="mr-id">{req.id}</td>
                <td>{req.serviceName}</td>
                <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td>
                <td><StatusBadge status={req.status} /></td>
                <td><StatusBadge status={req.paymentStatus} /></td>
                <td>
                  <button onClick={() => navigate(`/student/requests/${req.id}`)}>
                    {t("view_details")}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MyRequests;