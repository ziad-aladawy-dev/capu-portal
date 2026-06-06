import { useState, useEffect } from "react";
import { X, Shield, Save, AlertCircle } from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import "../../permissions/styles/roles.css";

const ACTION_LEVELS = [
  { value: 0, label: "None" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];

const ACTION_NAME_TO_LEVEL = {
  View: 1, Insert: 2, EditClose: 3, Open: 4, Delete: 5,
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

function RolePermissionsModal({ role, onClose }) {
  const [modules, setModules] = useState([]);
  const [resourceLevels, setResourceLevels] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [activeModuleId, setActiveModuleId] = useState(null);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await permissionService.fetchRolePermissions(role.id);
        const tree = Array.isArray(data) ? data : [];
        setModules(tree);

        const levels = {};
        for (const mod of tree) {
          for (const res of (mod.resources || [])) {
            const key = `${mod.moduleId}::${res.resourceId}`;
            levels[key] = computeResourceLevel(res.permissions || []);
          }
        }
        setResourceLevels(levels);

        if (tree.length > 0) {
          setActiveModuleId(tree[0].moduleId);
        }
      } catch (err) {
        setError(err.message || "Failed to load permissions");
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [role.id]);

  const activeModule = modules.find((m) => m.moduleId === activeModuleId);

  const handleLevelChange = (compositeKey, level) => {
    setResourceLevels((prev) => ({ ...prev, [compositeKey]: level }));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const permissions = Object.entries(resourceLevels)
        .filter(([, level]) => level > 0)
        .map(([compositeKey, level]) => {
          const resourceId = compositeKey.split("::")[1];
          return { resourceId, level };
        });
      await permissionService.updateRolePermissions(role.id, { permissions });
      onClose();
    } catch (err) {
      setError(err.message || "Failed to save permissions");
    } finally {
      setSaving(false);
    }
  };

  const totalConfigured = Object.values(resourceLevels).filter((l) => l > 0).length;
  const totalResources = modules.reduce((sum, m) => sum + (m.resources?.length || 0), 0);

  return (
    <div className="roles-modal-overlay" onClick={onClose}>
      <div className="roles-modal" style={{ width: 640, maxWidth: "90vw", maxHeight: "85vh" }} onClick={(e) => e.stopPropagation()}>
        <div className="roles-modal-header">
          <div className="roles-modal-header-left">
            <Shield size={18} />
            <div>
              <h2>Role Permissions</h2>
              <p className="roles-modal-subtitle">{role.name}</p>
            </div>
          </div>
          <button className="roles-modal-close" onClick={onClose} disabled={saving}><X size={16} /></button>
        </div>

        <div className="roles-modal-body">
          {error && (
            <div className="roles-permission-error">
              <AlertCircle size={14} />
              <span>{error}</span>
            </div>
          )}

          {loading ? (
            <div className="roles-loading" style={{ padding: "40px 0" }}>
              <div className="roles-spinner" />
              <p>Loading permissions...</p>
            </div>
          ) : modules.length === 0 ? (
            <div className="roles-empty" style={{ padding: "40px 0" }}>
              <p>No permissions found</p>
            </div>
          ) : (
            <div className="perm-tree-layout" style={{ border: "1px solid #e5e7eb", borderRadius: 10, overflow: "hidden" }}>
              <div className="perm-tree-modules">
                {modules.map((mod) => (
                  <button
                    key={mod.moduleId}
                    className={`perm-tree-module-btn ${activeModuleId === mod.moduleId ? "active" : ""}`}
                    onClick={() => setActiveModuleId(mod.moduleId)}
                  >
                    <span>{mod.moduleName}</span>
                  </button>
                ))}
              </div>
              <div className="perm-tree-resources" style={{ maxHeight: 360 }}>
                {!activeModule || activeModule.resources?.length === 0 ? (
                  <div className="perm-tree-empty">No resources in this module.</div>
                ) : (
                  activeModule.resources.map((res) => {
                    const key = `${activeModule.moduleId}::${res.resourceId}`;
                    const currentLevel = resourceLevels[key] || 0;
                    return (
                      <div key={key} className="perm-tree-resource-row">
                        <span className="perm-tree-resource-name">{res.resourceName}</span>
                        <div className="perm-level-selector">
                          {ACTION_LEVELS.filter((l) => l.value > 0).map((l) => (
                            <button
                              key={l.value}
                              className={`perm-lvl-btn ${currentLevel >= l.value ? "filled" : ""} ${currentLevel === l.value ? "current" : ""}`}
                              onClick={() => handleLevelChange(key, currentLevel === l.value ? 0 : l.value)}
                              title={l.label}
                            >
                              {l.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          )}

          <div style={{ fontSize: 12, color: "#9ca3af", textAlign: "center", padding: "8px 0 0", borderTop: "1px solid #f0f1f8", marginTop: 12 }}>
            {totalConfigured} of {totalResources} resources configured
          </div>
        </div>

        <div className="roles-modal-footer">
          <button className="roles-btn roles-btn-outline" onClick={onClose} disabled={saving}>Cancel</button>
          <button className="roles-btn roles-btn-primary" onClick={handleSave} disabled={saving || loading}>
            {saving ? "Saving..." : <><Save size={14} /> Save Permissions</>}
          </button>
        </div>
      </div>
    </div>
  );
}

export default RolePermissionsModal;
