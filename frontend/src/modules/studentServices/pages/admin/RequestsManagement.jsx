import { useState, useEffect, useContext } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Search, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ClipboardList } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { AuthContext } from "../../../../core/auth/AuthContext";
import { useStaffRequestsPaged } from "../../hooks/useStaffRequestsPaged";
import { assignRequest } from "../../services/studentServicesService";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { REQUEST_STATUS } from "../../../../core/constants/requestStatus";
import { getLocalized } from "../../../../core/utils/getLocalized";
import "../../styles/admin/RequestsManagement.css";

// Statuses offered in the filter — backend sends `status` as an int enum.
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

const REQUESTS_PAGED_KEY = "ss-requests-paged";

const RequestsManagement = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { user } = useContext(AuthContext);
  const queryClient = useQueryClient();
  const [filterStatus, setFilterStatus] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const { requests, loading, error, pagination, search, sortBy, ascending, changePage, changePageSize, applySearch, applySort, refresh } = useStaffRequestsPaged(10);

  const claimMutation = useMutation({
    mutationFn: (requestId) => assignRequest(requestId, user.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [REQUESTS_PAGED_KEY] }),
  });

  // Debounce the search box (~300ms) so we don't refetch on every keystroke.
  useEffect(() => {
    const handle = setTimeout(() => {
      if (searchInput !== search) applySearch(searchInput);
    }, 300);
    return () => clearTimeout(handle);
  }, [searchInput]); // eslint-disable-line react-hooks/exhaustive-deps

  const filteredRequests = filterStatus
    ? requests.filter(req => Number(req.status) === Number(filterStatus))
    : requests;
  const handleSort = (field) => { if (field === "requestnumber" || field === "studentname") applySort(field); };
  const getSortIcon = (field) => sortBy !== field ? null : ascending ? "↑" : "↓";
  const handlePageSizeChange = (e) => changePageSize(parseInt(e.target.value));
  const firstItem = pagination.totalCount === 0 ? 0 : (pagination.page - 1) * pagination.pageSize + 1;
  const lastItem = Math.min(pagination.page * pagination.pageSize, pagination.totalCount);
  const statusLabel = (value) => {
    const name = STATUS_NAMES[value] || String(value);
    return t(name, { defaultValue: name.replace(/([A-Z])/g, " $1").trim() });
  };

  if (loading && requests.length === 0) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} />;
  return (
    <div className="rm-container">
      <PageHeader icon={ClipboardList} kicker={t("student_services")} title={t("student_requests")} subtitle={t("manage_student_requests")} />
      <div className="rm-filters-bar">
        <div className="rm-search-box">
          <Search size={16} />
          <input type="text" placeholder={t("search_by_request_number_or_student")} value={searchInput} onChange={e => setSearchInput(e.target.value)} />
        </div>
        <div className="rm-status-filter">
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
            <option value="">{t("all_statuses")}</option>
            {FILTER_STATUSES.map(value => (
              <option key={value} value={value}>{statusLabel(value)}</option>
            ))}
          </select>
        </div>
      </div>
      {filteredRequests.length === 0 ? <EmptyState icon={ClipboardList} title={t("no_requests")} /> : (
        <>
          <div className="rm-table-container">
            <table className="rm-table">
              <thead>
                <tr>
                  <th className="sortable" onClick={() => handleSort("requestnumber")}>{t("request_number")} {getSortIcon("requestnumber")}</th>
                  <th className="sortable" onClick={() => handleSort("studentname")}>{t("student_name")} {getSortIcon("studentname")}</th>
                  <th>{t("service_name")}</th>
                  <th>{t("status")}</th>
                  <th>{t("date")}</th>
                  <th>{t("payment_status")}</th>
                  <th>{t("assigned_to")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredRequests.map(req => (
                  <tr key={req.id}>
                    <td className="rm-number">{req.requestNumber}</td>
                    <td>{getLocalized(req.studentName, i18n.language)}</td>
                    <td>{getLocalized(req.serviceName, i18n.language)}</td>
                    <td><StatusBadge status={req.status} /></td>
                    <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td>
                    <td><StatusBadge status={req.paymentStatus} type="payment" /></td>
                    <td>{req.assignedToStaffId ? t("assigned") : t("unassigned")}</td>
                    <td>
                      <button className="btn-primary" onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}>{t("review")}</button>
                      {!req.assignedToStaffId && (
                        <button className="btn-secondary" style={{ marginLeft: 6 }} onClick={() => claimMutation.mutate(req.id)} disabled={claimMutation.isPending}>
                          {claimMutation.isPending ? t("claiming") : t("claim")}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="rm-pagination-footer">
            <div className="rm-page-size-control">
              <label>{t("show")}</label>
              <select value={pagination.pageSize} onChange={handlePageSizeChange}><option value="10">10</option><option value="20">20</option><option value="50">50</option><option value="100">100</option></select>
              <span>{t("entries_per_page")}</span>
            </div>
            <div className="rm-pagination-buttons">
              <button className="btn-icon" onClick={() => changePage(1)} disabled={pagination.page === 1}><ChevronsLeft size={16} /></button>
              <button className="btn-icon" onClick={() => changePage(pagination.page - 1)} disabled={pagination.page === 1}><ChevronLeft size={16} /></button>
              <span className="rm-page-info">{t("page")} {pagination.page} / {pagination.totalPages}</span>
              <button className="btn-icon" onClick={() => changePage(pagination.page + 1)} disabled={pagination.page === pagination.totalPages}><ChevronRight size={16} /></button>
              <button className="btn-icon" onClick={() => changePage(pagination.totalPages)} disabled={pagination.page === pagination.totalPages}><ChevronsRight size={16} /></button>
            </div>
            <div className="rm-results-info">{t("showing_results", { first: firstItem, last: lastItem, total: pagination.totalCount })}</div>
          </div>
        </>
      )}
    </div>
  );
};

export default RequestsManagement;
