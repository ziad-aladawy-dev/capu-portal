import { useState, useEffect } from "react";
import { X, Shield, ChevronDown, ChevronRight, Save, AlertCircle } from "lucide-react";
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
  View: 1,
  Insert: 2,
  EditClose: 3,
  Open: 4,
  Delete: 5,
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
  const [expandedModules, setExpandedModules] = useState(new Set());

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
            // Use a composite key: moduleId + resourceId to guarantee uniqueness
            const key = `${mod.moduleId}::${res.resourceId}`;
            levels[key] = computeResourceLevel(res.permissions || []);
          }
        }
        setResourceLevels(levels);

        // Expand all modules by default so the user sees the full structure
        setExpandedModules(new Set(tree.map(m => m.moduleId)));
      } catch (err) {
        setError(err.message || "Failed to load permissions");
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [role.id]);

  const toggleModule = (moduleId) => {
    setExpandedModules((prev) => {
      const next = new Set(prev);
      if (next.has(moduleId)) next.delete(moduleId);
      else next.add(moduleId);
      return next;
    });
  };

  const handleLevelChange = (compositeKey, level) => {
    setResourceLevels((prev) => ({ ...prev, [compositeKey]: level }));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      // Extract the actual resourceId from the composite key (moduleId::resourceId)
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

  const totalCount = modules.reduce((sum, m) => sum + (m.resources?.length || 0), 0);
  const configuredCount = Object.values(resourceLevels).filter((l) => l > 0).length;

  return (
    <div className="roles-modal-overlay" onClick={onClose}>
      <div className="roles-modal roles-modal-wide" onClick={(e) => e.stopPropagation()}>
        <div className="roles-modal-header">
          <div className="roles-modal-header-left">
            <Shield size={18} />
            <div>
              <h2>Role Permissions</h2>
              <p className="roles-modal-subtitle">{role.name}</p>
            </div>
          </div>
          <button className="roles-modal-close" onClick={onClose} disabled={saving}>
            <X size={16} />
          </button>
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
            <div className="rp-module-list">
              {modules.map((mod) => {
                const isExpanded = expandedModules.has(mod.moduleId);
                const resCount = mod.resources?.length || 0;
                const configuredResCount = (mod.resources || []).filter(
                  (r) => (resourceLevels[`${mod.moduleId}::${r.resourceId}`] || 0) > 0
                ).length;

                return (
                  <div key={mod.moduleId} className="rp-module">
                    <button
                      className="rp-module-header"
                      onClick={() => toggleModule(mod.moduleId)}
                    >
                      {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                      <span className="rp-module-name">{mod.moduleName}</span>
                      <span className="rp-module-count">
                        {configuredResCount}/{resCount}
                      </span>
                    </button>
                    {isExpanded && (
                      <div className="rp-module-resources">
                        {(mod.resources || []).map((res) => {
                          const compositeKey = `${mod.moduleId}::${res.resourceId}`;
                          return (
                            <div key={compositeKey} className="rp-resource-row">
                              <div className="rp-resource-info">
                                <span className="rp-resource-name">{res.resourceName}</span>
                              </div>
                              <select
                                className="rp-level-select"
                                value={resourceLevels[compositeKey] || 0}
                                onChange={(e) => handleLevelChange(compositeKey, Number(e.target.value))}
                              >
                                {ACTION_LEVELS.map((l) => (
                                  <option key={l.value} value={l.value}>
                                    {l.label} ({l.value})
                                  </option>
                                ))}
                              </select>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}

          <div className="roles-permission-count">
            {configuredCount} of {totalCount} resources configured
          </div>
        </div>

        <div className="roles-modal-footer">
          <button
            className="roles-btn roles-btn-outline"
            onClick={onClose}
            disabled={saving}
          >
            Cancel
          </button>
          <button
            className="roles-btn roles-btn-primary"
            onClick={handleSave}
            disabled={saving || loading}
          >
            {saving ? "Saving..." : <><Save size={14} /> Save Permissions</>}
          </button>
        </div>
      </div>
    </div>
  );
}

export default RolePermissionsModal;