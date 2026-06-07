import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import {
  Shield, Save, RotateCcw, User, Search, X, Check,
  Undo2, ShieldCheck, CheckSquare,
} from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import * as authorizationService from "../../../core/services/authorizationService";
import * as staffService from "../../../core/services/staffService";
import * as studentService from "../../../core/services/studentService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import { useToast } from "../../../core/components/Toast";
import "../styles/permissions.css";
import { useTranslation } from "react-i18next";

const ACTION_VALUES = { View: 1, Insert: 2, EditClose: 3, Open: 4, Delete: 5 };
const LEVEL_TO_ACTION = { 1: "View", 2: "Insert", 3: "EditClose", 4: "Open", 5: "Delete" };
const LEVELS = [
  { value: 0, label: "No Permission" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];
const LEVEL_LABELS = { 0: "No Permission", 1: "View", 2: "Insert", 3: "Edit", 4: "Open", 5: "Delete" };
const LABEL_TO_ACTION = {
  View: "View",
  Insert: "Insert",
  Edit: "EditClose",
  Open: "Open",
  Delete: "Delete",
};

function PermissionsPage() {
  const { scopedUser, isScoped, scopeToUser } = useUserScope();
  const { addToast } = useToast();
  const { t } = useTranslation();
  const levelLabels = {
    0: t("no_permission"),
    1: t("view"),
    2: t("insert"),
    3: t("edit"),
    4: t("open_level"),
    5: t("delete"),
  };
  const [selectedUser, setSelectedUser] = useState(null);

  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const debounceRef = useRef(null);

  const [allRoles, setAllRoles] = useState([]);
  const [assignedRoleIds, setAssignedRoleIds] = useState([]);
  const [initialRoleIds, setInitialRoleIds] = useState([]);

  const [userTree, setUserTree] = useState([]);
  const [activeModuleId, setActiveModuleId] = useState(null);

  const [originalSnapshot, setOriginalSnapshot] = useState(null);
  const [pendingLevels, setPendingLevels] = useState({});
  const [resourceOverrides, setResourceOverrides] = useState({});

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    permissionService.fetchAllRoles({ pageSize: 100 }).then((res) => {
      setAllRoles(res?.items || []);
    });
  }, []);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (!searchQuery.trim()) { setSearchResults([]); return; }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const [staffRes, studentRes] = await Promise.all([
          staffService.searchStaff({ search: searchQuery, page: 1, pageSize: 10 }),
          studentService.searchStudents({ search: searchQuery, page: 1, pageSize: 10 }),
        ]);
        const staff = (staffRes?.items || []).map((s) => ({ id: s.id, name: s.name, code: s.employeeCode, type: "staff" }));
        const students = (studentRes?.items || []).map((s) => ({ id: s.id, name: s.name, code: s.studentCode, type: "student" }));
        setSearchResults([...staff, ...students]);
      } catch { setSearchResults([]); }
      finally { setSearching(false); }
    }, 300);
  }, [searchQuery]);

  const loadUserTree = useCallback(async (userId) => {
    setLoading(true);
    setDirty(false);
    setPendingLevels({});
    try {
      const [tree, assignment] = await Promise.all([
        authorizationService.fetchUserPermissionTree(userId),
        permissionService.fetchPermissionAssignment({ userId, alwaysActive: true }),
      ]);
      const modules = Array.isArray(tree) ? tree : [];
      setUserTree(modules);
      setActiveModuleId((prev) => {
        if (prev && modules.some((m) => m.moduleId === prev)) return prev;
        return modules.length > 0 ? modules[0].moduleId : null;
      });
      setAssignedRoleIds((assignment?.roleIds || []).map(String));
      setInitialRoleIds((assignment?.roleIds || []).map(String));

      const snap = {};
      const resOv = {};
      for (const mod of modules) {
        for (const res of (mod.resources || [])) {
          const rid = String(res.resourceId);
          const overrides = { allow: [], deny: [] };
          for (const p of (res.permissions || [])) {
            snap[p.permissionId] = {
              resourceId: res.resourceId,
              action: p.action,
              isAssigned: p.isAssigned,
              hasAllowOverride: p.hasAllowOverride,
              hasDenyOverride: p.hasDenyOverride,
            };
            if (p.hasAllowOverride) overrides.allow.push(p.action);
            if (p.hasDenyOverride) overrides.deny.push(p.action);
          }
          resOv[rid] = overrides;
        }
      }
      setOriginalSnapshot(snap);
      setResourceOverrides(resOv);
    } catch (err) {
      addToast({ title: t("load_failed"), message: err.message || "Failed to load permissions" }, "error");
    } finally {
      setLoading(false);
    }
  }, []);

  const handleSelectUser = (user) => {
    scopeToUser(user);
    setSelectedUser(user);
    setSearchQuery("");
    setSearchResults([]);
    loadUserTree(user.id);
  };

  useEffect(() => {
    if (isScoped && scopedUser && scopedUser.id !== selectedUser?.id) {
      setSelectedUser(scopedUser);
      loadUserTree(scopedUser.id);
    }
  }, [scopedUser?.id, isScoped, selectedUser?.id, loadUserTree]);

  useEffect(() => {
    if (!isScoped && selectedUser) {
      setSelectedUser(null);
      setUserTree([]);
      setAssignedRoleIds([]);
      setInitialRoleIds([]);
      setPendingLevels({});
      setOriginalSnapshot(null);
    }
  }, [isScoped]);

  const activeModule = useMemo(
    () => userTree.find((m) => m.moduleId === activeModuleId),
    [userTree, activeModuleId]
  );

  const resourceLevels = useMemo(() => {
    const map = {};
    for (const mod of userTree) {
      for (const res of (mod.resources || [])) {
        const rid = String(res.resourceId);
        let roleLvl = 0, effLvl = 0;
        for (const p of (res.permissions || [])) {
          const orig = originalSnapshot?.[p.permissionId];
          if (!orig) continue;
          const val = ACTION_VALUES[orig.action] || 0;
          if (orig.isAssigned) effLvl = Math.max(effLvl, val);
          let roleAssg;
          if (orig.hasAllowOverride) roleAssg = false;
          else if (orig.hasDenyOverride) roleAssg = true;
          else roleAssg = orig.isAssigned;
          if (roleAssg) roleLvl = Math.max(roleLvl, val);
        }
        map[rid] = { effectiveLevel: effLvl, roleBasedLevel: roleLvl };
      }
    }
    return map;
  }, [userTree, originalSnapshot]);

  const resourceActions = useMemo(() => {
    const map = {};
    for (const mod of userTree) {
      for (const res of (mod.resources || [])) {
        const rid = String(res.resourceId);
        const actions = new Set();
        for (const p of (res.permissions || [])) {
          actions.add(p.action);
        }
        map[rid] = actions;
      }
    }
    return map;
  }, [userTree]);

  const getDisplayLevel = useCallback((resId) => {
    if (pendingLevels[resId] !== undefined) return pendingLevels[resId];
    return resourceLevels[resId]?.effectiveLevel || 0;
  }, [pendingLevels, resourceLevels]);

  const resourceHasOverride = useCallback((resId) => {
    if (pendingLevels[resId] !== undefined) return true;
    const ov = resourceOverrides[resId];
    return ov && (ov.allow.length > 0 || ov.deny.length > 0);
  }, [pendingLevels, resourceOverrides]);

  const overrideCountByModule = useMemo(() => {
    const counts = {};
    for (const mod of userTree) {
      let count = 0;
      for (const res of (mod.resources || [])) {
        if (resourceHasOverride(String(res.resourceId))) count++;
      }
      counts[mod.moduleId] = count;
    }
    return counts;
  }, [userTree, resourceHasOverride]);

  const markDirty = () => { setDirty(true); };

  const setLevel = (resId, desired, roleBasedLevel) => {
    if (desired === roleBasedLevel) {
      const next = { ...pendingLevels };
      delete next[resId];
      setPendingLevels(next);
    } else {
      setPendingLevels((prev) => ({ ...prev, [resId]: desired }));
    }
    markDirty();
  };

  const handleRevert = (resId) => {
    const next = { ...pendingLevels };
    delete next[resId];
    setPendingLevels(next);
    markDirty();
  };

  const toggleRole = (roleId) => {
    const s = String(roleId);
    setAssignedRoleIds((prev) => prev.includes(s) ? prev.filter((id) => id !== s) : [...prev, s]);
    markDirty();
  };

  const handleReset = () => {
    if (selectedUser) loadUserTree(selectedUser.id);
  };

  const handleSave = async () => {
    if (!selectedUser) return;
    setSaving(true);
    try {
      const toAdd = [];
      const toRemove = [];

      for (const [resId, desired] of Object.entries(pendingLevels)) {
        const roleLvl = resourceLevels[resId]?.roleBasedLevel || 0;
        const ov = resourceOverrides[resId];

        if (ov) {
          for (const action of ov.allow) toRemove.push({ resourceId: resId, actions: [action], type: 1 });
          for (const action of ov.deny) toRemove.push({ resourceId: resId, actions: [action], type: 2 });
        }

        if (desired > roleLvl) {
          const action = LEVEL_TO_ACTION[desired];
          if (action) toAdd.push({ resourceId: resId, actions: [action], type: 1 });
        } else if (desired < roleLvl) {
          const action = LEVEL_TO_ACTION[desired + 1];
          if (action) toAdd.push({ resourceId: resId, actions: [action], type: 2 });
        }
      }

      // Use the snapshot loaded at init time for role diffing — no re-fetch needed.
      const rolesToAdd = assignedRoleIds.filter((id) => !initialRoleIds.includes(id));
      const rolesToRemove = initialRoleIds.filter((id) => !assignedRoleIds.includes(id));

      await permissionService.updatePermissionAssignment({
        userId: selectedUser.id,
        rolesToAdd,
        rolesToRemove,
        permissionsToAdd: toAdd,
        permissionsToRemove: toRemove,
        structuralScope: { structureNodeId: null },
        temporalScope: { academicYearId: null, semesterId: null, alwaysActive: true },
      });

      setDirty(false);
      setPendingLevels({});
      addToast({ title: t("changes_saved"), message: t("permissions_updated") }, "success");
      loadUserTree(selectedUser.id);
    } catch (err) {
      addToast({ title: t("save_failed"), message: err.message || "Failed to save permissions" }, "error", 6000);
    } finally {
      setSaving(false);
    }
  };

  const selectedUserName = selectedUser?.name || "";
  const selectedUserCode = selectedUser?.code || "";
  const selectedUserType = selectedUser?.type || "";

  return (
    <div className="perm-page">
      <div className="perm-header">
        <div className="perm-header-left">
          <Shield size={20} />
          <div>
            <h1>{t("permissions_manager")}</h1>
            <p>{t("permissions_manager_desc")}</p>
          </div>
        </div>
        <div className="perm-header-actions">
          {dirty && (
            <button className="perm-btn perm-btn-outline" onClick={handleReset}>
              <RotateCcw size={13} /> {t("reset")}
            </button>
          )}
          <button
            className={`perm-btn perm-btn-primary ${!dirty || saving ? "disabled" : ""}`}
            onClick={handleSave}
            disabled={!dirty || saving || !selectedUser}
          >
            {saving ? t("saving") : <><Save size={13} /> {t("save_changes")}</>}
          </button>
        </div>
      </div>



      <div className="perm-layout">
        <div className="perm-search-box" style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", background: "#f4f5f7", borderRadius: 8, color: "#6b7280" }}>
          <Search size={14} />
          <input
            type="text"
            placeholder={t("search_users_placeholder") || "Search users by name or ID…"}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ flex: 1, border: "none", background: "none", fontSize: 13, fontFamily: "Outfit, sans-serif", outline: "none", color: "#1a1f5e" }}
          />
          {searchQuery && <button style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280" }} onClick={() => { setSearchQuery(""); setSearchResults([]); }}><X size={12} /></button>}
        </div>
        {searching && <div style={{ fontSize: 12, color: "#6b7280", padding: "8px 4px" }}>{t("searching") || "Searching…"}</div>}
        {searchResults.length > 0 && (
          <div style={{ maxHeight: 280, overflowY: "auto" }}>
            {searchResults.map((u) => (
              <button
                key={`${u.type}-${u.id}`}
                style={{ display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "8px 10px", border: "none", background: selectedUser?.id === u.id ? "#e8eaf6" : "none", cursor: "pointer", borderRadius: 8, textAlign: "left", fontFamily: "Outfit, sans-serif", color: "#1a1f5e", transition: "background 0.15s" }}
                onClick={() => handleSelectUser(u)}
              >
                <User size={14} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <strong style={{ display: "block", fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{u.name}</strong>
                  <span style={{ display: "block", fontSize: 11, color: "#6b7280" }}>{u.code} &middot; {u.type === "staff" ? t("staff") : t("student")}</span>
                </div>
                {selectedUser?.id === u.id && <Check size={13} />}
              </button>
            ))}
          </div>
        )}

        {selectedUser ? (
          <div className="perm-user-card">
            <div className="perm-user-avatar">{selectedUserName.charAt(0)}</div>
            <div className="perm-user-info">
              <strong>{selectedUserName}</strong>
              <span>{selectedUserCode} &middot; {selectedUserType === "staff" ? t("staff") : t("student")}</span>
            </div>
          </div>
        ) : (
          <div className="perm-empty-state">
            <User size={36} />
            <h3>{t("select_user")}</h3>
            <p>{t("select_user_desc")}</p>
          </div>
        )}

        {selectedUser && !loading && (
          <>
            <div className="perm-roles-section">
              <h3 className="perm-section-title">
                <Shield size={16} /> {t("role_assignments")}
              </h3>
              <p className="perm-section-desc">{t("role_assignments_desc")}</p>
              <div className="perm-role-grid">
                {allRoles.map((role) => {
                  const isAssigned = assignedRoleIds.includes(String(role.id));
                  return (
                    <button
                      key={role.id}
                      className={`perm-role-chip ${isAssigned ? "is-assigned" : ""} ${role.isSystemRole ? "is-system" : ""}`}
                      onClick={() => toggleRole(role.id)}
                    >
                      <span className="perm-role-chip-check">{isAssigned ? <CheckSquare size={14} /> : null}</span>
                      <span className="perm-role-chip-name">{role.name}</span>
                      {role.isSystemRole && <span className="perm-role-chip-badge">{t("system_role_badge")}</span>}
                    </button>
                  );
                })}
              </div>
            </div>

            <div className="perm-overrides-section">
              <div className="perm-overrides-header">
                <div>
                  <h3 className="perm-section-title">
                    <Shield size={16} /> {t("effective_permissions")}
                  </h3>
                  <p className="perm-section-desc">
                    {t("effective_permissions_desc")}
                    <span className="perm-override-hint"> <Undo2 size={11} /> {t("revert_hint")}</span>
                  </p>
                </div>
              </div>

              <div className="perm-body">
                <div className="perm-module-sidebar">
                  {userTree.map((mod) => {
                    const count = overrideCountByModule[mod.moduleId] || 0;
                    return (
                      <button
                        key={mod.moduleId}
                        className={`perm-module-btn ${activeModuleId === mod.moduleId ? "active" : ""}`}
                        onClick={() => setActiveModuleId(mod.moduleId)}
                      >
                        <span>{mod.moduleName}</span>
                        {count > 0 && <span className="perm-module-badge">{count}</span>}
                      </button>
                    );
                  })}
                </div>

                <div className="perm-resource-area">
                  <div className="perm-resource-list">
                    {!activeModule || activeModule.resources?.length === 0 ? (
                      <div className="perm-resource-empty">{t("no_resources_module")}</div>
                    ) : (
                      activeModule.resources.map((res) => {
                        const rid = String(res.resourceId);
                        const displayLevel = getDisplayLevel(rid);
                        const levels = resourceLevels[rid] || { effectiveLevel: 0, roleBasedLevel: 0 };
                        const overridden = resourceHasOverride(rid);

                        const roleLvlName = levelLabels[levels.roleBasedLevel] || t("none");
                        const isDowngraded = displayLevel < levels.roleBasedLevel;

                        return (
                          <div key={rid} className={`perm-res-card ${overridden ? "has-override" : ""} ${isDowngraded ? "is-denied" : ""}`}>
                            <div className="perm-res-card-header">
                              <ShieldCheck size={14} className="perm-res-card-icon" />
                              <span>{res.resourceName}</span>
                              {levels.roleBasedLevel > 0 && (
                                <span className="perm-role-badge">{roleLvlName}</span>
                              )}
                            </div>
                            <div className="perm-level-selector">
                              <div className="perm-level-pills-row">
                                {LEVELS.map((lvl) => {
                                  const isLevelZero = lvl.value === 0;
                                  const backendAction = LABEL_TO_ACTION[lvl.label];
                                  const isAvailable = isLevelZero || (backendAction && resourceActions[rid]?.has(backendAction));
                                  const active = isLevelZero ? displayLevel === 0 : displayLevel >= lvl.value;
                                  return (
                                    <button
                                      key={lvl.value}
                                      className={`perm-pill ${active ? "filled" : ""} ${displayLevel === lvl.value && isAvailable ? "current" : ""}${isLevelZero ? " none" : ""}${!isAvailable ? " disabled" : ""}`}
                                      onClick={() => isAvailable && setLevel(rid, lvl.value, levels.roleBasedLevel)}
                                      title={t("set_effective_level", { level: levelLabels[lvl.value] })}
                                      disabled={!isAvailable}
                                    >
                                      {levelLabels[lvl.value]}
                                    </button>
                                  );
                                })}
                              </div>
                            </div>

                            {overridden && (
                              <div className="perm-res-override-footer">
                                <span className="perm-ovr-tag">
                                  {displayLevel > levels.roleBasedLevel
                                    ? t("override_allow", { level: levelLabels[displayLevel] })
                                    : displayLevel < levels.roleBasedLevel
                                      ? displayLevel === 0
                                        ? t("override_no_permission")
                                        : t("override_deny", { level: levelLabels[displayLevel] })
                                      : ""}
                                </span>
                                <button
                                  className="perm-ovr-revert"
                                  onClick={() => handleRevert(rid)}
                                  title={t("remove_override")}
                                >
                                  <Undo2 size={12} /> {t("revert")}
                                </button>
                              </div>
                            )}
                          </div>
                        );
                      })
                    )}
                  </div>
                </div>
              </div>
            </div>
          </>
        )}

        {selectedUser && loading && (
          <div className="roles-loading" style={{ padding: 40 }}>
            <div className="roles-spinner" />
            <p>{t("loading_permissions")}</p>
          </div>
        )}
      </div>
    </div>
  );
}

export default PermissionsPage;
