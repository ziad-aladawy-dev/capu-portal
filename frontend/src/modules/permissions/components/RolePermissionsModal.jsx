import { useState, useEffect } from "react";
import { X, Shield, Check, Search, AlertCircle, Save } from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import "../../permissions/styles/roles.css";

function RolePermissionsModal({ role, onClose }) {
  const [allPermissions, setAllPermissions] = useState([]);
  const [rolePermissionIds, setRolePermissionIds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState("");

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      setError(null);
      try {
        const [perms, rolePerms] = await Promise.all([
          permissionService.fetchAllPermissions(),
          permissionService.fetchRolePermissions(role.id),
        ]);
        setAllPermissions(perms?.items || perms || []);
        setRolePermissionIds(rolePerms?.permissionIds || rolePerms?.map((p) => p.id) || []);
      } catch (err) {
        setError(err.message || "Failed to load permissions");
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [role.id]);

  const togglePermission = (permId) => {
    setRolePermissionIds((prev) =>
      prev.includes(permId)
        ? prev.filter((id) => id !== permId)
        : [...prev, permId]
    );
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      await permissionService.updateRolePermissions(role.id, {
        permissionIds: rolePermissionIds,
      });
      onClose();
    } catch (err) {
      setError(err.message || "Failed to save permissions");
    } finally {
      setSaving(false);
    }
  };

  const filteredPermissions = allPermissions.filter((p) => {
    const name = (p.name || p.resource || "").toLowerCase();
    return name.includes(searchTerm.toLowerCase());
  });

  return (
    <div className="roles-modal-overlay" onClick={onClose}>
      <div className="roles-modal roles-modal-wide" onClick={(e) => e.stopPropagation()}>
        <div className="roles-modal-header">
          <div className="roles-modal-header-left">
            <Shield size={18} />
            <div>
              <h2>Role Permissions</h2>
              <p className="roles-modal-subtitle">
                {role.name}
              </p>
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

          <div className="roles-permission-search">
            <Search size={14} />
            <input
              type="text"
              placeholder="Search permissions..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          {loading ? (
            <div className="roles-loading" style={{ padding: "40px 0" }}>
              <div className="roles-spinner" />
              <p>Loading permissions...</p>
            </div>
          ) : filteredPermissions.length === 0 ? (
            <div className="roles-empty" style={{ padding: "40px 0" }}>
              <p>No permissions found</p>
            </div>
          ) : (
            <div className="roles-permission-list">
              {filteredPermissions.map((perm) => {
                const isChecked = rolePermissionIds.includes(perm.id);
                return (
                  <label
                    key={perm.id}
                    className={`roles-permission-item ${isChecked ? "checked" : ""}`}
                  >
                    <div className="roles-permission-check">
                      <div className={`roles-checkbox ${isChecked ? "checked" : ""}`}>
                        {isChecked && <Check size={12} />}
                      </div>
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => togglePermission(perm.id)}
                        style={{ display: "none" }}
                      />
                    </div>
                    <div className="roles-permission-info">
                      <span className="roles-permission-name">
                        {perm.name || perm.resource}
                      </span>
                      <span className="roles-permission-resource">
                        {perm.module || ""} {perm.resource ? `> ${perm.resource}` : ""}
                      </span>
                    </div>
                    <span className="roles-permission-level">
                      {perm.action || "View"}
                    </span>
                  </label>
                );
              })}
            </div>
          )}

          <div className="roles-permission-count">
            {rolePermissionIds.length} of {allPermissions.length} permissions selected
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
            {saving ? (
              <>Saving...</>
            ) : (
              <>
                <Save size={14} /> Save Permissions
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}

export default RolePermissionsModal;
