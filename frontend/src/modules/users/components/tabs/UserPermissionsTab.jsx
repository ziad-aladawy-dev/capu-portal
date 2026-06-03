import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Shield, Check, X, AlertTriangle, ExternalLink, Save, RotateCcw, CheckCircle } from "lucide-react";
import * as permissionService from "../../../../core/services/permissionService";
import { useToast } from "../../../../core/components/Toast";

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

function UserPermissionsTab({ userId, userType }) {
  const navigate = useNavigate();
  const { addToast } = useToast();

  const [allRoles, setAllRoles] = useState([]);
  const [assignedRoleIds, setAssignedRoleIds] = useState([]);
  const [overrides, setOverrides] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState(null);

  const [newSvcName, setNewSvcName] = useState("");
  const [newLevel, setNewLevel] = useState(5);
  const [newType, setNewType] = useState(1);

  useEffect(() => {
    permissionService.fetchAllRoles({ pageSize: 100 }).then((res) => {
      setAllRoles(res?.items || []);
    });
  }, []);

  const loadAssignment = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const assignment = await permissionService.fetchPermissionAssignment({ userId });
      setAssignedRoleIds(assignment?.roleIds || []);
      setOverrides(assignment?.permissionOverrides || []);
      setDirty(false);
    } catch (err) {
      setError(err.message || "Failed to load permissions");
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    loadAssignment();
  }, [loadAssignment]);

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
    setDirty(true);
    setSaved(false);
  };

  const handleRemoveOverride = (index) => {
    setOverrides((prev) => prev.filter((_, i) => i !== index));
    setDirty(true);
    setSaved(false);
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const currentAssignment = await permissionService.fetchPermissionAssignment({ userId });
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
        userId,
        rolesToAdd,
        rolesToRemove,
        permissionsToAdd,
        permissionsToRemove,
        structuralScope: { structureNodeId: null },
        temporalScope: { academicYearId: null, semesterId: null, alwaysActive: true },
      });

      setDirty(false);
      setSaved(true);
      addToast("Permissions saved successfully", "success");
      setTimeout(() => setSaved(false), 2000);
    } catch (err) {
      setError(err.message || "Failed to save");
      addToast(`Error: ${err.message}`, "error");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="perm-loading" style={{ padding: 40, textAlign: "center", color: "#9ca3af" }}>Loading permissions…</div>;
  }

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
        <div>
          <h3 className="section-title" style={{ margin: 0 }}>Role Assignments</h3>
          <p style={{ fontSize: 11, color: "#6b7280", margin: "4px 0 0" }}>Assign or remove roles for this user</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          {dirty && (
            <button className="perm-btn perm-btn-outline" onClick={loadAssignment} style={btnStyle}>
              <RotateCcw size={13} /> Reset
            </button>
          )}
          <button
            className="perm-btn perm-btn-primary"
            onClick={handleSave}
            disabled={!dirty || saving}
            style={{
              ...btnStyle,
              background: dirty ? "linear-gradient(135deg, #c9a84c, #e0c06a)" : "#e5e7eb",
              color: dirty ? "#1a1f5e" : "#9ca3af",
              cursor: dirty ? "pointer" : "not-allowed",
            }}
          >
            {saving ? "Saving…" : saved ? <><CheckCircle size={13} /> Saved!</> : <><Save size={13} /> Save Changes</>}
          </button>
          <button
            className="perm-btn"
            onClick={() => navigate("/admin/permissions")}
            style={{ ...btnStyle, background: "#f0f1f8", color: "#1a1f5e" }}
          >
            <ExternalLink size={13} /> Full Manager
          </button>
        </div>
      </div>

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", background: "rgba(220,38,38,0.08)", borderRadius: 8, marginBottom: 12, fontSize: 12, color: "#dc2626" }}>
          <AlertTriangle size={14} /> {error}
        </div>
      )}

      <div className="perm-role-grid" style={{ display: "flex", flexWrap: "wrap", gap: 6, marginBottom: 20 }}>
        {allRoles.map((role) => {
          const isAssigned = assignedRoleIds.includes(role.id);
          return (
            <button
              key={role.id}
              onClick={() => toggleRole(role.id)}
              style={{
                display: "inline-flex", alignItems: "center", gap: 6,
                padding: "6px 12px", borderRadius: 8, border: "1px solid",
                fontSize: 11, fontWeight: 700, cursor: "pointer",
                background: isAssigned ? "rgba(201,168,76,0.15)" : "#f8f9fb",
                borderColor: isAssigned ? "#c9a84c" : "#e5e7eb",
                color: isAssigned ? "#8a6815" : "#6b7280",
                transition: "0.15s ease",
              }}
            >
              <span style={{
                width: 14, height: 14, borderRadius: 3,
                background: isAssigned ? "#c9a84c" : "transparent",
                border: isAssigned ? "none" : "1.5px solid #d1d5db",
                display: "flex", alignItems: "center", justifyContent: "center",
              }}>
                {isAssigned && <Check size={10} color="white" />}
              </span>
              {role.name}
              {role.isSystemRole && (
                <span style={{ fontSize: 9, background: "rgba(26,31,94,0.08)", padding: "1px 5px", borderRadius: 4, color: "#1a1f5e" }}>system</span>
              )}
            </button>
          );
        })}
      </div>

      <h3 className="section-title" style={{ marginTop: 0 }}>Permission Overrides</h3>
      <p style={{ fontSize: 11, color: "#6b7280", margin: "-8px 0 12px" }}>Override specific permissions regardless of role assignments</p>

      <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
        <input
          type="text"
          placeholder="Resource name (e.g. users.management)"
          value={newSvcName}
          onChange={(e) => setNewSvcName(e.target.value)}
          style={{ flex: 1, padding: "8px 10px", borderRadius: 8, border: "1px solid #e5e7eb", fontSize: 12 }}
        />
        <select value={newLevel} onChange={(e) => setNewLevel(Number(e.target.value))} style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #e5e7eb", fontSize: 11 }}>
          {ACTION_LEVELS.map((l) => <option key={l.value} value={l.value}>{l.label}</option>)}
        </select>
        <select value={newType} onChange={(e) => setNewType(Number(e.target.value))} style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #e5e7eb", fontSize: 11 }}>
          {OVERRIDE_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
        <button
          onClick={handleAddOverride}
          disabled={!newSvcName.trim()}
          style={{
            padding: "8px 14px", borderRadius: 8, border: "none",
            background: newSvcName.trim() ? "linear-gradient(135deg, #c9a84c, #e0c06a)" : "#e5e7eb",
            color: newSvcName.trim() ? "#1a1f5e" : "#9ca3af",
            fontWeight: 700, fontSize: 11, cursor: newSvcName.trim() ? "pointer" : "not-allowed",
          }}
        >
          + Add
        </button>
      </div>

      {overrides.length === 0 ? (
        <p style={{ fontSize: 12, color: "#9ca3af", textAlign: "center", padding: 20 }}>No permission overrides configured.</p>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          {overrides.map((ov, i) => (
            <div key={i} style={{
              display: "flex", justifyContent: "space-between", alignItems: "center",
              padding: "8px 12px", background: "#f8f9fb", borderRadius: 8,
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <strong style={{ fontSize: 12 }}>{ov.resource}</strong>
                <span style={{
                  fontSize: 10, padding: "2px 6px", borderRadius: 4, fontWeight: 700,
                  background: `rgba(${ov.level > 2 ? "201,168,76" : "107,114,128"},0.12)`,
                  color: ov.level > 2 ? "#8a6815" : "#6b7280",
                }}>{ACTION_LEVELS[ov.level]?.label} ({ov.level})</span>
                <span style={{
                  fontSize: 10, padding: "2px 6px", borderRadius: 4, fontWeight: 700,
                  background: ov.type === 2 ? "rgba(220,38,38,0.1)" : "rgba(22,163,74,0.1)",
                  color: ov.type === 2 ? "#dc2626" : "#16a34a",
                }}>{ov.type === 2 ? "Deny" : "Allow"}</span>
              </div>
              <button onClick={() => handleRemoveOverride(i)} style={{ border: "none", background: "none", cursor: "pointer", color: "#9ca3af", padding: 4 }}>
                <X size={12} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

const btnStyle = {
  display: "inline-flex", alignItems: "center", gap: 6,
  padding: "7px 12px", borderRadius: 8, border: "none",
  fontSize: 11, fontWeight: 700, cursor: "pointer",
};

export default UserPermissionsTab;
