import { useState, useEffect, useCallback } from "react";
import { RefreshCw, Clock, CheckCircle, XCircle, AlertCircle, Activity, Settings, Trash2, RotateCcw } from "lucide-react";
import api from "../../../core/api/apiClient";
import PermissionGate from "../../../core/auth/PermissionGate";
import "../styles/sync.css";

const STATUS_ICONS = { succeeded: CheckCircle, failed: XCircle, running: Activity, pending: Clock };
const STATUS_COLORS = { succeeded: "#10b981", failed: "#ef4444", running: "#3b82f6", pending: "#f59e0b" };

function SyncPage() {
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
        setError("Sync history API not available yet");
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
        setError(err.message || "Failed to trigger sync");
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
      setError(err.message || "Failed to unlock sync entry");
    }
  };

  const handleDeleteHistory = async (id) => {
    try {
      await api.delete(`/sync/history/${id}`);
      await loadHistory();
    } catch (err) {
      setError(err.message || "Failed to delete sync entry");
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
            <h1>SIS Integration</h1>
            <p>Synchronize data between the portal and the Student Information System</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <PermissionGate resource="sync.sync" minLevel={2}>
            <button className="btn btn-primary" onClick={handleSync} disabled={syncing}>
              <RefreshCw size={16} className={syncing ? "spinning" : ""} />
              {syncing ? "Syncing..." : "Trigger Sync"}
            </button>
          </PermissionGate>
          <PermissionGate resource="sync.sync" minLevel={3}>
            <button className="btn btn-outline" onClick={() => {}} title="Configure sync settings">
              <Settings size={16} /> Edit Config
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
          <div className="sync-stat-info"><div className="sync-stat-value">{totalSyncs}</div><div className="sync-stat-label">Total Syncs</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#10b98118", color: "#10b981" }}><CheckCircle size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{succeeded}</div><div className="sync-stat-label">Succeeded</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#ef444418", color: "#ef4444" }}><XCircle size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{failed}</div><div className="sync-stat-label">Failed</div></div>
        </div>
        <div className="sync-stat-card">
          <div className="sync-stat-icon" style={{ background: "#8b5cf618", color: "#8b5cf6" }}><Activity size={22} /></div>
          <div className="sync-stat-info"><div className="sync-stat-value">{history[0] ? new Date(history[0].startedAt).toLocaleDateString() : "N/A"}</div><div className="sync-stat-label">Last Sync</div></div>
        </div>
      </div>

      <div className="sync-section-header">
        <Clock size={18} />
        <h2>Sync History</h2>
      </div>

      {loading ? (
        <div className="sync-loading">Loading sync history...</div>
      ) : history.length === 0 ? (
        <div className="sync-empty">
          <RefreshCw size={48} />
          <h3>No sync records yet</h3>
          <p>Sync history will appear here once you trigger your first synchronization</p>
        </div>
      ) : (
        <div className="sync-table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Status</th>
                <th>Started</th>
                <th>Completed</th>
                <th>Records Synced</th>
                <th>Errors</th>
                <th style={{ width: 100 }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {history.map((h, i) => {
                const StatusIcon = STATUS_ICONS[h.status] || Clock;
                const color = STATUS_COLORS[h.status] || "#6b7280";
                return (
                  <tr key={h.id || i}>
                    <td><span style={{ display: "inline-flex", alignItems: "center", gap: 6, color }}><StatusIcon size={14} /> {h.status}</span></td>
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
                              title="Unlock & retry"
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
                            title="Delete"
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
