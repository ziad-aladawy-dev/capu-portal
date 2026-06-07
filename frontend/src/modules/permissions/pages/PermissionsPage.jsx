import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import {
  Shield, Save, RotateCcw, User, Search, X, Check, Plus,
  Undo2, ShieldCheck, Globe, Building2, MapPin,
} from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import * as authorizationService from "../../../core/services/authorizationService";
import * as staffService from "../../../core/services/staffService";
import * as studentService from "../../../core/services/studentService";
import * as structureService from "../../../core/services/structureService";
import * as academicService from "../../../core/services/academicService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import { useToast } from "../../../core/components/Toast";
import StructureTree from "../../../core/components/StructureTree";
import NodeTypeBadge from "../../../core/components/NodeTypeBadge";
import "../styles/permissions.css";
import "../styles/roles.css";
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

function getInitialScope() {
  return { structuralScope: { structureNodeId: null }, temporalScope: { alwaysActive: true, academicYearId: null, semesterId: null } };
}

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

  // Per-role scope: { [roleId]: [{ structuralScope, temporalScope }, ...] }
  const [roleScopeMap, setRoleScopeMap] = useState({});
  // Per-override scope: { [resourceId]: { structuralScope, temporalScope } }
  const [overrideScopeMap, setOverrideScopeMap] = useState({});

  // Request-level fallback scopes
  const [requestStructuralScope, setRequestStructuralScope] = useState({ structureNodeId: null });
  const [requestTemporalScope, setRequestTemporalScope] = useState({ alwaysActive: true, academicYearId: null, semesterId: null });

  const [userTree, setUserTree] = useState([]);
  const [activeModuleId, setActiveModuleId] = useState(null);

  const [originalSnapshot, setOriginalSnapshot] = useState(null);
  const [pendingLevels, setPendingLevels] = useState({});
  const [resourceOverrides, setResourceOverrides] = useState({});

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  // Scope modal state
  const [structureTree, setStructureTree] = useState([]);
  const [academicYears, setAcademicYears] = useState([]);
  const [semesters, setSemesters] = useState([]);
  const [scopeModalOpen, setScopeModalOpen] = useState(false);
  const [scopeModalContext, setScopeModalContext] = useState(null); // { type: "role" | "override", id: string, entryIndex: number | null }
  const [scopeModalSelected, setScopeModalSelected] = useState(null);
  const [structureLoading, setStructureLoading] = useState(false);

  // Role picker search
  const [rolePickerSearch, setRolePickerSearch] = useState("");

  // Temporal picker sub-state
  const [tempYearId, setTempYearId] = useState("");
  const [tempSemesterId, setTempSemesterId] = useState("");

  useEffect(() => {
    permissionService.fetchAllRoles({ pageSize: 100 }).then((res) => {
      setAllRoles(res?.items || []);
    });
    structureService.fetchStructureTree().then(setStructureTree).catch(() => {});
    academicService.fetchAcademicYears().then((years) => {
      setAcademicYears(Array.isArray(years) ? years : []);
    }).catch(() => {});
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

      // Parse per-role assignments — group multiple scopes per role
      const roleAssignments = assignment?.roleAssignments || [];
      const ids = [...new Set(roleAssignments.map((r) => String(r.roleId)))];
      setAssignedRoleIds(ids);
      setInitialRoleIds([...ids]);

      const scopeMap = {};
      roleAssignments.forEach((r) => {
        const roleId = String(r.roleId);
        if (!scopeMap[roleId]) scopeMap[roleId] = [];
        scopeMap[roleId].push({
          structuralScope: r.structuralScope || { structureNodeId: null },
          temporalScope: r.temporalScope || { alwaysActive: true, academicYearId: null, semesterId: null },
        });
      });
      setRoleScopeMap(scopeMap);

      // Parse request-level defaults
      setRequestStructuralScope(assignment?.structuralScope || { structureNodeId: null });
      setRequestTemporalScope(assignment?.temporalScope || { alwaysActive: true, academicYearId: null, semesterId: null });

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
      setOverrideScopeMap({});
    } catch (err) {
      addToast({ title: t("load_failed"), message: err.message || "Failed to load permissions" }, "error");
    } finally {
      setLoading(false);
    }
  }, [addToast, t]);

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
      setRoleScopeMap({});
      setOverrideScopeMap({});
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

  const handleAddRole = (roleId) => {
    const s = String(roleId);
    if (assignedRoleIds.includes(s)) return;
    setAssignedRoleIds((prev) => [...prev, s]);
    setRoleScopeMap((prev) => ({
      ...prev,
      [s]: [{
        structuralScope: { ...requestStructuralScope },
        temporalScope: { ...requestTemporalScope },
      }],
    }));
    setRolePickerSearch("");
    markDirty();
  };

  const handleReset = () => {
    if (selectedUser) loadUserTree(selectedUser.id);
  };

  // ─── Scope modal handlers ───
  const openRoleScope = (roleId, entryIndex) => {
    const s = String(roleId);
    const scopes = roleScopeMap[s] || [getInitialScope()];
    if (entryIndex !== null && entryIndex >= 0 && entryIndex < scopes.length) {
      const current = scopes[entryIndex];
      setScopeModalContext({ type: "role", id: s, entryIndex });
      setScopeModalSelected(current.structuralScope.structureNodeId);
    } else {
      setScopeModalContext({ type: "role", id: s, entryIndex: null });
      setScopeModalSelected(undefined);
    }
    setScopeModalOpen(true);
  };

  const openOverrideScope = (resId, entryIndex) => {
    const s = String(resId);
    const scopes = overrideScopeMap[s] ? [overrideScopeMap[s]] : [{ structuralScope: { ...requestStructuralScope }, temporalScope: { ...requestTemporalScope } }];
    if (entryIndex !== null && entryIndex >= 0 && entryIndex < scopes.length) {
      const current = scopes[entryIndex];
      setScopeModalContext({ type: "override", id: s, entryIndex });
      setScopeModalSelected(current.structuralScope.structureNodeId);
    } else {
      setScopeModalContext({ type: "override", id: s, entryIndex: null });
      setScopeModalSelected(undefined);
    }
    setScopeModalOpen(true);
  };

  const handleScopeSelect = (node) => {
    setScopeModalSelected(node ? node.id : null);
  };

  const handleScopeClear = () => {
    setScopeModalSelected(null);
  };

  const handleScopeApply = () => {
    if (!scopeModalContext) return;
    const { type, id, entryIndex } = scopeModalContext;
    const newScope = {
      structuralScope: { structureNodeId: scopeModalSelected },
      temporalScope: requestTemporalScope,
    };
    if (type === "role") {
      setRoleScopeMap((prev) => {
        const arr = prev[id] || [];
        if (entryIndex !== null && entryIndex >= 0 && entryIndex < arr.length) {
          const updated = [...arr];
          updated[entryIndex] = newScope;
          return { ...prev, [id]: updated };
        }
        return { ...prev, [id]: [...arr, newScope] };
      });
    } else {
      setOverrideScopeMap((prev) => {
        const arr = Array.isArray(prev[id]) ? prev[id] : (prev[id] ? [prev[id]] : []);
        if (entryIndex !== null && entryIndex >= 0 && entryIndex < arr.length) {
          const updated = [...arr];
          updated[entryIndex] = newScope;
          return { ...prev, [id]: updated };
        }
        return { ...prev, [id]: [...arr, newScope] };
      });
    }
    markDirty();
    closeScopeModal();
  };

  const removeRoleScope = (roleId, entryIndex) => {
    const s = String(roleId);
    setRoleScopeMap((prev) => {
      const arr = prev[s] || [];
      if (entryIndex < 0 || entryIndex >= arr.length) return prev;
      const updated = arr.filter((_, i) => i !== entryIndex);
      if (updated.length === 0) {
        setAssignedRoleIds((prevIds) => prevIds.filter((id) => id !== s));
        const { [s]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [s]: updated };
    });
    markDirty();
  };

  const removeAllRoleScopes = (roleId) => {
    const s = String(roleId);
    setAssignedRoleIds((prev) => prev.filter((id) => id !== s));
    setRoleScopeMap((prev) => {
      const { [s]: _, ...rest } = prev;
      return rest;
    });
    markDirty();
  };

  const removeOverrideScope = (resId, entryIndex) => {
    const s = String(resId);
    setOverrideScopeMap((prev) => {
      const arr = Array.isArray(prev[s]) ? prev[s] : (prev[s] ? [prev[s]] : []);
      if (entryIndex < 0 || entryIndex >= arr.length) return prev;
      const updated = arr.filter((_, i) => i !== entryIndex);
      if (updated.length === 0) {
        const { [s]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [s]: updated };
    });
    markDirty();
  };

  const closeScopeModal = () => {
    setScopeModalOpen(false);
    setScopeModalContext(null);
    setScopeModalSelected(null);
  };

  // ─── Temporal scope handlers ───
  const setAlwaysActive = () => {
    setRequestTemporalScope({ alwaysActive: true, academicYearId: null, semesterId: null });
    markDirty();
  };

  const setLimitedTemporal = () => {
    setRequestTemporalScope({ alwaysActive: false, academicYearId: null, semesterId: null });
    setTempYearId("");
    setTempSemesterId("");
    markDirty();
  };

  const handleYearChange = (yearId) => {
    setTempYearId(yearId);
    setTempSemesterId("");
    setRequestTemporalScope({
      alwaysActive: false,
      academicYearId: yearId || null,
      semesterId: null,
    });
    if (yearId) {
      academicService.fetchSemesters(yearId).then((sems) => {
        setSemesters(Array.isArray(sems) ? sems : []);
      }).catch(() => setSemesters([]));
    } else {
      setSemesters([]);
    }
    markDirty();
  };

  const handleSemesterChange = (semId) => {
    setTempSemesterId(semId);
    setRequestTemporalScope((prev) => ({
      alwaysActive: false,
      academicYearId: prev.academicYearId,
      semesterId: semId || null,
    }));
    markDirty();
  };

  // Normalise temporal scope so AlwaysActive=true never carries stale IDs
  const normalizeTemporal = (t) => {
    if (!t) return { alwaysActive: true, academicYearId: null, semesterId: null };
    if (t.alwaysActive) {
      return { alwaysActive: true, academicYearId: null, semesterId: null };
    }
    return t;
  };

  // ─── Save ───
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
          if (action) {
            const raw = overrideScopeMap[resId];
            const scopes = Array.isArray(raw) ? raw : (raw ? [raw] : []);
            (scopes.length > 0 ? scopes : [null]).forEach((ovScope) => {
              toAdd.push({
                resourceId: resId,
                actions: [action],
                type: 1,
                structuralScope: ovScope?.structuralScope || requestStructuralScope,
                temporalScope: ovScope?.temporalScope || requestTemporalScope,
              });
            });
          }
        } else if (desired < roleLvl) {
          const action = LEVEL_TO_ACTION[desired + 1];
          if (action) {
            const raw = overrideScopeMap[resId];
            const scopes = Array.isArray(raw) ? raw : (raw ? [raw] : []);
            (scopes.length > 0 ? scopes : [null]).forEach((ovScope) => {
              toAdd.push({
                resourceId: resId,
                actions: [action],
                type: 2,
                structuralScope: ovScope?.structuralScope || requestStructuralScope,
                temporalScope: ovScope?.temporalScope || requestTemporalScope,
              });
            });
          }
        }
      }

      const assignedStr = assignedRoleIds;
      const initialStr = initialRoleIds;

      const rolesToAdd = assignedStr
        .filter((id) => !initialStr.includes(id))
        .flatMap((id) => {
          const scopes = roleScopeMap[id] || [getInitialScope()];
          return scopes.map((scope) => ({
            roleId: id,
            structuralScope: scope.structuralScope,
            temporalScope: scope.temporalScope,
          }));
        });

      const rolesToRemove = initialStr
        .filter((id) => !assignedStr.includes(id))
        .flatMap((id) => {
          const scopes = roleScopeMap[id] || [getInitialScope()];
          return scopes.map((scope) => ({
            roleId: id,
            structuralScope: scope.structuralScope,
            temporalScope: scope.temporalScope,
          }));
        });

      await permissionService.updatePermissionAssignment({
        userId: selectedUser.id,
        rolesToAdd: rolesToAdd.map((r) => ({
          ...r,
          temporalScope: normalizeTemporal(r.temporalScope),
        })),
        rolesToRemove: rolesToRemove.map((r) => ({
          ...r,
          temporalScope: normalizeTemporal(r.temporalScope),
        })),
        permissionsToAdd: toAdd.map((p) => ({
          ...p,
          temporalScope: normalizeTemporal(p.temporalScope),
        })),
        permissionsToRemove: toRemove,
        structuralScope: requestStructuralScope,
        temporalScope: normalizeTemporal(requestTemporalScope),
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

  // Resolve actual node name from the structure tree
  const getNodeName = useCallback((nodeId) => {
    if (!nodeId) return null;
    const find = (nodes) => {
      for (const n of nodes) {
        if (n.id === nodeId) return n.localizedName || n.name;
        if (n.children) { const f = find(n.children); if (f) return f; }
      }
      return null;
    };
    return find(structureTree);
  }, [structureTree]);

  // Unique structural scope nodes from all role assignments
  const roleScopeNodes = useMemo(() => {
    const seen = new Set();
    const nodes = [];
    for (const scopes of Object.values(roleScopeMap)) {
      for (const s of scopes) {
        const nid = s.structuralScope?.structureNodeId;
        const key = nid ?? "__global__";
        if (!seen.has(key)) {
          seen.add(key);
          nodes.push({ nodeId: nid, nodeName: nid ? getNodeName(nid) : null });
        }
      }
    }
    return nodes;
  }, [roleScopeMap, getNodeName]);

  // Resolve scope display for a role (kept for backward compat)
  const getRoleScopeDisplay = (roleId) => {
    const s = String(roleId);
    const scopes = roleScopeMap[s];
    if (!scopes || scopes.length === 0) return null;
    return scopes[0].structuralScope?.structureNodeId || null;
  };

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
            placeholder={t("search_users_placeholder") || "Search users by name or ID\u2026"}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ flex: 1, border: "none", background: "none", fontSize: 13, fontFamily: "Outfit, sans-serif", outline: "none", color: "#1a1f5e" }}
          />
          {searchQuery && <button style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280" }} onClick={() => { setSearchQuery(""); setSearchResults([]); }}><X size={12} /></button>}
        </div>
        {searching && <div style={{ fontSize: 12, color: "#6b7280", padding: "8px 4px" }}>{t("searching") || "Searching\u2026"}</div>}
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
            {/* ─── Temporal Scope Section ─── */}
            <div className="perm-temporal-section">
              <h3 className="perm-section-title">
                <Globe size={16} /> {t("temporal_scope")}
              </h3>
              <p className="perm-section-desc">{t("temporal_hint")}</p>
              <div className="perm-temporal-row">
                <div className="perm-temporal-toggle">
                  <button
                    className={`perm-temporal-toggle-btn ${requestTemporalScope.alwaysActive ? "active" : ""}`}
                    onClick={setAlwaysActive}
                  >
                    {t("always_active")}
                  </button>
                  <button
                    className={`perm-temporal-toggle-btn ${!requestTemporalScope.alwaysActive ? "active" : ""}`}
                    onClick={setLimitedTemporal}
                  >
                    {t("limited_to")}
                  </button>
                </div>
                {!requestTemporalScope.alwaysActive && (
                  <div className="perm-temporal-picker">
                    <select
                      className="perm-temporal-select"
                      value={tempYearId}
                      onChange={(e) => handleYearChange(e.target.value)}
                    >
                      <option value="">{t("select_academic_year")}</option>
                      {academicYears.map((y) => (
                        <option key={y.id} value={y.id}>{y.name}</option>
                      ))}
                    </select>
                    {tempYearId && (
                      <select
                        className="perm-temporal-select"
                        value={tempSemesterId}
                        onChange={(e) => handleSemesterChange(e.target.value)}
                      >
                        <option value="">{t("select_semester")}</option>
                        {semesters.map((s) => (
                          <option key={s.id} value={s.id}>{s.name}</option>
                        ))}
                      </select>
                    )}
                  </div>
                )}
              </div>
            </div>

            {/* ─── Role Assignments Section ─── */}
            <div className="perm-roles-section">
              <div className="perm-roles-header">
                <div>
                  <h3 className="perm-section-title">
                    <Shield size={16} /> {t("role_assignments")}
                  </h3>
                  <p className="perm-section-desc">{t("role_assignments_desc")}</p>
                </div>
              </div>

              <div className="perm-assigned-roles">
                {assignedRoleIds.length === 0 ? (
                  <div className="perm-assigned-empty">
                    <Shield size={24} className="perm-assigned-empty-icon" />
                    <span>{t("no_roles_assigned") || "No roles assigned yet"}</span>
                    <span className="perm-assigned-empty-hint">{t("add_role_hint") || 'Click "+ Add role" below to assign one'}</span>
                  </div>
                ) : (
                  assignedRoleIds.map((roleId) => {
                    const role = allRoles.find((r) => String(r.id) === roleId);
                    if (!role) return null;
                    const scopes = roleScopeMap[roleId] || [];
                    return (
                      <div
                        key={roleId}
                        className={`perm-assigned-role-card ${role.isSystemRole ? "is-system" : ""}`}
                      >
                        <div className="perm-assigned-role-head">
                          <div className="perm-assigned-role-info">
                            <span className="perm-assigned-role-name">{role.name}</span>
                            {role.isSystemRole && <span className="perm-assigned-role-badge">{t("system_role_badge")}</span>}
                          </div>
                          <button
                            className="perm-assigned-role-remove"
                            onClick={() => removeAllRoleScopes(roleId)}
                            title={t("remove_role") || "Remove role"}
                          >
                            <X size={12} />
                          </button>
                        </div>
                        <div className="perm-assigned-scopes">
                          {scopes.map((scope, idx) => {
                            const nodeId = scope.structuralScope?.structureNodeId;
                            const nodeName = nodeId ? getNodeName(nodeId) : null;
                            return (
                              <span
                                key={idx}
                                className={`perm-assigned-scope ${nodeId ? "is-scoped" : "is-global"}`}
                                onClick={() => openRoleScope(roleId, idx)}
                                title={t("assignment_scope")}
                              >
                                <span className="perm-assigned-scope-icon">
                                  {nodeId ? <Building2 size={10} /> : <Globe size={10} />}
                                </span>
                                <span className="perm-assigned-scope-label">
                                  {nodeName || (nodeId ? t("scope_structural") : t("scope_global"))}
                                </span>
                                <X
                                  size={8}
                                  className="perm-assigned-scope-remove"
                                  onClick={(e) => { e.stopPropagation(); removeRoleScope(roleId, idx); }}
                                />
                              </span>
                            );
                          })}
                          <button
                            className="perm-assigned-scope-add"
                            onClick={() => openRoleScope(roleId, null)}
                            title={t("add_scope") || "Add scope"}
                          >
                            <Plus size={10} />
                          </button>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>

              {/* ─── Inline role picker ─── */}
              <div className="perm-add-role-area">
                <div className="perm-picker">
                  <div className="perm-picker-search">
                    <Search size={14} className="perm-picker-search-icon" />
                    <input
                      placeholder={t("search_roles") || "Search roles\u2026"}
                      value={rolePickerSearch}
                      onChange={(e) => setRolePickerSearch(e.target.value)}
                      autoFocus
                    />
                    {rolePickerSearch && (
                      <button
                        className="perm-picker-close"
                        onClick={() => setRolePickerSearch("")}
                      >
                        <X size={14} />
                      </button>
                    )}
                  </div>
                  <div className="perm-picker-list">
                    {(() => {
                      const available = allRoles.filter((r) => !assignedRoleIds.includes(String(r.id)));
                      const filtered = rolePickerSearch
                        ? available.filter((r) => r.name.toLowerCase().includes(rolePickerSearch.toLowerCase()))
                        : available;
                      if (filtered.length === 0) {
                        return (
                          <div className="perm-picker-empty">
                            {t("no_roles_found") || "No roles found"}
                          </div>
                        );
                      }
                      return (
                        <div className="perm-picker-items">
                          {filtered.map((role) => (
                            <button
                              key={role.id}
                              className="perm-picker-item"
                              onClick={() => handleAddRole(role.id)}
                            >
                              <div className="perm-picker-item-left">
                                <Plus size={12} className="perm-picker-item-plus" />
                                <span className="perm-picker-item-name">{role.name}</span>
                              </div>
                              {role.isSystemRole && <span className="perm-picker-item-badge">{t("system_role_badge")}</span>}
                            </button>
                          ))}
                        </div>
                      );
                    })()}
                  </div>
                </div>
              </div>
            </div>

            {/* ─── Effective Permissions Section ─── */}
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
                        const ovScopes = overrideScopeMap[rid] ? (Array.isArray(overrideScopeMap[rid]) ? overrideScopeMap[rid] : [overrideScopeMap[rid]]) : [];
                        const roleLvlName = levelLabels[levels.roleBasedLevel] || t("none");
                        const isDowngraded = displayLevel < levels.roleBasedLevel;

                        return (
                          <div key={rid} className={`perm-res-card ${overridden ? "has-override" : ""} ${isDowngraded ? "is-denied" : ""}`}>
                            <div className="perm-res-card-header">
                              <ShieldCheck size={14} className="perm-res-card-icon" />
                              <span>{res.resourceName}</span>
                              {overridden && (
                                <>
                                  {ovScopes.map((s, idx) => {
                                    const nodeId = s.structuralScope?.structureNodeId;
                                    const nodeName = nodeId ? getNodeName(nodeId) : null;
                                    return (
                                      <span
                                        key={idx}
                                        className={`perm-res-scope-badge ${nodeId ? "is-scoped" : "is-global"}`}
                                        onClick={() => openOverrideScope(rid, idx)}
                                        title={t("override_scope_title")}
                                      >
                                        {nodeId
                                          ? <><Building2 size={10} /> {nodeName || t("scope_structural")}</>
                                          : <><Globe size={10} /> {t("override_scope_global")}</>}
                                      </span>
                                    );
                                  })}
                                  <span
                                    className="perm-res-scope-add-btn"
                                    onClick={() => openOverrideScope(rid, null)}
                                    title={t("add_scope") || "Add scope"}
                                  >
                                    <Plus size={10} />
                                  </span>
                                </>
                              )}
                              {levels.roleBasedLevel > 0 && (
                                <>
                                  {roleScopeNodes.map((n, i) => (
                                    <span
                                      key={i}
                                      className={`perm-res-scope-badge ${n.nodeId ? "is-scoped" : "is-global"}`}
                                      title={t("assignment_scope")}
                                    >
                                      {n.nodeId
                                        ? <><Building2 size={10} /> {n.nodeName || t("scope_structural")}</>
                                        : <><Globe size={10} /> {t("scope_global")}</>}
                                    </span>
                                  ))}
                                  <span className="perm-role-badge">{roleLvlName}</span>
                                </>
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

      {/* ─── Scope Selection Modal ─── */}
      {scopeModalOpen && (
            <div className="perm-scope-overlay" onClick={closeScopeModal}>
            <div className="perm-scope-modal" onClick={(e) => e.stopPropagation()}>
              <div className="perm-scope-modal-header">
                <div className="perm-scope-modal-header-left">
                  <MapPin size={16} className="perm-scope-modal-icon" />
                  <h3>{t("select_structural_scope")}</h3>
                </div>
                <button className="perm-scope-close-btn" onClick={closeScopeModal}><X size={16} /></button>
              </div>

            <div className="perm-scope-modal-body">
              {/* Selected node preview */}
              {scopeModalSelected && (() => {
                const findNode = (nodes, id) => {
                  for (const n of nodes) {
                    if (n.id === id) return n;
                    if (n.children) { const f = findNode(n.children, id); if (f) return f; }
                  }
                  return null;
                };
                const node = findNode(structureTree, scopeModalSelected);
                return node ? (
                  <div className="perm-scope-selected-preview">
                    <span className="perm-scope-selected-label">{t("structural_scope")}</span>
                    <div className="perm-scope-selected-node">
                      <NodeTypeBadge type={node.type} size="sm" showIcon showLabel />
                      <span className="perm-scope-selected-name">{node.localizedName || node.name}</span>
                    </div>
                  </div>
                ) : null;
              })()}

              {/* Global option */}
              <button
                className={`perm-scope-global-btn ${scopeModalSelected === null ? "is-active" : ""}`}
                onClick={handleScopeClear}
              >
                <Globe size={16} />
                <div className="perm-scope-global-info">
                  <strong>{t("global_scope")}</strong>
                  <span>{t("scope_applies_everywhere") || "Applies to all structural contexts"}</span>
                </div>
                {scopeModalSelected === null && <Check size={16} className="perm-scope-global-check" />}
              </button>

              {/* Divider */}
              <div className="perm-scope-divider">
                <span className="perm-scope-divider-line" />
                <span className="perm-scope-divider-label">{t("or") || "or"}</span>
                <span className="perm-scope-divider-line" />
              </div>

              {/* Structure tree */}
              {structureTree.length > 0 ? (
                <div className="perm-scope-tree-wrapper">
                  <StructureTree
                    tree={structureTree}
                    selectedId={scopeModalSelected}
                    onSelect={handleScopeSelect}
                    defaultExpandedDepth={2}
                  />
                </div>
              ) : (
                <div className="perm-scope-loading">
                  <div className="perm-scope-spinner" />
                  <p>{t("loading_structure")}</p>
                </div>
              )}
            </div>

            <div className="perm-scope-modal-footer">
              <button className="perm-scope-btn perm-scope-btn-outline" onClick={closeScopeModal}>{t("cancel")}</button>
              <button
                className={`perm-scope-btn ${scopeModalSelected !== undefined ? "perm-scope-btn-primary" : "perm-scope-btn-primary disabled"}`}
                onClick={handleScopeApply}
              >
                {t("apply") || "Apply"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default PermissionsPage;
