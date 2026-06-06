import { useState, useEffect, useCallback, useMemo } from "react";
import { UserCog, Plus, Shield, ShieldCheck, Trash2, AlertTriangle, RefreshCw, Save, Info, Users, Settings, X, Search, RotateCcw } from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import * as authorizationService from "../../../core/services/authorizationService";
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
      const permissions = Object.entries(displayLevels)
        .filter(([, level]) => level > 0)
        .map(([compositeKey, level]) => {
          const resourceId = compositeKey.split("::")[1];
          return { resourceId, level };
        });
      await permissionService.updateRolePermissions(selectedRole.id, { permissions });
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
    if (!trimmed) { setCreateError("Role name is required"); return; }
    if (trimmed.length < 2) { setCreateError("Role name must be at least 2 characters"); return; }
    if (trimmed.length > 100) { setCreateError("Role name must be 100 characters or fewer"); return; }
    setCreating(true);
    try {
      await permissionService.createRole({ name: trimmed });
      closeCreate();
      await fetchRoles();
    } catch (err) {
      if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
        setCreateError("A role with this name already exists");
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
    if (!trimmed) { setFormError("Role name is required"); return; }
    if (trimmed.length < 2) { setFormError("Role name must be at least 2 characters"); return; }
    if (trimmed.length > 100) { setFormError("Role name must be 100 characters or fewer"); return; }
    setSaving(true);
    try {
      await permissionService.updateRole(selectedRole.id, { name: trimmed });
      setFormError("");
      await fetchRoles();
    } catch (err) {
      if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
        setFormError("A role with this name already exists");
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
          <p>Loading roles…</p>
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
              <h1>Roles</h1>
              <p>Manage role definitions and system roles</p>
            </div>
          </div>
        </div>
        <div className="roles-error">
          <AlertTriangle size={36} className="roles-error-icon" />
          <h3>Failed to load roles</h3>
          <p>{error}</p>
          <button className="roles-btn roles-btn-outline" onClick={() => fetchRoles()}>
            <RefreshCw size={13} /> Retry
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
            <h1>Roles</h1>
            <p>Manage role definitions and system roles</p>
          </div>
        </div>
        <div className="roles-header-actions">
          <button className="roles-btn roles-btn-primary" onClick={openCreate}>
            <Plus size={14} /> Create Role
          </button>
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
            <span>{roles.length} roles</span>
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
                placeholder="Search roles…"
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
                No roles match "{roleSearch}"
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
                  <span style={{ fontSize: 9, opacity: 0.6, textTransform: "uppercase", letterSpacing: 0.5 }}>System</span>
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="roles-detail">
          {!selectedRole ? (
            <div className="roles-not-selected">
              <Shield size={40} color="#d1d5db" />
              <h3>Select a Role</h3>
              <p>Choose a role from the sidebar to view and manage its settings.</p>
            </div>
          ) : (
            <>
              <div className="roles-detail-tabs">
                <button
                  className={`roles-detail-tab ${activeTab === "general" ? "active" : ""}`}
                  onClick={() => setActiveTab("general")}
                >
                  <Settings size={13} /> General
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "permissions" ? "active" : ""}`}
                  onClick={() => setActiveTab("permissions")}
                >
                  <ShieldCheck size={13} /> Permissions
                  {permDirty && <span style={{ marginLeft: 4, width: 6, height: 6, borderRadius: "50%", background: "#c9a84c", display: "inline-block" }} />}
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "members" ? "active" : ""}`}
                  onClick={() => setActiveTab("members")}
                >
                  <Users size={13} /> Members
                </button>
              </div>

              <div className="roles-detail-body">
                {activeTab === "general" && (
                  <div className="role-general-form">
                    <div className="role-form-group">
                      <label>Role Name</label>
                      <div style={{ display: "flex", gap: 8 }}>
                        <input
                          type="text"
                          className={`role-form-input ${formError ? "error" : ""}`}
                          value={formName}
                          onChange={(e) => setFormName(e.target.value)}
                          placeholder="Role name"
                          maxLength={100}
                          style={{ flex: 1 }}
                        />
                        <button
                          className="roles-btn roles-btn-primary"
                          onClick={handleUpdateName}
                          disabled={saving || formName === selectedRole.name}
                        >
                          <Save size={13} /> {saving ? "Saving…" : "Save"}
                        </button>
                      </div>
                      {formError && <span className="role-form-error">{formError}</span>}
                      <span className="role-form-hint">Must be unique and 2–100 characters.</span>
                    </div>

                    {selectedRole.isSystemRole && (
                      <div className="role-badge-system">
                        <Shield size={12} /> System Role
                      </div>
                    )}

                    <p className="role-meta">
                      <Info size={12} style={{ marginRight: 4, verticalAlign: "middle" }} />
                      Created {formatDate(selectedRole.createdAt)} {selectedRole.isSystemRole ? "· Managed by system" : ""}
                    </p>

                    {!selectedRole.isSystemRole && (
                      <>
                        <div style={{ borderTop: "1px solid #e5e7eb", paddingTop: 16 }}>
                          <h4 style={{ fontSize: 13, fontWeight: 600, margin: "0 0 8px", color: "#dc2626" }}>Danger Zone</h4>
                          <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 12px" }}>
                            Deleting this role cannot be undone. Users assigned to this role will lose its permissions.
                          </p>
                          <button className="role-delete-btn" onClick={openDelete}>
                            <Trash2 size={13} /> Delete Role
                          </button>
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
                          Role Permissions — {selectedRole.name}
                        </h3>
                        <p style={{ fontSize: 12, color: "#6b7280", margin: "4px 0 0" }}>
                          {totalConfigured} of {totalResources} resources configured
                        </p>
                      </div>
                      <div className="roles-header-actions">
                        {permDirty && (
                          <button className="roles-btn roles-btn-outline" onClick={handleResetPermissions} disabled={permSaving}>
                            <RotateCcw size={13} /> Reset
                          </button>
                        )}
                        <button
                          className={`roles-btn roles-btn-primary ${!permDirty || permSaving ? "disabled" : ""}`}
                          onClick={handleSavePermissions}
                          disabled={!permDirty || permSaving}
                        >
                          {permSaving ? "Saving\u2026" : <><Save size={13} /> Save Changes</>}
                        </button>
                      </div>
                    </div>

                    {permLoading ? (
                      <div className="roles-loading" style={{ padding: "40px 0" }}>
                        <div className="roles-spinner" />
                        <p>Loading permissions…</p>
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
                            <div className="perm-tree-empty">No resources in this module.</div>
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
                                        <button
                                          key={l.value}
                                          className={`perm-lvl-btn ${active ? "filled" : ""} ${currentLevel === l.value ? "current" : ""} ${!isAvailable ? "disabled" : ""}`}
                                          onClick={() => isAvailable && handleLevelChange(key, currentLevel === l.value ? 0 : l.value)}
                                          title={l.label}
                                          disabled={!isAvailable}
                                        >
                                          {l.label}
                                        </button>
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
                          Members — {selectedRole.name}
                        </h3>
                        <p style={{ fontSize: 12, color: "#6b7280", margin: "4px 0 0" }}>
                          {members.length} staff member{members.length !== 1 ? "s" : ""} assigned to this role
                        </p>
                      </div>
                    </div>
                    <div className="role-members-list">
                      {membersLoading ? (
                        <div className="roles-loading" style={{ padding: "40px 0" }}>
                          <div className="roles-spinner" />
                          <p>Loading members…</p>
                        </div>
                      ) : members.length === 0 ? (
                        <div className="role-members-empty">
                          <Users size={32} color="#d1d5db" style={{ marginBottom: 8 }} />
                          <p>No members assigned to this role.</p>
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
                              <span className="role-member-scope-label">{m.structureNodePath || "Global"}</span>
                              {m.year !== "Global" && <span className="role-member-scope">{m.year} / {m.semester}</span>}
                            </div>
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
              <h2>Create Role</h2>
              <button className="roles-modal-close" onClick={closeCreate}><X size={16} /></button>
            </div>
            <form onSubmit={handleCreate}>
              <div className="roles-modal-body">
                <div className="roles-form-group">
                  <label htmlFor="role-name">Role Name</label>
                  <input
                    id="role-name"
                    type="text"
                    className={`roles-form-input ${createError ? "error" : ""}`}
                    value={createName}
                    onChange={(e) => setCreateName(e.target.value)}
                    placeholder="e.g. Department Head"
                    autoFocus
                    maxLength={100}
                  />
                  {createError && <span className="roles-form-error">{createError}</span>}
                  <span className="roles-form-hint">Must be unique and 2–100 characters.</span>
                </div>
              </div>
              <div className="roles-modal-footer">
                <button type="button" className="roles-btn roles-btn-outline" onClick={closeCreate}>Cancel</button>
                <button type="submit" className="roles-btn roles-btn-primary" disabled={creating}>
                  {creating ? "Creating…" : "Create"}
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
              <h2>Delete Role</h2>
              <button className="roles-modal-close" onClick={closeDelete}><X size={16} /></button>
            </div>
            <div className="roles-delete-body">
              <AlertTriangle size={36} className="roles-delete-icon" />
              <p>Are you sure you want to delete <strong>{deleteTarget.name}</strong>?</p>
              <p className="roles-delete-hint">This action cannot be undone. Any users assigned to this role will lose its permissions.</p>
            </div>
            <div className="roles-modal-footer">
              <button className="roles-btn roles-btn-outline" onClick={closeDelete} disabled={deleting}>Cancel</button>
              <button className="roles-btn roles-btn-danger" onClick={handleDelete} disabled={deleting}>
                {deleting ? "Deleting…" : "Delete"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default RolesPage;
