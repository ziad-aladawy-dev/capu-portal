import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  FolderTree, Shield, Check, AlertTriangle, X, Search, RefreshCw,
} from "lucide-react";
import * as authorizationService from "../../../core/services/authorizationService";
import * as permissionService from "../../../core/services/permissionService";
import "../styles/authorization.css";

function PermissionTreePage() {
  const { t } = useTranslation();
  const [roles, setRoles] = useState([]);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [tree, setTree] = useState([]);
  const [search, setSearch] = useState("");

  const [loading, setLoading] = useState(true);
  const [rolesLoading, setRolesLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setRolesLoading(true);
    permissionService
      .fetchAllRoles({ page: 1, pageSize: 100 })
      .then((r) => {
        if (cancelled) return;
        setRoles(r?.items || []);
      })
      .catch(() => {
        if (!cancelled) setRoles([]);
      })
      .finally(() => {
        if (!cancelled) setRolesLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = selectedRoleId
        ? await authorizationService.fetchRolePermissions(selectedRoleId)
        : await authorizationService.fetchPermissionTree();
      setTree(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load permission tree");
      setTree([]);
    } finally {
      setLoading(false);
    }
  }, [selectedRoleId]);

  useEffect(() => {
    load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return tree;
    return tree
      .map((m) => ({
        ...m,
        resources: (m.resources || [])
          .map((r) => ({
            ...r,
            permissions: (r.permissions || []).filter(
              (p) =>
                p.permissionName.toLowerCase().includes(q) ||
                p.action.toLowerCase().includes(q) ||
                r.resourceName.toLowerCase().includes(q) ||
                m.moduleName.toLowerCase().includes(q)
            ),
          }))
          .filter(
            (r) =>
              r.permissions.length > 0 ||
              r.resourceName.toLowerCase().includes(q) ||
              m.moduleName.toLowerCase().includes(q)
          ),
      }))
      .filter(
        (m) =>
          m.resources.length > 0 || m.moduleName.toLowerCase().includes(q)
      );
  }, [tree, search]);

  return (
    <div className="auth-tree-page">
      <div className="auth-tree-header">
        <div className="auth-tree-header-left">
          <FolderTree size={22} />
          <div>
            <h1>{t("permission_inspector")}</h1>
            <p>{t("permission_inspector_desc")}</p>
          </div>
        </div>
        <button className="spr-btn spr-btn-outline" onClick={load} style={{
          border: "1.5px solid #d1d5db",
          background: "transparent",
          color: "#6b7280",
          fontFamily: "Outfit",
          fontSize: 13,
          fontWeight: 600,
          padding: "8px 16px",
          borderRadius: 8,
          cursor: "pointer",
          display: "inline-flex",
          alignItems: "center",
          gap: 6,
        }}>
          <RefreshCw size={13} />
          {t("refresh")}
        </button>
      </div>

      {error && (
        <div className="auth-tree-error-banner">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button
            onClick={() => setError(null)}
            style={{
              marginLeft: "auto",
              background: "transparent",
              border: "none",
              cursor: "pointer",
              color: "#b91c1c",
            }}
          >
            <X size={14} />
          </button>
        </div>
      )}

      <div className="auth-tree-toolbar">
        <select
          value={selectedRoleId}
          onChange={(e) => setSelectedRoleId(e.target.value)}
          disabled={rolesLoading}
        >
          <option value="">{t("full_permission_tree")}</option>
          {roles.map((r) => (
            <option key={r.id} value={r.id}>
              Role: {r.name}
            </option>
          ))}
        </select>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 6,
            background: "white",
            border: "1px solid #e5e7eb",
            padding: "8px 12px",
            borderRadius: 8,
            flex: 1,
            minWidth: 200,
          }}
        >
          <Search size={14} />
          <input
            type="text"
            placeholder={t("filter_permissions")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ border: "none", outline: "none", flex: 1, background: "transparent", fontSize: 13 }}
          />
        </div>
      </div>

      {loading ? (
        <div className="auth-tree-loading">
          <div className="auth-tree-spinner" />
          <p>{t("loading_permission_tree")}</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="auth-tree-empty">
          <Shield size={40} />
          <h3>{t("no_permissions_to_show")}</h3>
          <p>
            {tree.length === 0
              ? selectedRoleId
                ? t("no_permission_rows")
                : t("no_permissions_defined")
              : t("no_permissions_filter")}
          </p>
        </div>
      ) : (
        <div className="auth-tree-modules">
          {filtered.map((m) => (
            <div key={m.moduleId} className="auth-tree-module">
              <div className="auth-tree-module-head">
                <span>{m.moduleName}</span>
                <span className="module-meta">
                  {(m.resources || []).reduce(
                    (sum, r) => sum + (r.permissions?.length || 0),
                    0
                  )}{" "}
                  {t("permissions")}
                </span>
              </div>
              {(m.resources || []).map((r) => (
                <div key={r.resourceId} className="auth-tree-resource">
                  <strong>{r.resourceName}</strong>
                  <div className="auth-tree-actions">
                    {(r.permissions || []).map((p) => {
                      const isAssigned = p.isAssigned === true;
                      const isUnassigned = p.isAssigned === false;
                      return (
                        <span
                          key={p.permissionId}
                          className={`auth-tree-action-chip ${
                            isAssigned ? "is-assigned" : isUnassigned ? "is-unassigned" : ""
                          }`}
                          title={p.description || p.permissionName}
                        >
                          {isAssigned && <Check size={11} />}
                          {p.action}
                        </span>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default PermissionTreePage;
