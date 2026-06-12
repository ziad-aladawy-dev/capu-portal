import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutGrid, Table as TableIcon, ClipboardList, AlertCircle } from "lucide-react";
import { useMyRequests } from "../hooks/useStudentRequests";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { RequestStatusBadge, PaymentStatusBadge } from "../components/RequestBadges";
import {
  REQUEST_STATUS_NAMES, REQUEST_STATUS_LABELS, KANBAN_COLUMNS,
} from "../constants/requestStatus";
import styles from "./MyRequests.module.css";

// Every status (1..10) is filterable so no request is ever invisible.
const FILTER_OPTIONS = Object.keys(REQUEST_STATUS_NAMES).map(Number);

function MyRequests() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const listQuery = useMyRequests();
  const [filterStatus, setFilterStatus] = useState(""); // "" = all, else int
  const [view, setView] = useState("table");

  const list = useMemo(() => listQuery.data || [], [listQuery.data]);
  const filtered = useMemo(
    () => (filterStatus === "" ? list : list.filter((r) => r.status === Number(filterStatus))),
    [list, filterStatus]
  );

  const byColumn = useMemo(() => {
    const map = {};
    for (const col of KANBAN_COLUMNS) {
      map[col.key] = list.filter((r) => col.statuses.includes(r.status));
    }
    return map;
  }, [list]);

  const open = (id) => navigate(`/student/requests/${id}`);

  const tableTitle = t("portal_requests.table", { defaultValue: "Table" });
  const boardTitle = t("portal_requests.board", { defaultValue: "Board" });

  return (
    <PortalPageShell
      title={t("portal_requests.title", { defaultValue: "My Requests" })}
      subtitle={t("portal_requests.subtitle", { defaultValue: "Track your service requests" })}
      actions={
        <div className={styles.viewToggle}>
          <button
            type="button"
            className={view === "table" ? styles.toggleActive : styles.toggleBtn}
            onClick={() => setView("table")}
            title={tableTitle}
            aria-label={tableTitle}
          >
            <TableIcon size={15} />
          </button>
          <button
            type="button"
            className={view === "kanban" ? styles.toggleActive : styles.toggleBtn}
            onClick={() => setView("kanban")}
            title={boardTitle}
            aria-label={boardTitle}
          >
            <LayoutGrid size={15} />
          </button>
        </div>
      }
    >
      {listQuery.isLoading ? (
        <div className={styles.loading}>
          {Array.from({ length: 4 }).map((_, i) => <PortalSkeleton key={i} variant="block" height={56} />)}
        </div>
      ) : listQuery.isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_requests.load_failed", { defaultValue: "Couldn't load your requests." })}
          onAction={() => listQuery.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : !list.length ? (
        <PortalEmptyState
          icon={ClipboardList}
          title={t("portal_requests.empty", { defaultValue: "You have no requests yet." })}
          text={t("portal_requests.empty_hint", { defaultValue: "Browse the services catalog to submit your first request." })}
          action={{
            to: "/student/services",
            label: t("portal_requests.browse_services", { defaultValue: "Browse services" }),
          }}
        />
      ) : view === "table" ? (
        <>
          <div className={styles.filterBar}>
            <select
              className={styles.filterSelect}
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
            >
              <option value="">{t("portal_requests.all_statuses", { defaultValue: "All Statuses" })}</option>
              {FILTER_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {t(`portal_requests.status_${REQUEST_STATUS_NAMES[s]}`, {
                    defaultValue: REQUEST_STATUS_LABELS[s],
                  })}
                </option>
              ))}
            </select>
          </div>
          <PortalCard padding="none" className={styles.tableCard}>
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>{t("portal_requests.request", { defaultValue: "Request" })}</th>
                    <th>{t("portal_requests.service", { defaultValue: "Service" })}</th>
                    <th>{t("portal_requests.submitted", { defaultValue: "Submitted" })}</th>
                    <th>{t("portal_requests.status", { defaultValue: "Status" })}</th>
                    <th>{t("portal_requests.payment", { defaultValue: "Payment" })}</th>
                    <th>{t("portal_requests.actions", { defaultValue: "Actions" })}</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((req) => (
                    <tr key={req.id}>
                      <td className={styles.reqNum}>#{req.requestNumber || String(req.id).slice(0, 8)}</td>
                      <td>{req.serviceName}</td>
                      <td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "—"}</td>
                      <td><RequestStatusBadge status={req.status} /></td>
                      <td><PaymentStatusBadge status={req.paymentStatus} /></td>
                      <td>
                        <button type="button" className={styles.linkBtn} onClick={() => open(req.id)}>
                          {t("portal_requests.view", { defaultValue: "View" })}
                        </button>
                      </td>
                    </tr>
                  ))}
                  {!filtered.length && (
                    <tr>
                      <td colSpan={6} className={styles.tableEmpty}>
                        {t("portal_requests.none_for_filter", { defaultValue: "No requests with this status." })}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </PortalCard>
        </>
      ) : (
        <div className={styles.kanban}>
          {KANBAN_COLUMNS.map((col) => (
            <div key={col.key} className={styles.kanbanCol}>
              <div className={styles.kanbanHead}>
                {t(`portal_requests.${col.labelKey}`, { defaultValue: col.label })}
                <span className={styles.kanbanCount}>{byColumn[col.key].length}</span>
              </div>
              <div className={styles.kanbanCards}>
                {byColumn[col.key].map((req) => (
                  <button type="button" key={req.id} className={styles.kanbanCard} onClick={() => open(req.id)}>
                    <div className={styles.kanbanService}>{req.serviceName}</div>
                    <div className={styles.kanbanMeta}>
                      <span>#{req.requestNumber || String(req.id).slice(0, 8)}</span>
                      {col.statuses.length > 1 && <RequestStatusBadge status={req.status} />}
                      <PaymentStatusBadge status={req.paymentStatus} />
                    </div>
                  </button>
                ))}
                {byColumn[col.key].length === 0 && <div className={styles.kanbanEmpty}>—</div>}
              </div>
            </div>
          ))}
        </div>
      )}
    </PortalPageShell>
  );
}

export default MyRequests;
