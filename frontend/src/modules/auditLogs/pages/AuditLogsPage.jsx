import { useState, useEffect, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ShieldCheck, Search, Filter } from "lucide-react";
import api from "../../../core/api/apiClient";
import PageHeader from "../../../core/components/PageHeader";
import DataTable from "../../../core/components/DataTable";
import ErrorMessage from "../../../core/components/ErrorMessage";
import { useToast } from "../../../core/components/Toast";
import "../styles/auditLogs.css";

const PAGE_SIZE = 20;

const getActionLabel = (action, t) => {
  const map = {
    View: t("view_action"), Insert: t("create_action"), EditClose: t("edit_action"), Open: t("open_action"), Delete: t("delete_action"),
  };
  return map[action] || action;
};

function AuditLogsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [moduleFilter, setModuleFilter] = useState("");
  const [sortField, setSortField] = useState("createdAtUtc");
  const [sortAsc, setSortAsc] = useState(false);
  const [page, setPage] = useState(1);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.get("/audit-logs", { params: { pageSize: 50 } });
      setLogs(Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : []);
    } catch (err) {
      const message = err.message || t("failed_to_load_data");
      setError(message);
      addToast(message, "error");
    } finally {
      setLoading(false);
    }
  }, [addToast, t]);

  useEffect(() => { loadLogs(); }, [loadLogs]);

  const modules = [...new Set(logs.map((l) => l.source).filter(Boolean))];

  const filtered = useMemo(() => logs.filter((l) => {
    if (moduleFilter && l.source !== moduleFilter) return false;
    if (!search) return true;
    const q = search.toLowerCase();
    return (
      (l.source && l.source.toLowerCase().includes(q)) ||
      (l.entityName && l.entityName.toLowerCase().includes(q)) ||
      (l.userName && l.userName.toLowerCase().includes(q)) ||
      (l.message && l.message.toLowerCase().includes(q))
    );
  }), [logs, moduleFilter, search]);

  const sorted = useMemo(() => {
    const copy = [...filtered];
    copy.sort((a, b) => {
      const av = a[sortField] ?? "";
      const bv = b[sortField] ?? "";
      const cmp = String(av).localeCompare(String(bv));
      return sortAsc ? cmp : -cmp;
    });
    return copy;
  }, [filtered, sortField, sortAsc]);

  const totalPages = Math.max(1, Math.ceil(sorted.length / PAGE_SIZE));
  const pageData = sorted.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const toggleSort = (field) => {
    if (field === sortField) setSortAsc((a) => !a);
    else { setSortField(field); setSortAsc(true); }
    setPage(1);
  };

  const sortableLabel = (field, text) => (
    <span
      onClick={() => toggleSort(field)}
      style={{ cursor: "pointer", userSelect: "none" }}
    >
      {text}{sortField === field ? (sortAsc ? " ↑" : " ↓") : ""}
    </span>
  );

  const columns = [
    {
      key: "createdAtUtc", label: sortableLabel("createdAtUtc", t("timestamp")),
      render: (v) => <span className="audit-muted">{v ? new Date(v).toLocaleString() : "—"}</span>,
    },
    {
      key: "userName", label: sortableLabel("userName", t("user")),
      render: (v) => v || t("system"),
    },
    {
      key: "source", label: sortableLabel("source", t("module")),
      render: (v) => <span className="audit-badge audit-badge-outline">{v}</span>,
    },
    {
      key: "action", label: sortableLabel("action", t("action")),
      render: (v) => (
        <span className={`audit-badge ${v?.toLowerCase() === "delete" ? "audit-badge-danger" : "audit-badge-primary"}`}>
          {getActionLabel(v, t)}
        </span>
      ),
    },
    { key: "entityName", label: sortableLabel("entityName", t("resource")) },
    {
      key: "message", label: t("details"),
      cellStyle: { maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis" },
      render: (v, row) => <span className="audit-muted">{v || row.requestPath || "—"}</span>,
    },
  ];

  return (
    <div className="audit-page">
      <PageHeader icon={ShieldCheck} title={t("audit_logs")} subtitle={t("audit_logs_subtitle")} />

      <div className="audit-toolbar">
        <div className="audit-search-bar">
          <Search size={16} />
          <input
            type="text"
            placeholder={t("search_logs")}
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>
        <div className="audit-filter-group">
          <Filter size={16} />
          <select
            className="form-select"
            value={moduleFilter}
            onChange={(e) => { setModuleFilter(e.target.value); setPage(1); }}
          >
            <option value="">{t("all_modules")}</option>
            {modules.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
      </div>

      {error ? (
        <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 12 }}>
          <ErrorMessage message={error} />
          <button className="btn-outline" onClick={loadLogs}>{t("retry")}</button>
        </div>
      ) : (
        <DataTable
          columns={columns}
          data={pageData}
          loading={loading}
          emptyIcon={ShieldCheck}
          emptyTitle={search || moduleFilter ? t("no_logs_match_filters") : t("no_audit_logs_yet")}
          emptyMessage=""
          pagination={{ pageNumber: page, totalPages }}
          onPageChange={setPage}
          tableLabel={t("audit_logs")}
        />
      )}
    </div>
  );
}

export default AuditLogsPage;
