import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { UserCheck, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { useStaffRequests } from "../../hooks/useStaffRequests";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { REQUEST_STATUS } from "../../../../core/constants/requestStatus";
import { getLocalized } from "../../../../core/utils/getLocalized";
import "../../styles/admin/MyAssignedRequests.css";

const FILTER_STATUSES = [
  REQUEST_STATUS.Pending,
  REQUEST_STATUS.UnderReview,
  REQUEST_STATUS.MoreInfoRequired,
  REQUEST_STATUS.Approved,
  REQUEST_STATUS.Rejected,
  REQUEST_STATUS.PaymentPending,
  REQUEST_STATUS.ReadyForPickup,
  REQUEST_STATUS.Completed,
  REQUEST_STATUS.Cancelled,
];

const STATUS_NAMES = Object.fromEntries(
  Object.entries(REQUEST_STATUS).map(([name, value]) => [value, name])
);

const MyAssignedRequests = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { assignedToMe, loading, error } = useStaffRequests();
  const [filterStatus, setFilterStatus] = useState("");
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 10;

  const filtered = useMemo(() => {
    if (!filterStatus) return assignedToMe;
    return assignedToMe.filter((req) => Number(req.status) === Number(filterStatus));
  }, [assignedToMe, filterStatus]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paged = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  const statusLabel = (value) => {
    const name = STATUS_NAMES[value] || String(value);
    return t(name, { defaultValue: name.replace(/([A-Z])/g, " $1").trim() });
  };

  if (loading && assignedToMe.length === 0) return <LoadingSpinner />;
  if (error) return <div className="mar-error">{error}</div>;

  return (
    <div className="mar-container">
      <PageHeader
        icon={UserCheck}
        kicker={t("student_services")}
        title={t("my_queue")}
        subtitle={t("my_queue_subtitle")}
      />
      <div className="mar-filters-bar">
        <div className="mar-filter-info">
          <span className="mar-count">{assignedToMe.length} {t("assigned_requests")}</span>
        </div>
        <div className="mar-status-filter">
          <select value={filterStatus} onChange={(e) => { setFilterStatus(e.target.value); setPage(1); }}>
            <option value="">{t("all_statuses")}</option>
            {FILTER_STATUSES.map((value) => (
              <option key={value} value={value}>{statusLabel(value)}</option>
            ))}
          </select>
        </div>
      </div>
      {paged.length === 0 ? (
        <EmptyState icon={UserCheck} title={t("no_assigned_requests")} />
      ) : (
        <>
          <div className="mar-table-container">
            <table className="mar-table">
              <thead>
                <tr>
                  <th>{t("request_number")}</th>
                  <th>{t("student_name")}</th>
                  <th>{t("service_name")}</th>
                  <th>{t("status")}</th>
                  <th>{t("date")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {paged.map((req) => (
                  <tr key={req.id}>
                    <td className="mar-number">{req.requestNumber}</td>
                    <td>{getLocalized(req.studentName, i18n.language)}</td>
                    <td>{getLocalized(req.serviceName, i18n.language)}</td>
                    <td><StatusBadge status={req.status} /></td>
                    <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td>
                    <td>
                      <button className="btn-primary" onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}>
                        {t("review")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalPages > 1 && (
            <div className="mar-pagination">
              <button className="btn-icon" onClick={() => setPage(1)} disabled={page === 1}><ChevronsLeft size={16} /></button>
              <button className="btn-icon" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}><ChevronLeft size={16} /></button>
              <span className="mar-page-info">{t("page")} {page} / {totalPages}</span>
              <button className="btn-icon" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page === totalPages}><ChevronRight size={16} /></button>
              <button className="btn-icon" onClick={() => setPage(totalPages)} disabled={page === totalPages}><ChevronsRight size={16} /></button>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default MyAssignedRequests;
