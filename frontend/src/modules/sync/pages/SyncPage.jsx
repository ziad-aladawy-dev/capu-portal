import { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { RefreshCw, Clock, CheckCircle, XCircle, AlertCircle, Activity, Settings, Trash2, RotateCcw } from "lucide-react";
import api from "../../../core/api/apiClient";
import PermissionGate from "../../../core/auth/PermissionGate";
import "../styles/sync.css";

const STATUS_ICONS = { succeeded: CheckCircle, failed: XCircle, running: Activity, pending: Clock };
const STATUS_COLORS = { succeeded: "#10b981", failed: "#ef4444", running: "#3b82f6", pending: "#f59e0b" };

function SyncPage() {
  const { t } = useTranslation();
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState(null);

  const loadHistory = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get("/sync/history", { params: { pageSize: 20 } });
      setHistory(Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : []);
    } catch (err) {
      if (err.response?.status !== 404) {
        setError(t("failed_to_load_data"));
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadHistory(); }, [loadHistory]);

  const handleSync = async () => {
    setSyncing(true);
    setError(null);
    try {
      await api.post("/sync/trigger");
      await loadHistory();
    } catch (err) {
      if (err.response?.status !== 404) {
        setError(err.message || t("failed_to_load_data"));
      }
    } finally {
      setSyncing(false);
    }
  };

  const handleUnlock = async (id) => {
    try {
      await api.post(`/sync/history/${id}/unlock`);
      await loadHistory();
    } catch (err) {
      setError(err.message || t("failed_to_load_data"));
    }
  };

  const handleDeleteHistory = async (id) => {
    try {
      await api.delete(`/sync/history/${id}`);
      await loadHistory();
    } catch (err) {
      setError(err.message || t("failed_to_load_data"));
    }
  };

  const totalSyncs = history.length;
  const succeeded = history.filter((h) => h.status === "succeeded").length;
  const failed = history.filter((h) => h.status === "failed").length;

  return (
    <div className="sync-page">
      <div className="sync-header">
        <div className="sync-header-left">
          <RefreshCw size={24} />
          <div>
            <h1>{t("sis_integration")}</h1>
            <p>{t("sis_integration_subtitle")}</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <PermissionGate resource="sync.sync" minLevel={2}>
            <button className="btn btn-primary" onClick={handleSync} disabled={syncing}>
              <RefreshCw size={16} className={syncing ? "spinning" : ""} />
              {syncing ? t("syncing") : t("trigger_sync")}
            </button>
          </PermissionGate>
          <PermissionGate resource="sync.sync" minLevel={3}>
            <button className="btn btn-outline" onClick={() => {}} title={t("edit_config")}>
              <Settings size={16} /> {t("edit_config")}
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="sync-alert sync-alert-warning">
          <AlertCircle size={16} /> {error}
        </div>
      )}

      <div className="sync-stats-grid">
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#3b82f618", color: "#3b82f6" }}><RefreshCw size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{totalSyncs}</div><div className="sync-stat-label">{t("total_syncs")}</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#10b98118", color: "#10b981" }}><CheckCircle size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{succeeded}</div><div className="sync-stat-label">{t("succeeded")}</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#ef444418", color: "#ef4444" }}><XCircle size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{failed}</div><div className="sync-stat-label">{t("failed")}</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#8b5cf618", color: "#8b5cf6" }}><Activity size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{history[0] ? new Date(history[0].startedAt).toLocaleDateString() : t("n_a")}</div><div className="sync-stat-label">{t("last_sync")}</div></div>
        </div>
      </div>

      <div className="sync-section-header">
        <Clock size={18} />
        <h2>{t("sync_history")}</h2>
      </div>

      {loading ? (
        <div className="sync-loading">{t("loading_sync_history")}</div>
      ) : history.length === 0 ? (
        <div className="sync-empty">
          <RefreshCw size={48} />
          <h3>{t("no_sync_records")}</h3>
          <p>{t("no_sync_records_message")}</p>
        </div>
      ) : (
        <div className="sync-table-wrapper">
          <table>
            <thead>
              <tr>
                <th>{t("status")}</th>
                <th>{t("started")}</th>
                <th>{t("completed")}</th>
                <th>{t("records_synced")}</th>
                <th>{t("errors")}</th>
                <th style={{ width: 100 }}>{t("actions")}</th>
              </tr>
            </thead>
            <tbody>
              {history.map((h, i) => {
                const StatusIcon = STATUS_ICONS[h.status] || Clock;
                const color = STATUS_COLORS[h.status] || "#6b7280";
                return (
                  <tr key={h.id || i}>
                    <td><span style={{ display: "inline-flex", alignItems: "center", gap: 6, color }}><StatusIcon size={14} /> {t(h.status)}</span></td>
                    <td>{h.startedAt ? new Date(h.startedAt).toLocaleString() : "—"}</td>
                    <td>{h.completedAt ? new Date(h.completedAt).toLocaleString() : "—"}</td>
                    <td>{h.recordsSynced ?? "—"}</td>
                    <td>{h.errorCount ?? h.errors?.length ?? "—"}</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <PermissionGate resource="sync.sync" minLevel={4}>
                          {h.status === "failed" && (
                            <button
                              className="sync-action-btn"
                              onClick={() => handleUnlock(h.id)}
                              title={t("unlock_retry")}
                              style={{ color: "#f59e0b" }}
                            >
                              <RotateCcw size={13} />
                            </button>
                          )}
                        </PermissionGate>
                        <PermissionGate resource="sync.sync" minLevel={5}>
                          <button
                            className="sync-action-btn"
                            onClick={() => handleDeleteHistory(h.id)}
                            title={t("delete")}
                            style={{ color: "#ef4444" }}
                          >
                            <Trash2 size={13} />
                          </button>
                        </PermissionGate>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default SyncPage;
