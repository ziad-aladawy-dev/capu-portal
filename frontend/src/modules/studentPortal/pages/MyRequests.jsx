import { useEffect, useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutGrid, Table as TableIcon } from "lucide-react";
import { useStudentRequests } from "../hooks/useStudentRequests";
import LoadingSpinner from "../../studentServices/components/LoadingSpinner";
import EmptyState from "../../studentServices/components/EmptyState";
import StatusBadge from "../components/StatusBadge";
import {
  REQUEST_STATUS, REQUEST_STATUS_LABELS, KANBAN_COLUMNS,
} from "../constants/requestStatus";
import "../styles/MyRequests.css";

const FILTER_OPTIONS = [
  REQUEST_STATUS.Draft, REQUEST_STATUS.Pending, REQUEST_STATUS.UnderReview,
  REQUEST_STATUS.MoreInfoRequired, REQUEST_STATUS.Approved, REQUEST_STATUS.Rejected,
  REQUEST_STATUS.Completed,
];

const MyRequests = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { requests, loading, loadRequests } = useStudentRequests();
  const [filterStatus, setFilterStatus] = useState(""); // "" = all, else int
  const [view, setView] = useState("table");

  useEffect(() => { loadRequests(); }, [loadRequests]);

  const list = useMemo(() => requests || [], [requests]);
  const filtered = useMemo(
    () => (filterStatus === "" ? list : list.filter((r) => r.status === Number(filterStatus))),
    [list, filterStatus]
  );

  const byColumn = useMemo(() => {
    const map = {};
    for (const col of KANBAN_COLUMNS) map[col.key] = [];
    for (const r of list) if (map[r.status]) map[r.status].push(r);
    return map;
  }, [list]);

  const open = (id) => navigate(`/student/requests/${id}`);

  if (loading && !list.length) return <LoadingSpinner />;

  return (
    <div className="mr-container">
      <div className="mr-head">
        <h1>{t("my_requests", { defaultValue: "My Requests" })}</h1>
        <div className="mr-view-toggle">
          <button className={view === "table" ? "active" : ""} onClick={() => setView("table")} title="Table">
            <TableIcon size={16} />
          </button>
          <button className={view === "kanban" ? "active" : ""} onClick={() => setView("kanban")} title="Board">
            <LayoutGrid size={16} />
          </button>
        </div>
      </div>

      {view === "table" && (
        <div className="mr-filter">
          <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
            <option value="">{t("all_statuses", { defaultValue: "All Statuses" })}</option>
            {FILTER_OPTIONS.map((s) => (
              <option key={s} value={s}>{REQUEST_STATUS_LABELS[s]}</option>
            ))}
          </select>
        </div>
      )}

      {!list.length ? (
        <EmptyState message={t("no_requests", { defaultValue: "You have no requests yet." })} />
      ) : view === "table" ? (
        <div className="mr-table-wrapper">
          <table className="mr-table">
            <thead>
              <tr>
                <th>{t("request_id", { defaultValue: "Request" })}</th>
                <th>{t("service", { defaultValue: "Service" })}</th>
                <th>{t("submitted_date", { defaultValue: "Submitted" })}</th>
                <th>{t("status", { defaultValue: "Status" })}</th>
                <th>{t("payment_status", { defaultValue: "Payment" })}</th>
                <th>{t("actions", { defaultValue: "Actions" })}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((req) => (
                <tr key={req.id}>
                  <td className="mr-id">{String(req.id).slice(0, 8)}</td>
                  <td>{req.serviceName}</td>
                  <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td>
                  <td><StatusBadge status={req.status} type="request" /></td>
                  <td><StatusBadge status={req.paymentStatus} type="payment" /></td>
                  <td>
                    <button className="mr-link" onClick={() => open(req.id)}>
                      {t("view_details", { defaultValue: "View" })}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="mr-kanban">
          {KANBAN_COLUMNS.map((col) => (
            <div key={col.key} className="mr-kanban-col">
              <div className="mr-kanban-head">
                {t(`status_${col.key}`, { defaultValue: col.label })}
                <span className="mr-kanban-count">{byColumn[col.key].length}</span>
              </div>
              <div className="mr-kanban-cards">
                {byColumn[col.key].map((req) => (
                  <button key={req.id} className="mr-kanban-card" onClick={() => open(req.id)}>
                    <div className="mr-kanban-service">{req.serviceName}</div>
                    <div className="mr-kanban-meta">
                      <span>#{String(req.id).slice(0, 8)}</span>
                      <StatusBadge status={req.paymentStatus} type="payment" />
                    </div>
                  </button>
                ))}
                {byColumn[col.key].length === 0 && <div className="mr-kanban-empty">—</div>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default MyRequests;
