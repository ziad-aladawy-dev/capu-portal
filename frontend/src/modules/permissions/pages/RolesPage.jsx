import { useState, useEffect, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { UserCog, Plus, Shield, ShieldCheck, Trash2, AlertTriangle, RefreshCw, Save, Info, Users, Settings, X, Search, RotateCcw, UserPlus, UserMinus, Loader2 } from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import * as permissionService from "../../../core/services/permissionService";
import * as authorizationService from "../../../core/services/authorizationService";
import * as staffService from "../../../core/services/staffService";
import "../styles/roles.css";

const ACTION_LEVELS = [
  { value: 0, label: "No Permission" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];

const ACTION_NAME_TO_LEVEL = {
  View: 1, Insert: 2, EditClose: 3, Open: 4, Delete: 5,
};
const LEVEL_TO_ACTION = { 1: "View", 2: "Insert", 3: "EditClose", 4: "Open", 5: "Delete" };
const LABEL_TO_ACTION = {
  View: "View",
  Insert: "Insert",
  Edit: "EditClose",
  Open: "Open",
  Delete: "Delete",
};

function computeResourceLevel(permissions) {
  if (!permissions || permissions.length === 0) return 0;
  let maxLevel = 0;
  for (const p of permissions) {
    if (p.isAssigned) {
      const level = ACTION_NAME_TO_LEVEL[p.action] || 0;
      if (level > maxLevel) maxLevel = level;
    }
  }
  return maxLevel;
}

function RolesPage() {
  const { t } = useTranslation();
  const actionLevelLabels = {
    0: t("no_permission"),
    1: t("view"),
    2: t("insert"),
    3: t("edit"),
    4: t("open_level"),
    5: t("delete"),
  };
  const [roles, setRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedRoleId, setSelectedRoleId] = useState(null);

  const [activeTab, setActiveTab] = useState("general");

  const [formName, setFormName] = useState("");
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const [permTree, setPermTree] = useState([]);
  const [rolePerms, setRolePerms] = useState(null);
  const [permLoading, setPermLoading] = useState(false);
  const [permActiveModuleId, setPermActiveModuleId] = useState(null);
  const [originalLevels, setOriginalLevels] = useState({});
  const [pendingLevels, setPendingLevels] = useState({});
  const [permSaving, setPermSaving] = useState(false);

  const [members, setMembers] = useState([]);
  const [membersLoading, setMembersLoading] = useState(false);

  const [addMemberModalOpen, setAddMemberModalOpen] = useState(false);
  const [memberSearch, setMemberSearch] = useState("");
  const [memberSearchResults, setMemberSearchResults] = useState([]);
  const [memberSearching, setMemberSearching] = useState(false);
  const [addingMember, setAddingMember] = useState(false);

  const [removeMemberTarget, setRemoveMemberTarget] = useState(null);
  const [removingMember, setRemovingMember] = useState(false);

  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createName, setCreateName] = useState("");
  const [createError, setCreateError] = useState("");
  const [creating, setCreating] = useState(false);

  const [roleSearch, setRoleSearch] = useState("");

  const fetchRoles = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await permissionService.fetchAllRoles({ pageSize: 200 });
      setRoles(data.items || []);
    } catch (err) {
      setError(err.message || "Failed to load roles");
      setRoles([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchRoles(); }, [fetchRoles]);

  const selectedRole = useMemo(
    () => roles.find((r) => r.id === selectedRoleId) || null,
    [roles, selectedRoleId]
  );

  const filteredRoles = useMemo(() => {
    if (!roleSearch.trim()) return roles;
    const q = roleSearch.toLowerCase();
    return roles.filter((r) => r.name.toLowerCase().includes(q));
  }, [roles, roleSearch]);

  const loadPermissionTree = useCallback(async () => {
    try {
      const tree = await authorizationService.fetchPermissionTree();
      const modules = Array.isArray(tree) ? tree : [];
      setPermTree(modules);
      if (modules.length > 0) {
        setPermActiveModuleId((prev) => prev || modules[0].moduleId);
      }
    } catch {
      setPermTree([]);
    }
  }, []);

  const loadRolePermissions = useCallback(async (roleId) => {
    setPermLoading(true);
    try {
      const data = await permissionService.fetchRolePermissions(roleId);
      const tree = Array.isArray(data) ? data : [];
      setRolePerms(tree);

      const levels = {};
      for (const mod of tree) {
        for (const res of (mod.resources || [])) {
          const key = `${mod.moduleId}::${res.resourceId}`;
          levels[key] = computeResourceLevel(res.permissions || []);
        }
      }
      setOriginalLevels(levels);
      setPendingLevels({});

      if (tree.length > 0) {
        setPermActiveModuleId((prev) => prev || tree[0].moduleId);
      }
      if (permTree.length === 0) {
        setPermTree(tree);
      }
    } catch (err) {
      console.error("Failed to load role permissions:", err);
    } finally {
      setPermLoading(false);
    }
  }, [permTree.length]);

  useEffect(() => {
    loadPermissionTree();
  }, [loadPermissionTree]);

  const loadRoleMembers = useCallback(async (roleId) => {
    setMembersLoading(true);
    try {
      const data = await permissionService.fetchRoleMembers(roleId);
      setMembers(Array.isArray(data) ? data : []);
    } catch {
      setMembers([]);
    } finally {
      setMembersLoading(false);
    }
  }, []);

  const openAddMember = () => {
    setAddMemberModalOpen(true);
    setMemberSearch("");
    setMemberSearchResults([]);
  };

  const closeAddMember = () => {
    setAddMemberModalOpen(false);
    setMemberSearch("");
    setMemberSearchResults([]);
  };

  const handleMemberSearch = useCallback(async (query) => {
    setMemberSearch(query);
    if (!query.trim()) {
      setMemberSearchResults([]);
      return;
    }
    setMemberSearching(true);
    try {
      const data = await staffService.searchStaff({ search: query, page: 1, pageSize: 10 });
      const items = data.items || data.data || data || [];
      const results = (Array.isArray(items) ? items : [])
        .filter((s) => !members.some((m) => m.staffId === s.id))
        .map((s) => ({
          id: s.id,
          name: s.name,
          email: s.email,
          employeeCode: s.employeeCode,
          jobTitle: s.jobTitle,
        }));
      setMemberSearchResults(results);
    } catch {
      setMemberSearchResults([]);
    } finally {
      setMemberSearching(false);
    }
  }, [members]);

  const handleAddMember = async (staffId) => {
    if (!selectedRole || addingMember) return;
    setAddingMember(true);
    try {
      await permissionService.addRoleMember(selectedRole.id, staffId);
      await loadRoleMembers(selectedRole.id);
      setMemberSearch("");
      setMemberSearchResults([]);
    } catch (err) {
      setError(err.message || "Failed to add member");
    } finally {
      setAddingMember(false);
    }
  };

  const openRemoveMember = (member) => {
    setRemoveMemberTarget(member);
  };

  const closeRemoveMember = () => {
    setRemoveMemberTarget(null);
    setRemovingMember(false);
  };

  const handleRemoveMember = async () => {
    if (!selectedRole || !removeMemberTarget || removingMember) return;
    setRemovingMember(true);
    try {
      await permissionService.removeRoleMember(selectedRole.id, removeMemberTarget.staffId);
      await loadRoleMembers(selectedRole.id);
      closeRemoveMember();
    } catch (err) {
      setError(err.message || "Failed to remove member");
      closeRemoveMember();
    }
  };

  useEffect(() => {
    if (selectedRoleId && activeTab === "permissions") {
      loadRolePermissions(selectedRoleId);
    }
  }, [selectedRoleId, activeTab, loadRolePermissions]);

  useEffect(() => {
    setMembers([]);
    if (selectedRoleId && activeTab === "members") {
      loadRoleMembers(selectedRoleId);
    }
  }, [selectedRoleId, activeTab, loadRoleMembers]);

  const activeModule = useMemo(() => {
    return (rolePerms || permTree).find((m) => m.moduleId === permActiveModuleId);
  }, [rolePerms, permTree, permActiveModuleId]);

  const handleLevelChange = (compositeKey, level) => {
    setPendingLevels((prev) => {
      const original = originalLevels[compositeKey] || 0;
      const current = prev[compositeKey] !== undefined ? prev[compositeKey] : original;
      const newLevel = current === level ? 0 : level;
      if (newLevel === original) {
        const next = { ...prev };
        delete next[compositeKey];
        return next;
      }
      return { ...prev, [compositeKey]: newLevel };
    });
  };

  const displayLevels = useMemo(() => {
    const merged = { ...originalLevels };
    for (const [key, lvl] of Object.entries(pendingLevels)) {
      merged[key] = lvl;
    }
    return merged;
  }, [originalLevels, pendingLevels]);

  const permDirty = Object.keys(pendingLevels).length > 0;

  const resourceActions = useMemo(() => {
    const map = {};
    const source = rolePerms || permTree;
    for (const mod of source) {
      for (const res of (mod.resources || [])) {
        const key = `${mod.moduleId}::${res.resourceId}`;
        const actions = new Set();
        for (const p of (res.permissions || [])) {
          actions.add(p.action);
        }
        map[key] = actions;
      }
    }
    return map;
  }, [rolePerms, permTree]);

  const handleResetPermissions = () => {
    if (selectedRole) loadRolePermissions(selectedRole.id);
  };

  const handleSavePermissions = async () => {
    if (!selectedRole) return;
    setPermSaving(true);
    try {
      const resources = Object.entries(displayLevels)
        .filter(([, level]) => level > 0)
        .map(([compositeKey, level]) => {
          const resourceId = compositeKey.split("::")[1];
          return { resourceId, actions: [LEVEL_TO_ACTION[level]].filter(Boolean) };
        });
      await permissionService.updateRolePermissions(selectedRole.id, { resources });
      await loadRolePermissions(selectedRole.id);
    } catch (err) {
      setError(err.message || "Failed to save permissions");
    } finally {
      setPermSaving(false);
    }
  };

  const openCreate = () => {
    setCreateModalOpen(true);
    setCreateName("");
    setCreateError("");
  };

  const closeCreate = () => {
    setCreateModalOpen(false);
    setCreateName("");
    setCreateError("");
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    const trimmed = createName.trim();
    if (!trimmed) { setCreateError(t("role_name_required")); return; }
    if (trimmed.length < 2) { setCreateError(t("role_name_min_length")); return; }
    if (trimmed.length > 100) { setCreateError(t("role_name_max_length")); return; }
    setCreating(true);
    try {
      await permissionService.createRole({ name: trimmed });
      closeCreate();
      await fetchRoles();
    } catch (err) {
      if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
        setCreateError(t("role_name_exists"));
      } else {
        setCreateError(err.message || "Failed to create role");
      }
    } finally {
      setCreating(false);
    }
  };

  const handleSelectRole = (roleId) => {
    setSelectedRoleId(roleId);
    setActiveTab("general");
    setFormName(roles.find((r) => r.id === roleId)?.name || "");
    setFormError("");
  };

  const handleUpdateName = async () => {
    if (!selectedRole) return;
    const trimmed = formName.trim();
    if (!trimmed) { setFormError(t("role_name_required")); return; }
    if (trimmed.length < 2) { setFormError(t("role_name_min_length")); return; }
    if (trimmed.length > 100) { setFormError(t("role_name_max_length")); return; }
    setSaving(true);
    try {
      await permissionService.updateRole(selectedRole.id, { name: trimmed });
      setFormError("");
      await fetchRoles();
    } catch (err) {
      if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
        setFormError(t("role_name_exists"));
      } else {
        setFormError(err.message || "Failed to update role");
      }
    } finally {
      setSaving(false);
    }
  };

  const openDelete = () => setDeleteTarget(selectedRole);
  const closeDelete = () => { setDeleteTarget(null); setDeleting(false); };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await permissionService.deleteRole(deleteTarget.id);
      closeDelete();
      setSelectedRoleId(null);
      await fetchRoles();
    } catch (err) {
      setDeleting(false);
      setError(err.message || "Failed to delete role");
      closeDelete();
    }
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleDateString("en-EG", { year: "numeric", month: "short", day: "numeric" });
    } catch { return "—"; }
  };

  // Compute counts for the permission page
  const totalConfigured = Object.values(displayLevels).filter((l) => l > 0).length;
  const totalResources = (rolePerms || permTree).reduce((sum, m) => sum + (m.resources?.length || 0), 0);

  if (loading) {
    return (
      <div className="roles-page">
        <div className="roles-loading">
          <div className="roles-spinner" />
          <p>{t("loading_roles")}</p>
        </div>
      </div>
    );
  }

  if (error && roles.length === 0) {
    return (
      <div className="roles-page">
        <div className="roles-header">
          <div className="roles-header-left">
            <UserCog size={20} />
            <div>
              <h1>{t("roles")}</h1>
              <p>{t("manage_roles")}</p>
            </div>
          </div>
        </div>
        <div className="roles-error">
          <AlertTriangle size={36} className="roles-error-icon" />
          <h3>{t("failed_to_load_roles")}</h3>
          <p>{error}</p>
          <button className="roles-btn roles-btn-outline" onClick={() => fetchRoles()}>
            <><RefreshCw size={13} /> {t("retry")}</>
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="roles-page">
      <div className="roles-header">
        <div className="roles-header-left">
          <UserCog size={20} />
          <div>
            <h1>{t("roles")}</h1>
            <p>{t("manage_roles")}</p>
          </div>
        </div>
        <div className="roles-header-actions">
          <PermissionGate resource="permissions.roles" minLevel={2}>
          <button className="roles-btn roles-btn-primary" onClick={openCreate}>
            <><Plus size={14} /> {t("create_role")}</>
          </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="perm-banner perm-banner-error" style={{ marginBottom: 16 }}>
          <AlertTriangle size={15} />
          <span>{error}</span>
          <button onClick={() => setError(null)}><X size={13} /></button>
        </div>
      )}

      <div className="roles-content">
        <div className="roles-sidebar">
          <div className="roles-sidebar-header">
            <span>{t("roles_count", { count: roles.length })}</span>
          </div>
          <div style={{ padding: "6px 8px 0" }}>
            <div style={{
              display: "flex", alignItems: "center", gap: 6,
              background: "#fff", border: "1px solid #e5e7eb",
              borderRadius: 6, padding: "6px 10px",
            }}>
              <Search size={13} color="#9ca3af" />
              <input
                type="text"
                placeholder={t("search_roles")}
                value={roleSearch}
                onChange={(e) => setRoleSearch(e.target.value)}
                style={{
                  border: "none", outline: "none", flex: 1,
                  background: "transparent", fontSize: 12,
                  fontFamily: "Outfit, sans-serif", color: "#1a1f5e",
                }}
              />
            </div>
          </div>
          <div className="roles-sidebar-list">
            {filteredRoles.length === 0 && roleSearch && (
              <div style={{ padding: 20, textAlign: "center", color: "#9ca3af", fontSize: 12 }}>
                {t("no_roles_match", { search: roleSearch })}
              </div>
            )}
            {filteredRoles.map((role) => (
              <button
                key={role.id}
                className={`roles-sidebar-item ${selectedRoleId === role.id ? "active" : ""} ${role.isSystemRole ? "system-role" : ""}`}
                onClick={() => handleSelectRole(role.id)}
              >
                <span className="roles-sidebar-indicator" />
                <span style={{ flex: 1, textAlign: "left" }}>{role.name}</span>
                {role.isSystemRole && (
                  <span style={{ fontSize: 9, opacity: 0.6, textTransform: "uppercase", letterSpacing: 0.5 }}>{t("system_role_badge")}</span>
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="roles-detail">
          {!selectedRole ? (
            <div className="roles-not-selected">
              <Shield size={40} color="#d1d5db" />
              <h3>{t("select_role")}</h3>
              <p>{t("select_role_desc")}</p>
            </div>
          ) : (
            <>
              <div className="roles-detail-tabs">
                <button
                  className={`roles-detail-tab ${activeTab === "general" ? "active" : ""}`}
                  onClick={() => setActiveTab("general")}
                >
                  <><Settings size={13} /> {t("general")}</>
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "permissions" ? "active" : ""}`}
                  onClick={() => setActiveTab("permissions")}
                >
                  <><ShieldCheck size={13} /> {t("permissions")}</>
                  {permDirty && <span style={{ marginLeft: 4, width: 6, height: 6, borderRadius: "50%", background: "#c9a84c", display: "inline-block" }} />}
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "members" ? "active" : ""}`}
                  onClick={() => setActiveTab("members")}
                >
                  <><Users size={13} /> {t("members")}</>
                </button>
              </div>

              <div className="roles-detail-body">
                {activeTab === "general" && (
                  <div className="role-general-form">
                    <div className="role-form-group">
                      <label>{t("role_name")}</label>
                      <div style={{ display: "flex", gap: 8 }}>
                        <input
                          type="text"
                          className={`role-form-input ${formError ? "error" : ""}`}
                          value={formName}
                          onChange={(e) => setFormName(e.target.value)}
                          placeholder={t("role_name_placeholder")}
                          maxLength={100}
                          style={{ flex: 1 }}
                        />
                        <PermissionGate resource="permissions.roles" minLevel={3}>
                        <button
                          className="roles-btn roles-btn-primary"
                          onClick={handleUpdateName}
                          disabled={saving || formName === selectedRole.name}
                        >
                          <Save size={13} /> {saving ? t("saving") : t("save")}
                        </button>
                        </PermissionGate>
                      </div>
                      {formError && <span className="role-form-error">{formError}</span>}
                      <span className="role-form-hint">{t("role_name_hint")}</span>
                    </div>

                    {selectedRole.isSystemRole && (
                      <div className="role-badge-system">
                        <><Shield size={12} /> {t("system_role")}</>
                      </div>
                    )}

                    <p className="role-meta">
                      <Info size={12} style={{ marginRight: 4, verticalAlign: "middle" }} />
                      {t("created_at")} {formatDate(selectedRole.createdAt)}{selectedRole.isSystemRole ? ` · ${t("managed_by_system")}` : ""}
                    </p>

                    {!selectedRole.isSystemRole && (
                      <>
                        <div style={{ borderTop: "1px solid #e5e7eb", paddingTop: 16 }}>
                          <h4 style={{ fontSize: 13, fontWeight: 600, margin: "0 0 8px", color: "#dc2626" }}>{t("danger_zone")}</h4>
                          <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 12px" }}>
                            {t("delete_role_warning")}
                          </p>
                          <PermissionGate resource="permissions.roles" minLevel={5}>
                          <button className="role-delete-btn" onClick={openDelete}>
                            <><Trash2 size={13} /> {t("delete_role")}</>
                          </button>
                          </PermissionGate>
                        </div>
                      </>
                    )}
                  </div>
                )}

                {activeTab === "permissions" && (
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                      <div>
                        <h3 style={{ fontSize: 15, fontWeight: 700, margin: 0, color: "#1a1f5e" }}>
                          <ShieldCheck size={15} style={{ marginRight: 6, verticalAlign: "middle" }} />
                          {t("role_permissions_title", { name: selectedRole.name })}
                        </h3>
                        <p style={{ fontSize: 12, color: "#6b7280", margin: "4px 0 0" }}>
                          {t("resources_configured", { configured: totalConfigured, total: totalResources })}
                        </p>
                      </div>
                      <div className="roles-header-actions">
                        {permDirty && (
                          <PermissionGate resource="permissions.roles" minLevel={3}>
                          <button className="roles-btn roles-btn-outline" onClick={handleResetPermissions} disabled={permSaving}>
                            <><RotateCcw size={13} /> {t("reset")}</>
                          </button>
                          </PermissionGate>
                        )}
                        <PermissionGate resource="permissions.roles" minLevel={3}>
                        <button
                          className={`roles-btn roles-btn-primary ${!permDirty || permSaving ? "disabled" : ""}`}
                          onClick={handleSavePermissions}
                          disabled={!permDirty || permSaving}
                        >
                          {permSaving ? t("saving") : <><Save size={13} /> {t("save_changes")}</>}
                        </button>
                        </PermissionGate>
                      </div>
                    </div>

                    {permLoading ? (
                      <div className="roles-loading" style={{ padding: "40px 0" }}>
                        <div className="roles-spinner" />
                        <p>{t("loading_permissions")}</p>
                      </div>
                    ) : (
                      <div className="perm-tree-layout">
                        <div className="perm-tree-modules">
                          {(rolePerms || permTree).map((mod) => (
                            <button
                              key={mod.moduleId}
                              className={`perm-tree-module-btn ${permActiveModuleId === mod.moduleId ? "active" : ""}`}
                              onClick={() => setPermActiveModuleId(mod.moduleId)}
                            >
                              <span>{mod.moduleName}</span>
                            </button>
                          ))}
                        </div>
                        <div className="perm-tree-resources">
                          {!activeModule || activeModule.resources?.length === 0 ? (
                            <div className="perm-tree-empty">{t("no_resources_module")}</div>
                          ) : (
                            activeModule.resources.map((res) => {
                              const key = `${activeModule.moduleId}::${res.resourceId}`;
                              const currentLevel = displayLevels[key] || 0;
                              return (
                                <div key={key} className="perm-tree-resource-row">
                                  <span className="perm-tree-resource-name">{res.resourceName}</span>
                                  <div className="perm-level-selector">
                                    {ACTION_LEVELS.map((l) => {
                                      const isLevelZero = l.value === 0;
                                      const backendAction = LABEL_TO_ACTION[l.label];
                                      const isAvailable = isLevelZero || (backendAction && resourceActions[key]?.has(backendAction));
                                      const active = isLevelZero ? currentLevel === 0 : currentLevel >= l.value;
                                      return (
                                        <PermissionGate resource="permissions.roles" minLevel={3}>
                                        <button
                                          key={l.value}
                                          className={`perm-lvl-btn ${active ? "filled" : ""} ${currentLevel === l.value ? "current" : ""} ${!isAvailable ? "disabled" : ""}`}
                                          onClick={() => isAvailable && handleLevelChange(key, currentLevel === l.value ? 0 : l.value)}
                                          title={actionLevelLabels[l.value]}
                                          disabled={!isAvailable}
                                        >
                                          {actionLevelLabels[l.value]}
                                        </button>
                                        </PermissionGate>
                                      );
                                    })}
                                  </div>
                                </div>
                              );
                            })
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {activeTab === "members" && (
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                      <div>
                        <h3 style={{ fontSize: 15, fontWeight: 700, margin: 0, color: "#1a1f5e" }}>
                          <Users size={15} style={{ marginRight: 6, verticalAlign: "middle" }} />
                          {t("members_title", { name: selectedRole.name })}
                        </h3>
                        <p style={{ fontSize: 12, color: "#6b7280", margin: "4px 0 0" }}>
                          {t("members_count", { count: members.length })}
                        </p>
                      </div>
                      <PermissionGate resource="permissions.roles" minLevel={3}>
                        <button className="roles-btn roles-btn-primary" onClick={openAddMember}>
                          <><UserPlus size={13} /> {t("add_member")}</>
                        </button>
                      </PermissionGate>
                    </div>
                    <div className="role-members-list">
                      {membersLoading ? (
                        <div className="roles-loading" style={{ padding: "40px 0" }}>
                          <div className="roles-spinner" />
                          <p>{t("loading_members")}</p>
                        </div>
                      ) : members.length === 0 ? (
                        <div className="role-members-empty">
                          <Users size={32} color="#d1d5db" style={{ marginBottom: 8 }} />
                          <p>{t("no_members")}</p>
                        </div>
                      ) : (
                        members.map((m) => (
                          <div key={m.id} className="role-member-item">
                            <div className="role-member-avatar">
                              {m.name.charAt(0).toUpperCase()}
                            </div>
                            <div className="role-member-info">
                              <strong>{m.name}</strong>
                              <span>{m.email} &middot; {m.jobTitle || m.employeeCode}</span>
                            </div>
                            <div className="role-member-meta">
                              <span className="role-member-scope-label">{m.structureNodePath || t("global")}</span>
                              {m.year !== "Global" && <span className="role-member-scope">{m.year} / {m.semester}</span>}
                            </div>
                            <PermissionGate resource="permissions.roles" minLevel={3}>
                              <button
                                className="role-member-remove-btn"
                                onClick={() => openRemoveMember(m)}
                                title={t("remove_member")}
                              >
                                <UserMinus size={13} />
                              </button>
                            </PermissionGate>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      </div>

      {createModalOpen && (
        <div className="roles-modal-overlay" onClick={closeCreate}>
          <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <h2>{t("create_role")}</h2>
              <button className="roles-modal-close" onClick={closeCreate}><X size={16} /></button>
            </div>
            <form onSubmit={handleCreate}>
              <div className="roles-modal-body">
                <div className="roles-form-group">
                  <label htmlFor="role-name">{t("role_name")}</label>
                  <input
                    id="role-name"
                    type="text"
                    className={`roles-form-input ${createError ? "error" : ""}`}
                    value={createName}
                    onChange={(e) => setCreateName(e.target.value)}
                    placeholder={t("role_name_example")}
                    autoFocus
                    maxLength={100}
                  />
                  {createError && <span className="roles-form-error">{createError}</span>}
                  <span className="roles-form-hint">{t("role_name_hint")}</span>
                </div>
              </div>
              <div className="roles-modal-footer">
                <button type="button" className="roles-btn roles-btn-outline" onClick={closeCreate}>{t("cancel")}</button>
                <button type="submit" className="roles-btn roles-btn-primary" disabled={creating}>
                  {creating ? t("creating") : t("create")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {deleteTarget && (
        <div className="roles-modal-overlay" onClick={closeDelete}>
          <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <h2>{t("delete_role")}</h2>
              <button className="roles-modal-close" onClick={closeDelete}><X size={16} /></button>
            </div>
            <div className="roles-delete-body">
              <AlertTriangle size={36} className="roles-delete-icon" />
              <p>{t("delete_role_confirm", { name: deleteTarget.name })}</p>
              <p className="roles-delete-hint">{t("delete_role_warning")}</p>
            </div>
            <div className="roles-modal-footer">
              <button className="roles-btn roles-btn-outline" onClick={closeDelete} disabled={deleting}>{t("cancel")}</button>
              <button className="roles-btn roles-btn-danger" onClick={handleDelete} disabled={deleting}>
                {deleting ? t("deleting") : t("delete")}
              </button>
            </div>
          </div>
        </div>
      )}

      {addMemberModalOpen && selectedRole && (
        <div className="roles-modal-overlay" onClick={closeAddMember}>
          <div className="roles-modal roles-modal-wide" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <div className="roles-modal-header-left">
                <UserPlus size={18} color="#1a1f5e" />
                <div>
                  <h2>{t("add_member_to_role", { name: selectedRole.name })}</h2>
                  <p className="roles-modal-subtitle">{t("add_member_subtitle")}</p>
                </div>
              </div>
              <button className="roles-modal-close" onClick={closeAddMember}><X size={16} /></button>
            </div>
            <div className="roles-modal-body">
              <div className="role-add-member-search">
                <Search size={14} color="#9ca3af" />
                <input
                  type="text"
                  className="role-add-member-input"
                  value={memberSearch}
                  onChange={(e) => handleMemberSearch(e.target.value)}
                  placeholder={t("search_staff_placeholder")}
                  autoFocus
                />
                {memberSearching && <Loader2 size={14} className="roles-spinner-inline" />}
              </div>
              <div className="role-add-member-results">
                {memberSearchResults.length === 0 && memberSearch.trim() && !memberSearching ? (
                  <div className="role-add-member-empty">
                    <Users size={24} color="#d1d5db" />
                    <p>{t("no_staff_found")}</p>
                  </div>
                ) : memberSearchResults.length === 0 && !memberSearch.trim() && !memberSearching ? (
                  <div className="role-add-member-empty">
                    <UserPlus size={24} color="#d1d5db" />
                    <p>{t("type_to_search_staff")}</p>
                  </div>
                ) : (
                  memberSearchResults.map((s) => (
                    <div key={s.id} className="role-add-member-result-item">
                      <div className="role-member-avatar">
                        {s.name.charAt(0).toUpperCase()}
                      </div>
                      <div className="role-member-info">
                        <strong>{s.name}</strong>
                        <span>{s.email} &middot; {s.jobTitle || s.employeeCode}</span>
                      </div>
                      <button
                        className="role-add-member-add-btn"
                        onClick={() => handleAddMember(s.id)}
                        disabled={addingMember}
                      >
                        {addingMember ? (
                          <Loader2 size={13} className="roles-spinner-inline" />
                        ) : (
                          <><UserPlus size={13} /> {t("add")}</>
                        )}
                      </button>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {removeMemberTarget && (
        <div className="roles-modal-overlay" onClick={closeRemoveMember}>
          <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <h2>{t("remove_member")}</h2>
              <button className="roles-modal-close" onClick={closeRemoveMember}><X size={16} /></button>
            </div>
            <div className="roles-delete-body">
              <UserMinus size={36} className="roles-remove-member-icon" />
              <p>{t("remove_member_confirm", { name: removeMemberTarget.name })}</p>
              <p className="roles-delete-hint">{t("remove_member_warning", { role: selectedRole?.name })}</p>
            </div>
            <div className="roles-modal-footer">
              <button className="roles-btn roles-btn-outline" onClick={closeRemoveMember} disabled={removingMember}>{t("cancel")}</button>
              <button className="roles-btn roles-btn-danger" onClick={handleRemoveMember} disabled={removingMember}>
                {removingMember ? t("removing") : t("remove")}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default RolesPage;
