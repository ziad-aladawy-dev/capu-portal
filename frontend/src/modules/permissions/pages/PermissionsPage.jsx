import { useState, useCallback, useEffect, useRef } from "react";
import {
  Shield, Search, X, Save, RotateCcw, Plus, User, AlertTriangle,
  Building2, CalendarRange, BookOpen, Check, CheckCircle, Globe,
} from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import * as staffService from "../../../core/services/staffService";
import * as studentService from "../../../core/services/studentService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import "../styles/permissions.css";

const ACTION_LEVELS = [
  { value: 0, label: "None" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];

const OVERRIDE_TYPES = [
  { value: 1, label: "Allow" },
  { value: 2, label: "Deny" },
];

function PermissionsPage() {
  const { scopedUser, isScoped, scopeToUser, clearScope } = useUserScope();
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const debounceRef = useRef(null);

  const [allRoles, setAllRoles] = useState([]);
  const [assignedRoleIds, setAssignedRoleIds] = useState([]);
  const [overrides, setOverrides] = useState([]);

  const [newSvcName, setNewSvcName] = useState("");
  const [newLevel, setNewLevel] = useState(5);
  const [newType, setNewType] = useState(1);

  const [scopeNodeId, setScopeNodeId] = useState(null);
  const [alwaysActive, setAlwaysActive] = useState(true);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

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

  const loadAssignment = useCallback(async (userId) => {
    setLoading(true);
    setError(null);
    try {
      const assignment = await permissionService.fetchPermissionAssignment({ userId });
      setAssignedRoleIds(assignment?.roleIds || []);
      setOverrides(assignment?.permissionOverrides || []);
      setScopeNodeId(assignment?.structuralScope?.structureNodeId || null);
      setAlwaysActive(assignment?.temporalScope?.alwaysActive ?? true);
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
      setOverrides([]);
    }
  }, [isScoped]);

  const handleSelectUser = (user) => {
    scopeToUser(user);
    setSelectedUser(user);
    setSearchQuery("");
    setSearchResults([]);
    loadAssignment(user.id);
  };

  const toggleRole = (roleId) => {
    setAssignedRoleIds((prev) =>
      prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId]
    );
    setDirty(true);
    setSaved(false);
  };

  const handleAddOverride = () => {
    if (!newSvcName.trim()) return;
    setOverrides((prev) => [
      ...prev,
      { serviceId: `svc-${Date.now()}`, resource: newSvcName.trim(), level: newLevel, type: newType },
    ]);
    setNewSvcName("");
    setNewLevel(5);
    setNewType(1);
    setDirty(true);
    setSaved(false);
  };

  const handleRemoveOverride = (index) => {
    setOverrides((prev) => prev.filter((_, i) => i !== index));
    setDirty(true);
    setSaved(false);
  };

  const handleReset = () => {
    if (selectedUser) loadAssignment(selectedUser.id);
  };

  const handleSave = async () => {
    if (!selectedUser) return;
    setSaving(true);
    setError(null);
    try {
      const currentAssignment = await permissionService.fetchPermissionAssignment({ userId: selectedUser.id });
      const currentRoleIds = currentAssignment?.roleIds || [];
      const currentOverrides = currentAssignment?.permissionOverrides || [];

      const rolesToAdd = assignedRoleIds.filter((id) => !currentRoleIds.includes(id));
      const rolesToRemove = currentRoleIds.filter((id) => !assignedRoleIds.includes(id));

      const overrideKey = (o) => `${o.serviceId}|${o.resource}`;
      const currentKeys = new Set(currentOverrides.map(overrideKey));
      const newKeys = new Set(overrides.map(overrideKey));

      const permissionsToAdd = overrides.filter((o) => !currentKeys.has(overrideKey(o)));
      const permissionsToRemove = currentOverrides.filter((o) => !newKeys.has(overrideKey(o)));

      await permissionService.updatePermissionAssignment({
        userId: selectedUser.id,
        rolesToAdd,
        rolesToRemove,
        permissionsToAdd,
        permissionsToRemove,
        structuralScope: { structureNodeId: scopeNodeId },
        temporalScope: { academicYearId: null, semesterId: null, alwaysActive },
      });

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
              <RotateCcw size={13} />
              Reset
            </button>
          )}
          <button
            className={`perm-btn perm-btn-primary ${!dirty || saving ? "disabled" : ""}`}
            onClick={handleSave}
            disabled={!dirty || saving || !selectedUser}
          >
            {saving ? "Saving…" : saved ? "Saved!" : <><Save size={13} /> Save Changes</>}
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

      {saved && <div className="perm-success-banner"><CheckCircle size={15} /><span>Permissions saved successfully</span></div>}

      <div className="perm-layout">
        <div className="perm-left-panel">
          <div className="perm-user-search">
            <div className="perm-search-box">
              <Search size={14} />
              <input
                type="text"
                placeholder="Search users by name or ID…"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
              {searchQuery && <button className="perm-clear-search" onClick={() => { setSearchQuery(""); setSearchResults([]); }}><X size={12} /></button>}
            </div>
            {searching && <div className="perm-search-status">Searching…</div>}
            {searchResults.length > 0 && (
              <div className="perm-search-results">
                {searchResults.map((u) => (
                  <button
                    key={`${u.type}-${u.id}`}
                    className={`perm-user-option ${selectedUser?.id === u.id ? "is-selected" : ""}`}
                    onClick={() => handleSelectUser(u)}
                  >
                    <User size={14} />
                    <div className="perm-user-option-info">
                      <strong>{u.name}</strong>
                      <span>{u.code} · {u.type === "staff" ? "Staff" : "Student"}</span>
                    </div>
                    {selectedUser?.id === u.id && <Check size={13} />}
                  </button>
                ))}
              </div>
            )}
          </div>

          {selectedUser && (
            <div className="perm-user-card">
              <div className="perm-user-avatar">{selectedUserName.charAt(0)}</div>
              <div className="perm-user-info">
                <strong>{selectedUserName}</strong>
                <span>{selectedUserCode} · {selectedUserType === "staff" ? "Staff" : "Student"}</span>
              </div>
            </div>
          )}
        </div>

        <div className="perm-right-panel">
          {!selectedUser ? (
            <div className="perm-empty-state">
              <User size={40} />
              <h3>Select a User</h3>
              <p>Search for a user above to manage their roles and permission overrides.</p>
            </div>
          ) : loading ? (
            <div className="perm-loading">Loading permissions…</div>
          ) : (
            <>
              <div className="perm-section">
                <h3 className="perm-section-title">
                  <Shield size={16} />
                  Role Assignments
                </h3>
                <p className="perm-section-desc">Select the roles assigned to this user. A user can have multiple roles.</p>
                <div className="perm-role-grid">
                  {allRoles.map((role) => {
                    const isAssigned = assignedRoleIds.includes(role.id);
                    return (
                      <button
                        key={role.id}
                        className={`perm-role-chip ${isAssigned ? "is-assigned" : ""} ${role.isSystemRole ? "is-system" : ""}`}
                        onClick={() => toggleRole(role.id)}
                      >
                        <span className="perm-role-chip-check">{isAssigned ? <Check size={11} /> : null}</span>
                        <span className="perm-role-chip-name">{role.name}</span>
                        {role.isSystemRole && <span className="perm-role-chip-badge">system</span>}
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="perm-section">
                <h3 className="perm-section-title">
                  <Globe size={16} />
                  Scope
                </h3>
                <p className="perm-section-desc">Define the scope context for this assignment.</p>
                <div className="perm-scope-row">
                  <div className="perm-scope-field">
                    <Building2 size={13} />
                    <input
                      type="text"
                      placeholder="Structure Node ID (optional)"
                      value={scopeNodeId || ""}
                      onChange={(e) => { setScopeNodeId(e.target.value || null); setDirty(true); setSaved(false); }}
                    />
                  </div>
                  <label className="perm-scope-check">
                    <input
                      type="checkbox"
                      checked={alwaysActive}
                      onChange={(e) => { setAlwaysActive(e.target.checked); setDirty(true); setSaved(false); }}
                    />
                    <span>Always Active (no temporal constraints)</span>
                  </label>
                </div>
              </div>

              <div className="perm-section">
                <h3 className="perm-section-title">
                  <AlertTriangle size={16} />
                  Permission Overrides
                </h3>
                <p className="perm-section-desc">Override specific permissions for this user regardless of their roles.</p>

                <div className="perm-override-form">
                  <div className="perm-override-form-row">
                    <input
                      type="text"
                      className="perm-override-input"
                      placeholder="Resource name (e.g. users.management)"
                      value={newSvcName}
                      onChange={(e) => setNewSvcName(e.target.value)}
                    />
                    <select className="perm-override-select" value={newLevel} onChange={(e) => setNewLevel(Number(e.target.value))}>
                      {ACTION_LEVELS.map((l) => <option key={l.value} value={l.value}>{l.label} ({l.value})</option>)}
                    </select>
                    <select className="perm-override-select small" value={newType} onChange={(e) => setNewType(Number(e.target.value))}>
                      {OVERRIDE_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </select>
                    <button className="perm-btn perm-btn-primary" onClick={handleAddOverride} disabled={!newSvcName.trim()}>
                      <Plus size={13} />
                      Add
                    </button>
                  </div>
                </div>

                {overrides.length === 0 ? (
                  <p className="perm-empty-override">No permission overrides configured.</p>
                ) : (
                  <div className="perm-override-list">
                    {overrides.map((ov, i) => (
                      <div key={i} className="perm-override-item">
                        <div className="perm-override-info">
                          <strong>{ov.resource}</strong>
                          <span className={`perm-override-level level-${ov.level}`}>{ACTION_LEVELS[ov.level]?.label} ({ov.level})</span>
                          <span className={`perm-override-type ${ov.type === 2 ? "is-deny" : "is-allow"}`}>{ov.type === 2 ? "Deny" : "Allow"}</span>
                        </div>
                        <button className="perm-btn-icon" onClick={() => handleRemoveOverride(i)}><X size={12} /></button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default PermissionsPage;
