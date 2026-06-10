import { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { ShieldCheck, Search, Filter, AlertCircle } from "lucide-react";
import api from "../../../core/api/apiClient";
import "../styles/auditLogs.css";

const getActionLabel = (action, t) => {
  const map = {
    View: t("view_action"), Insert: t("create_action"), EditClose: t("edit_action"), Open: t("open_action"), Delete: t("delete_action"),
  };
  return map[action] || action;
};

function AuditLogsPage() {
  const { t } = useTranslation();
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
      setError(err.message || t("failed_to_load_data"));
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
            <h1>{t("audit_logs")}</h1>
            <p>{t("audit_logs_subtitle")}</p>
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
          <input type="text" placeholder={t("search_logs")} value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div className="audit-filter-group">
          <Filter size={16} />
          <select className="form-select" value={moduleFilter} onChange={(e) => setModuleFilter(e.target.value)}>
            <option value="">{t("all_modules")}</option>
            {modules.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="audit-loading">{t("loading_audit_logs")}</div>
      ) : filtered.length === 0 ? (
        <div className="audit-empty">
          <ShieldCheck size={48} />
          <h3>{search || moduleFilter ? t("no_logs_match_filters") : t("no_audit_logs_yet")}</h3>
        </div>
      ) : (
        <div className="audit-table-wrapper">
          <table>
            <thead>
              <tr>
                <th>{t("timestamp")}</th>
                <th>{t("user")}</th>
                <th>{t("module")}</th>
                <th>{t("action")}</th>
                <th>{t("resource")}</th>
                <th>{t("details")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((l, i) => (
                <tr key={l.id || i}>
                  <td className="audit-muted">{l.timestamp ? new Date(l.timestamp).toLocaleString() : "—"}</td>
                  <td>{l.performedBy || t("system")}</td>
                  <td><span className="audit-badge audit-badge-outline">{l.module}</span></td>
                  <td><span className={`audit-badge ${l.action?.toLowerCase() === "delete" ? "audit-badge-danger" : "audit-badge-primary"}`}>{getActionLabel(l.action, t)}</span></td>
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
