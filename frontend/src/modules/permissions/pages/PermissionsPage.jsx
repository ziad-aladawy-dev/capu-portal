import { useState, useCallback, useEffect, useMemo } from "react";
import {
  Shield, X, Save, RotateCcw, User, AlertTriangle,
  Building2, CheckCircle, Globe, Lock, Unlock,
} from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import * as authorizationService from "../../../core/services/authorizationService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import ScopeMultiSelectModal from "../../../core/components/ScopeMultiSelectModal";
import "../styles/permissions.css";

const ACTION_LEVELS = [
  { value: 0, label: "None" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];

function PermissionsPage() {
  const { scopedUser, isScoped } = useUserScope();
  const [selectedUser, setSelectedUser] = useState(null);

  const [allRoles, setAllRoles] = useState([]);
  const [assignedRoleIds, setAssignedRoleIds] = useState([]);

  // Permission tree for the override UI
  const [permissionTree, setPermissionTree] = useState([]);
  const [activeModuleId, setActiveModuleId] = useState(null);

  // Override configs: keyed by resourceId → { level, type, scopeNodes }
  const [overrideConfigs, setOverrideConfigs] = useState({});
  const [alwaysActive, setAlwaysActive] = useState(true);

  // Scope modal state — tracks which resource we're picking scope for
  const [scopeModalTarget, setScopeModalTarget] = useState(null); // resourceId or null

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  // Load roles
  useEffect(() => {
    permissionService.fetchAllRoles({ pageSize: 100 }).then((res) => {
      setAllRoles(res?.items || []);
    });
  }, []);

  // Load permission tree
  useEffect(() => {
    authorizationService.fetchPermissionTree().then((tree) => {
      const modules = Array.isArray(tree) ? tree : [];
      setPermissionTree(modules);
      if (modules.length > 0 && !activeModuleId) {
        setActiveModuleId(modules[0].moduleId);
      }
    }).catch(() => setPermissionTree([]));
  }, []);

  const loadAssignment = useCallback(async (userId) => {
    setLoading(true);
    setError(null);
    try {
      const assignment = await permissionService.fetchPermissionAssignment({ userId });
      setAssignedRoleIds((assignment?.roleIds || []).map(String));

      // Build overrideConfigs from existing overrides
      const configs = {};
      for (const ov of (assignment?.permissionOverrides || [])) {
        configs[String(ov.resourceId)] = {
          level: ov.level || 0,
          type: ov.type || 1,
          scopeNodes: [],
        };
      }
      setOverrideConfigs(configs);
      setDirty(false);
    } catch (err) {
      setError(err.message || "Failed to load assignment");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (isScoped && scopedUser && scopedUser.id !== selectedUser?.id) {
      setSelectedUser(scopedUser);
      loadAssignment(scopedUser.id);
    }
  }, [scopedUser?.id, isScoped, selectedUser?.id, loadAssignment]);

  useEffect(() => {
    if (!isScoped && selectedUser) {
      setSelectedUser(null);
      setAssignedRoleIds([]);
      setOverrideConfigs({});
    }
  }, [isScoped]);

  // Active module data
  const activeModule = useMemo(
    () => permissionTree.find((m) => m.moduleId === activeModuleId),
    [permissionTree, activeModuleId]
  );

  // Count overrides per module for badges
  const overrideCountByModule = useMemo(() => {
    const counts = {};
    for (const mod of permissionTree) {
      let count = 0;
      for (const res of (mod.resources || [])) {
        if (overrideConfigs[String(res.resourceId)]) count++;
      }
      counts[mod.moduleId] = count;
    }
    return counts;
  }, [permissionTree, overrideConfigs]);

  const markDirty = () => { setDirty(true); setSaved(false); };

  const toggleRole = (roleId) => {
    const s = String(roleId);
    setAssignedRoleIds((prev) => prev.includes(s) ? prev.filter((id) => id !== s) : [...prev, s]);
    markDirty();
  };

  // --- Override helpers ---
  const toggleOverride = (resourceId) => {
    const key = String(resourceId);
    setOverrideConfigs((prev) => {
      if (prev[key]) {
        const next = { ...prev };
        delete next[key];
        return next;
      }
      return { ...prev, [key]: { level: 1, type: 1, scopeNodes: [] } };
    });
    markDirty();
  };

  const setOverrideLevel = (resourceId, level) => {
    const key = String(resourceId);
    setOverrideConfigs((prev) => {
      if (!prev[key]) return prev;
      return { ...prev, [key]: { ...prev[key], level } };
    });
    markDirty();
  };

  const setOverrideType = (resourceId, type) => {
    const key = String(resourceId);
    setOverrideConfigs((prev) => {
      if (!prev[key]) return prev;
      return { ...prev, [key]: { ...prev[key], type } };
    });
    markDirty();
  };

  const openScopeFor = (resourceId) => {
    setScopeModalTarget(resourceId);
  };

  const handleScopeApply = useCallback((ids, nodes) => {
    const target = scopeModalTarget;
    if (!target) return;
    const nodeObjs = nodes || ids.map((id) => ({ id, name: id }));
    setOverrideConfigs((prev) => {
      if (!prev[target]) return prev;
      return { ...prev, [target]: { ...prev[target], scopeNodes: nodeObjs } };
    });
    markDirty();
    setScopeModalTarget(null);
  }, [scopeModalTarget]);

  const removeScopeNode = (resourceId, nodeId) => {
    const key = String(resourceId);
    setOverrideConfigs((prev) => {
      if (!prev[key]) return prev;
      return {
        ...prev,
        [key]: {
          ...prev[key],
          scopeNodes: prev[key].scopeNodes.filter((n) => n.id !== nodeId),
        },
      };
    });
    markDirty();
  };

  const handleReset = () => {
    if (selectedUser) loadAssignment(selectedUser.id);
  };

  const handleSave = async () => {
    if (!selectedUser) return;
    setSaving(true);
    setError(null);
    try {
      // Get current state from server
      const current = await permissionService.fetchPermissionAssignment({ userId: selectedUser.id });
      const currentRoleIds = (current?.roleIds || []).map(String);
      const currentOverrides = current?.permissionOverrides || [];

      const rolesToAdd = assignedRoleIds.filter((id) => !currentRoleIds.includes(id));
      const rolesToRemove = currentRoleIds.filter((id) => !assignedRoleIds.includes(id));

      // Build new overrides list
      const newOverrides = Object.entries(overrideConfigs).map(([resourceId, cfg]) => ({
        resourceId,
        level: cfg.level,
        type: cfg.type,
        scopeNodes: cfg.scopeNodes,
      }));

      // Determine adds and removes
      const currentKeySet = new Set(currentOverrides.map((o) => `${String(o.resourceId)}|${o.type}`));
      const newKeySet = new Set(newOverrides.map((o) => `${o.resourceId}|${o.type}`));

      const permissionsToAdd = newOverrides.filter((o) => !currentKeySet.has(`${o.resourceId}|${o.type}`));
      const permissionsToRemove = currentOverrides.filter((o) => !newKeySet.has(`${String(o.resourceId)}|${o.type}`));

      // Group overrides by scope for batched API calls
      const scopeGroups = new Map();
      for (const ov of newOverrides) {
        const scopeKey = ov.scopeNodes.length === 0
          ? "__global__"
          : ov.scopeNodes.map((n) => n.id).sort().join(",");
        if (!scopeGroups.has(scopeKey)) {
          scopeGroups.set(scopeKey, { nodeIds: ov.scopeNodes.map((n) => n.id), perms: [] });
        }
        scopeGroups.get(scopeKey).perms.push({ resourceId: ov.resourceId, level: ov.level, type: ov.type });
      }

      // If no scope groups exist, still send role changes
      if (scopeGroups.size === 0) {
        scopeGroups.set("__global__", { nodeIds: [], perms: [] });
      }

      let first = true;
      for (const [, group] of scopeGroups) {
        const scopes = group.nodeIds.length > 0 ? group.nodeIds : [null];
        for (const nodeId of scopes) {
          await permissionService.updatePermissionAssignment({
            userId: selectedUser.id,
            rolesToAdd: first ? rolesToAdd : [],
            rolesToRemove: first ? rolesToRemove : [],
            permissionsToAdd: first ? permissionsToAdd.filter((p) =>
              group.perms.some((gp) => gp.resourceId === p.resourceId)
            ).map((o) => ({ resourceId: o.resourceId, level: o.level, type: o.type })) : [],
            permissionsToRemove: first ? permissionsToRemove.map((o) => ({
              resourceId: String(o.resourceId), level: o.level, type: o.type,
            })) : [],
            structuralScope: { structureNodeId: nodeId },
            temporalScope: { academicYearId: null, semesterId: null, alwaysActive },
          });
          first = false;
        }
      }

      setDirty(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (err) {
      setError(err.message || "Failed to save");
    } finally {
      setSaving(false);
    }
  };

  const selectedUserName = selectedUser?.name || "";
  const selectedUserCode = selectedUser?.code || "";
  const selectedUserType = selectedUser?.type || "";

  return (
    <div className="permissions-page">
      <div className="perm-header">
        <div className="perm-header-left">
          <Shield size={20} />
          <div>
            <h1>Permissions Manager</h1>
            <p>Manage user role assignments and permission overrides</p>
          </div>
        </div>
        <div className="perm-header-actions">
          {dirty && (
            <button className="perm-btn perm-btn-outline" onClick={handleReset}>
              <RotateCcw size={13} /> Reset
            </button>
          )}
          <button
            className={`perm-btn perm-btn-primary ${!dirty || saving ? "disabled" : ""}`}
            onClick={handleSave}
            disabled={!dirty || saving || !selectedUser}
          >
            {saving ? "Saving\u2026" : saved ? "Saved!" : <><Save size={13} /> Save Changes</>}
          </button>
        </div>
      </div>

      {error && (
        <div className="perm-error-banner">
          <AlertTriangle size={15} />
          <span>{error}</span>
          <button onClick={() => setError(null)}><X size={13} /></button>
        </div>
      )}
      {saved && (
        <div className="perm-success-banner">
          <CheckCircle size={15} /><span>Permissions saved successfully</span>
        </div>
      )}

      <div className="perm-stack">
        {/* User Card */}
        {selectedUser ? (
          <div className="perm-user-card">
            <div className="perm-user-avatar">{selectedUserName.charAt(0)}</div>
            <div className="perm-user-info">
              <strong>{selectedUserName}</strong>
              <span>{selectedUserCode} · {selectedUserType === "staff" ? "Staff" : "Student"}</span>
            </div>
          </div>
        ) : (
          <div className="perm-empty-state" style={{ padding: "40px 20px" }}>
            <User size={36} />
            <h3>Select a User</h3>
            <p>Search for a user from the sidebar and click to select.</p>
          </div>
        )}

        {selectedUser && !loading && (
          <>
            {/* Role Assignments */}
            <div className="perm-content-card">
              <h3 className="perm-section-title">
                <Shield size={16} /> Role Assignments
              </h3>
              <p className="perm-section-desc">Select the roles assigned to this user.</p>
              <div className="perm-role-grid">
                {allRoles.map((role) => {
                  const isAssigned = assignedRoleIds.includes(String(role.id));
                  return (
                    <button
                      key={role.id}
                      className={`perm-role-chip ${isAssigned ? "is-assigned" : ""} ${role.isSystemRole ? "is-system" : ""}`}
                      onClick={() => toggleRole(role.id)}
                    >
                      <span className="perm-role-chip-check">{isAssigned ? <CheckCircle size={11} /> : null}</span>
                      <span className="perm-role-chip-name">{role.name}</span>
                      {role.isSystemRole && <span className="perm-role-chip-badge">system</span>}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Permission Overrides — Tab + Card design */}
            <div className="perm-content-card perm-overrides-panel">
              <div className="perm-overrides-header">
                <div>
                  <h3 className="perm-section-title">
                    <AlertTriangle size={16} /> Permission Overrides
                  </h3>
                  <p className="perm-section-desc">
                    Configure per-resource overrides. Each resource can have its own scope.
                  </p>
                </div>
                <label className="perm-scope-check">
                  <input
                    type="checkbox"
                    checked={alwaysActive}
                    onChange={(e) => { setAlwaysActive(e.target.checked); markDirty(); }}
                  />
                  <span>Always Active</span>
                </label>
              </div>

              {/* Module Tabs */}
              <div className="perm-module-tabs">
                {permissionTree.map((mod) => {
                  const count = overrideCountByModule[mod.moduleId] || 0;
                  return (
                    <button
                      key={mod.moduleId}
                      className={`perm-module-tab ${activeModuleId === mod.moduleId ? "active" : ""}`}
                      onClick={() => setActiveModuleId(mod.moduleId)}
                    >
                      <span>{mod.moduleName}</span>
                      {count > 0 && <span className="perm-module-tab-badge">{count}</span>}
                    </button>
                  );
                })}
              </div>

              {/* Resource Cards */}
              <div className="perm-resource-list">
                {activeModule?.resources?.length === 0 && (
                  <div className="perm-resource-empty">No resources in this module.</div>
                )}
                {(activeModule?.resources || []).map((res) => {
                  const key = String(res.resourceId);
                  const config = overrideConfigs[key];
                  const isEnabled = !!config;

                  return (
                    <div key={key} className={`perm-resource-card ${isEnabled ? "is-active" : ""}`}>
                      <div className="perm-resource-top">
                        <button
                          className={`perm-resource-toggle ${isEnabled ? "on" : ""}`}
                          onClick={() => toggleOverride(res.resourceId)}
                          title={isEnabled ? "Remove override" : "Add override"}
                        >
                          <span className="perm-toggle-track">
                            <span className="perm-toggle-thumb" />
                          </span>
                        </button>
                        <span className="perm-resource-name">{res.resourceName}</span>

                        {isEnabled && (
                          <div className="perm-resource-type-group">
                            <button
                              className={`perm-type-btn allow ${config.type === 1 ? "active" : ""}`}
                              onClick={() => setOverrideType(res.resourceId, 1)}
                            >
                              <Unlock size={10} /> Allow
                            </button>
                            <button
                              className={`perm-type-btn deny ${config.type === 2 ? "active" : ""}`}
                              onClick={() => setOverrideType(res.resourceId, 2)}
                            >
                              <Lock size={10} /> Deny
                            </button>
                          </div>
                        )}
                      </div>

                      {isEnabled && (
                        <div className="perm-resource-body">
                          {/* Action level pills */}
                          <div className="perm-action-row">
                            <span className="perm-action-label">Level</span>
                            <div className="perm-action-pills">
                              {ACTION_LEVELS.filter((l) => l.value > 0).map((l) => (
                                <button
                                  key={l.value}
                                  className={`perm-action-pill ${config.level >= l.value ? "filled" : ""} ${config.level === l.value ? "current" : ""}`}
                                  onClick={() => setOverrideLevel(res.resourceId, l.value)}
                                  title={`${l.label} (${l.value})`}
                                >
                                  {l.label}
                                </button>
                              ))}
                            </div>
                          </div>

                          {/* Per-override scope */}
                          <div className="perm-resource-scope">
                            <Building2 size={12} className="perm-scope-icon" />
                            {config.scopeNodes.length === 0 ? (
                              <span className="perm-scope-all-label">All scopes</span>
                            ) : (
                              <div className="perm-scope-chips">
                                {config.scopeNodes.map((n) => (
                                  <span key={n.id} className="perm-scope-chip">
                                    {n.name}
                                    <button onClick={() => removeScopeNode(res.resourceId, n.id)}>
                                      <X size={9} />
                                    </button>
                                  </span>
                                ))}
                              </div>
                            )}
                            <button
                              className="perm-scope-add-btn"
                              onClick={() => openScopeFor(key)}
                            >
                              {config.scopeNodes.length === 0 ? "Narrow scope" : "Edit"}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          </>
        )}

        {selectedUser && loading && (
          <div className="perm-loading">Loading permissions\u2026</div>
        )}
      </div>

      {/* Scope Modal */}
      {scopeModalTarget && (
        <ScopeMultiSelectModal
          initialSelectedIds={(overrideConfigs[scopeModalTarget]?.scopeNodes || []).map((n) => n.id)}
          onApply={handleScopeApply}
          onClose={() => setScopeModalTarget(null)}
        />
      )}
    </div>
  );
}

export default PermissionsPage;