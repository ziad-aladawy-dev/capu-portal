import { useState, useCallback, useMemo } from "react";
import {
  Shield, Save, RotateCcw, X, Check, Plus, Search,
  Undo2, ShieldCheck, Globe, Building2, MapPin, AlertTriangle,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import * as structureService from "../../../core/services/structureService";
import * as academicService from "../../../core/services/academicService";
import { useRoles, useUpdatePermissionAssignment } from "../../../core/query/usePermissionsData";
import { useDomain } from "../../../core/contexts/DomainContext";
import { usePermission } from "../../../core/auth/usePermission";
import { useToast } from "../../../core/components/Toast";
import StructureTree from "../../../core/components/StructureTree";
import NodeTypeBadge from "../../../core/components/NodeTypeBadge";
import {
  ACTION_LEVELS as LEVELS, ACTION_NAME_TO_LEVEL as ACTION_VALUES,
  LEVEL_TO_ACTION, LABEL_TO_ACTION, PERMISSION_RESOURCES, getLevelLabels,
} from "../../../core/constants/permissionLevels";
import { findNode, isOutsideActiveScope } from "../utils/scopeUtils";
import "../styles/permissions.css";
import "../styles/roles.css";

const EMPTY = [];

function getInitialScope() {
  return {
    structuralScope: { structureNodeId: null },
    temporalScope: { alwaysActive: true, academicYearId: null, semesterId: null },
  };
}

// Normalise temporal scope so AlwaysActive=true never carries stale IDs
function normalizeTemporal(t) {
  if (!t) return { alwaysActive: true, academicYearId: null, semesterId: null };
  if (t.alwaysActive) {
    return { alwaysActive: true, academicYearId: null, semesterId: null };
  }
  return t;
}

function buildInitialState(tree, assignment) {
  const roleAssignments = assignment?.roleAssignments || [];
  const ids = [...new Set(roleAssignments.map((r) => String(r.roleId)))];

  const scopeMap = {};
  roleAssignments.forEach((r) => {
    const roleId = String(r.roleId);
    if (!scopeMap[roleId]) scopeMap[roleId] = [];
    scopeMap[roleId].push({
      structuralScope: r.structuralScope || { structureNodeId: null },
      temporalScope: r.temporalScope || { alwaysActive: true, academicYearId: null, semesterId: null },
    });
  });

  const snap = {};
  const resOv = {};
  for (const mod of tree) {
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

  return {
    ids,
    scopeMap,
    snap,
    resOv,
    requestStructuralScope: assignment?.structuralScope || { structureNodeId: null },
    requestTemporalScope: assignment?.temporalScope || { alwaysActive: true, academicYearId: null, semesterId: null },
  };
}

// Seeded once per mount from the queries' data — the parent remounts this
// component (via key) whenever the user or the fetched data changes, so no
// data-sync effects are needed.
function PermissionsEditor({ user, tree, assignment }) {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { can } = usePermission();
  const canEditPerms = can(PERMISSION_RESOURCES.PERMISSIONS, 3);
  const editDisabledTitle = canEditPerms ? undefined : t("requires_permission_level", {
    defaultValue: 'Requires "Edit" access on permissions',
    level: t("edit"),
  });
  const levelLabels = getLevelLabels(t);

  const [init] = useState(() => buildInitialState(tree, assignment));
  const initialRoleIds = init.ids;
  const originalSnapshot = init.snap;
  const resourceOverrides = init.resOv;

  const [assignedRoleIds, setAssignedRoleIds] = useState(init.ids);
  const [roleScopeMap, setRoleScopeMap] = useState(init.scopeMap);
  const [overrideScopeMap, setOverrideScopeMap] = useState({});
  const [pendingLevels, setPendingLevels] = useState({});
  const [requestStructuralScope, setRequestStructuralScope] = useState(init.requestStructuralScope);
  const [requestTemporalScope, setRequestTemporalScope] = useState(init.requestTemporalScope);
  const [dirty, setDirty] = useState(false);
  const [activeModuleId, setActiveModuleId] = useState(tree.length > 0 ? tree[0].moduleId : null);

  const [scopeModalOpen, setScopeModalOpen] = useState(false);
  const [scopeModalContext, setScopeModalContext] = useState(null); // { type: "role" | "override", id, entryIndex }
  const [scopeModalSelected, setScopeModalSelected] = useState(null);
  const [rolePickerSearch, setRolePickerSearch] = useState("");
  const [tempYearId, setTempYearId] = useState(init.requestTemporalScope.academicYearId || "");
  const [tempSemesterId, setTempSemesterId] = useState(init.requestTemporalScope.semesterId || "");

  const rolesQuery = useRoles({ pageSize: 100 });
  const allRoles = rolesQuery.data ?? EMPTY;

  const structureQuery = useQuery({
    queryKey: ["structure-tree"],
    queryFn: () => structureService.fetchStructureTree(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 5 * 60 * 1000,
  });
  const structureTree = structureQuery.data ?? EMPTY;

  const yearsQuery = useQuery({
    queryKey: ["academic-years"],
    queryFn: () => academicService.fetchAcademicYears(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 5 * 60 * 1000,
  });
  const academicYears = yearsQuery.data ?? EMPTY;

  const semestersQuery = useQuery({
    queryKey: ["semesters", tempYearId],
    queryFn: () => academicService.fetchSemesters(tempYearId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!tempYearId,
  });
  const semesters = semestersQuery.data ?? EMPTY;

  const saveMutation = useUpdatePermissionAssignment();
  const saving = saveMutation.isPending;
  const userTree = tree;

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
        map[String(res.resourceId)] = new Set((res.permissions || []).map((p) => p.action));
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

  const markDirty = () => setDirty(true);

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
    setAssignedRoleIds(init.ids);
    setRoleScopeMap(init.scopeMap);
    setOverrideScopeMap({});
    setPendingLevels({});
    setRequestStructuralScope(init.requestStructuralScope);
    setRequestTemporalScope(init.requestTemporalScope);
    setTempYearId(init.requestTemporalScope.academicYearId || "");
    setTempSemesterId(init.requestTemporalScope.semesterId || "");
    setDirty(false);
  };

  // ─── Scope modal handlers ───
  const openRoleScope = (roleId, entryIndex) => {
    const s = String(roleId);
    const scopes = roleScopeMap[s] || [getInitialScope()];
    if (entryIndex !== null && entryIndex >= 0 && entryIndex < scopes.length) {
      setScopeModalContext({ type: "role", id: s, entryIndex });
      setScopeModalSelected(scopes[entryIndex].structuralScope.structureNodeId);
    } else {
      setScopeModalContext({ type: "role", id: s, entryIndex: null });
      setScopeModalSelected(undefined);
    }
    setScopeModalOpen(true);
  };

  const openOverrideScope = (resId, entryIndex) => {
    const s = String(resId);
    const scopes = overrideScopeMap[s]
      ? (Array.isArray(overrideScopeMap[s]) ? overrideScopeMap[s] : [overrideScopeMap[s]])
      : [{ structuralScope: { ...requestStructuralScope }, temporalScope: { ...requestTemporalScope } }];
    if (entryIndex !== null && entryIndex >= 0 && entryIndex < scopes.length) {
      setScopeModalContext({ type: "override", id: s, entryIndex });
      setScopeModalSelected(scopes[entryIndex].structuralScope.structureNodeId);
    } else {
      setScopeModalContext({ type: "override", id: s, entryIndex: null });
      setScopeModalSelected(undefined);
    }
    setScopeModalOpen(true);
  };

  const closeScopeModal = () => {
    setScopeModalOpen(false);
    setScopeModalContext(null);
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
        const rest = { ...prev };
        delete rest[s];
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
      const rest = { ...prev };
      delete rest[s];
      return rest;
    });
    markDirty();
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

  // ─── Save ───
  const handleSave = () => {
    if (!user || saving) return;
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

    const rolesToAdd = assignedRoleIds
      .filter((id) => !initialRoleIds.includes(id))
      .flatMap((id) => {
        const scopes = roleScopeMap[id] || [getInitialScope()];
        return scopes.map((scope) => ({
          roleId: id,
          structuralScope: scope.structuralScope,
          temporalScope: scope.temporalScope,
        }));
      });

    const rolesToRemove = initialRoleIds
      .filter((id) => !assignedRoleIds.includes(id))
      .flatMap((id) => {
        const scopes = init.scopeMap[id] || [getInitialScope()];
        return scopes.map((scope) => ({
          roleId: id,
          structuralScope: scope.structuralScope,
          temporalScope: scope.temporalScope,
        }));
      });

    saveMutation.mutate({
      userId: user.id,
      rolesToAdd: rolesToAdd.map((r) => ({ ...r, temporalScope: normalizeTemporal(r.temporalScope) })),
      rolesToRemove: rolesToRemove.map((r) => ({ ...r, temporalScope: normalizeTemporal(r.temporalScope) })),
      permissionsToAdd: toAdd.map((p) => ({ ...p, temporalScope: normalizeTemporal(p.temporalScope) })),
      permissionsToRemove: toRemove,
      structuralScope: requestStructuralScope,
      temporalScope: normalizeTemporal(requestTemporalScope),
    }, {
      onSuccess: () => {
        addToast({ title: t("changes_saved"), message: t("permissions_updated") }, "success");
        // Query invalidation refetches tree + assignment; the parent remounts
        // this editor with the fresh server state.
      },
      onError: (err) => {
        addToast({ title: t("save_failed"), message: err.message || "Failed to save permissions" }, "error", 6000);
      },
    });
  };

  const getNodeName = useCallback((nodeId) => {
    if (!nodeId) return null;
    const node = findNode(structureTree, nodeId);
    return node ? (node.localizedName || node.name) : null;
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

  // Scenario B guardrail: list role assignments whose structural scope falls
  // outside the active navbar scope.
  const outOfScopeAssignments = useMemo(() => {
    if (!scopeNode?.id || structureTree.length === 0) return [];
    const out = [];
    for (const [roleId, scopes] of Object.entries(roleScopeMap)) {
      for (const s of scopes) {
        const nid = s.structuralScope?.structureNodeId;
        if (nid && isOutsideActiveScope(structureTree, scopeNode.id, nid)) {
          const role = allRoles.find((r) => String(r.id) === roleId);
          out.push({ roleName: role?.name || roleId, nodeName: getNodeName(nid) || nid });
        }
      }
    }
    return out;
  }, [roleScopeMap, scopeNode, structureTree, allRoles, getNodeName]);

  const scopeModalNodeOutside = scopeModalSelected
    && scopeNode?.id
    && isOutsideActiveScope(structureTree, scopeNode.id, scopeModalSelected);

  return (
    <>
      {/* ─── Action bar ─── */}
      <div className="perm-header-actions" style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
        {dirty && (
          <button className="perm-btn perm-btn-outline" onClick={handleReset} disabled={saving}>
            <RotateCcw size={13} /> {t("reset")}
          </button>
        )}
        <button
          className={`perm-btn perm-btn-primary ${!dirty || saving ? "disabled" : ""}`}
          onClick={handleSave}
          disabled={!canEditPerms || !dirty || saving}
          title={editDisabledTitle}
        >
          {saving ? t("saving") : <><Save size={13} /> {t("save_changes")}</>}
        </button>
      </div>

      {outOfScopeAssignments.length > 0 && (
        <div className="perm-banner perm-banner-warning" role="alert" style={{
          display: "flex", alignItems: "flex-start", gap: 8,
          background: "#fef3c7", border: "1px solid #fde68a", borderRadius: 8,
          padding: "10px 12px", fontSize: 12, color: "#92400e",
        }}>
          <AlertTriangle size={15} style={{ flexShrink: 0, marginTop: 1 }} />
          <span>
            {t("out_of_scope_warning", {
              defaultValue: `Some role assignments are scoped outside your active scope (${scopeNode?.name}):`,
              scope: scopeNode?.name,
            })}{" "}
            {outOfScopeAssignments.map((o) => `${o.roleName} → ${o.nodeName}`).join(", ")}
          </span>
        </div>
      )}

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
                      onClick={() => canEditPerms && removeAllRoleScopes(roleId)}
                      disabled={!canEditPerms}
                      title={editDisabledTitle || t("remove_role") || "Remove role"}
                    >
                      <X size={12} />
                    </button>
                  </div>
                  <div className="perm-assigned-scopes">
                    {scopes.map((scope, idx) => {
                      const nodeId = scope.structuralScope?.structureNodeId;
                      const nodeName = nodeId ? getNodeName(nodeId) : null;
                      const outside = nodeId && scopeNode?.id
                        && isOutsideActiveScope(structureTree, scopeNode.id, nodeId);
                      return (
                        <span
                          key={idx}
                          className={`perm-assigned-scope ${nodeId ? "is-scoped" : "is-global"}`}
                          onClick={() => openRoleScope(roleId, idx)}
                          title={outside
                            ? t("scope_outside_active", { defaultValue: "Outside your active scope" })
                            : t("assignment_scope")}
                          style={outside ? { borderColor: "#f59e0b", background: "#fef3c7", color: "#92400e" } : undefined}
                        >
                          <span className="perm-assigned-scope-icon">
                            {outside ? <AlertTriangle size={10} /> : nodeId ? <Building2 size={10} /> : <Globe size={10} />}
                          </span>
                          <span className="perm-assigned-scope-label">
                            {nodeName || (nodeId ? t("scope_structural") : t("scope_global"))}
                          </span>
                          {canEditPerms && (
                            <X
                              size={8}
                              className="perm-assigned-scope-remove"
                              onClick={(e) => { e.stopPropagation(); removeRoleScope(roleId, idx); }}
                            />
                          )}
                        </span>
                      );
                    })}
                    <button
                      className="perm-assigned-scope-add"
                      onClick={() => canEditPerms && openRoleScope(roleId, null)}
                      disabled={!canEditPerms}
                      title={editDisabledTitle || t("add_scope") || "Add scope"}
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
                placeholder={t("search_roles") || "Search roles…"}
                value={rolePickerSearch}
                onChange={(e) => setRolePickerSearch(e.target.value)}
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
                        onClick={() => canEditPerms && handleAddRole(role.id)}
                        disabled={!canEditPerms}
                        title={editDisabledTitle}
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
                            {canEditPerms && (
                              <span
                                className="perm-res-scope-add-btn"
                                onClick={() => openOverrideScope(rid, null)}
                                title={t("add_scope") || "Add scope"}
                              >
                                <Plus size={10} />
                              </span>
                            )}
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
                            const disabled = !canEditPerms || !isAvailable;
                            return (
                              <button
                                key={lvl.value}
                                className={`perm-pill ${active ? "filled" : ""} ${displayLevel === lvl.value && isAvailable ? "current" : ""}${isLevelZero ? " none" : ""}${disabled ? " disabled" : ""}`}
                                onClick={() => !disabled && setLevel(rid, lvl.value, levels.roleBasedLevel)}
                                title={editDisabledTitle || t("set_effective_level", { level: levelLabels[lvl.value] })}
                                disabled={disabled}
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
                            onClick={() => canEditPerms && handleRevert(rid)}
                            disabled={!canEditPerms}
                            title={editDisabledTitle || t("remove_override")}
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
              {scopeModalSelected && (() => {
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

              {scopeModalNodeOutside && (
                <div role="alert" style={{
                  display: "flex", alignItems: "flex-start", gap: 8,
                  background: "#fef3c7", border: "1px solid #fde68a", borderRadius: 8,
                  padding: "8px 10px", fontSize: 12, color: "#92400e", marginBottom: 10,
                }}>
                  <AlertTriangle size={14} style={{ flexShrink: 0, marginTop: 1 }} />
                  <span>
                    {t("scope_outside_active_warning", {
                      defaultValue: `This node is outside your active scope (${scopeNode?.name}). The assignment will not take effect within your current scope.`,
                      scope: scopeNode?.name,
                    })}
                  </span>
                </div>
              )}

              <button
                className={`perm-scope-global-btn ${scopeModalSelected === null ? "is-active" : ""}`}
                onClick={() => setScopeModalSelected(null)}
              >
                <Globe size={16} />
                <div className="perm-scope-global-info">
                  <strong>{t("global_scope")}</strong>
                  <span>{t("scope_applies_everywhere") || "Applies to all structural contexts"}</span>
                </div>
                {scopeModalSelected === null && <Check size={16} className="perm-scope-global-check" />}
              </button>

              <div className="perm-scope-divider">
                <span className="perm-scope-divider-line" />
                <span className="perm-scope-divider-label">{t("or") || "or"}</span>
                <span className="perm-scope-divider-line" />
              </div>

              {structureTree.length > 0 ? (
                <div className="perm-scope-tree-wrapper">
                  <StructureTree
                    tree={structureTree}
                    selectedId={scopeModalSelected}
                    onSelect={(node) => setScopeModalSelected(node ? node.id : null)}
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
    </>
  );
}

export default PermissionsEditor;
