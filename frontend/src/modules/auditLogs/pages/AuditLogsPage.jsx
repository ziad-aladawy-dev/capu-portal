import { useState, useEffect, useCallback } from "react";
import { ShieldCheck, Search, Filter, AlertCircle } from "lucide-react";
import api from "../../../core/api/apiClient";
import "../styles/auditLogs.css";

const ACTION_LABELS = {
  View: "View", Insert: "Create", EditClose: "Edit", Open: "Open", Delete: "Delete",
};

function AuditLogsPage() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [moduleFilter, setModuleFilter] = useState("");

  const loadLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = { pageSize: 50 };
      if (moduleFilter) params.module = moduleFilter;
      const { data } = await api.get("/audit-logs", { params });
      setLogs(Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load audit logs");
    } finally {
      setLoading(false);
    }
  }, [moduleFilter]);

  useEffect(() => { loadLogs(); }, [loadLogs]);

  const modules = [...new Set(logs.map((l) => l.module).filter(Boolean))];

  const filtered = logs.filter((l) => {
    if (!search) return true;
    const q = search.toLowerCase();
    return (
      (l.module && l.module.toLowerCase().includes(q)) ||
      (l.resource && l.resource.toLowerCase().includes(q)) ||
      (l.performedBy && l.performedBy.toLowerCase().includes(q))
    );
  });

  return (
    <div className="audit-page">
      <div className="audit-header">
        <div className="audit-header-left">
          <ShieldCheck size={24} />
          <div>
            <h1>Audit Logs</h1>
            <p>Track all administrative actions across the system</p>
          </div>
        </div>
      </div>

      {error && (
        <div className="audit-alert audit-alert-error">
          <AlertCircle size={16} /> {error}
        </div>
      )}

      <div className="audit-toolbar">
        <div className="audit-search-bar">
          <Search size={16} />
          <input type="text" placeholder="Search logs..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div className="audit-filter-group">
          <Filter size={16} />
          <select className="form-select" value={moduleFilter} onChange={(e) => setModuleFilter(e.target.value)}>
            <option value="">All Modules</option>
            {modules.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="audit-loading">Loading audit logs...</div>
      ) : filtered.length === 0 ? (
        <div className="audit-empty">
          <ShieldCheck size={48} />
          <h3>{search || moduleFilter ? "No logs match your filters" : "No audit logs yet"}</h3>
        </div>
      ) : (
        <div className="audit-table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Timestamp</th>
                <th>User</th>
                <th>Module</th>
                <th>Action</th>
                <th>Resource</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((l, i) => (
                <tr key={l.id || i}>
                  <td className="audit-muted">{l.timestamp ? new Date(l.timestamp).toLocaleString() : "—"}</td>
                  <td>{l.performedBy || "System"}</td>
                  <td><span className="audit-badge audit-badge-outline">{l.module}</span></td>
                  <td><span className={`audit-badge ${l.action?.toLowerCase() === "delete" ? "audit-badge-danger" : "audit-badge-primary"}`}>{ACTION_LABELS[l.action] || l.action}</span></td>
                  <td>{l.resource}</td>
                  <td className="audit-muted" style={{ maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {l.details || l.description || "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default AuditLogsPage;
