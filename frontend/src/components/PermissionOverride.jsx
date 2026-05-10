import React, { useState } from "react";
import { Plus, X, ChevronDown } from "lucide-react";
import { useAuth } from "../hooks/use-auth";
import "./PermissionOverride.css";

export const PermissionOverride = () => {
  const { user, permissions } = useAuth();
  const [showAddMenu, setShowAddMenu] = useState(false);

  // Separate added and removed permissions
  const addedPermissions = permissions?.filter(p => p.type === "added") || [];
  const removedPermissions = permissions?.filter(p => p.type === "removed") || [];

  const allModules = [
    "students",
    "admin",
    "financial",
    "registration",
    "permissions"
  ];

  const getAvailableModules = () => {
    const currentModules = [
      ...addedPermissions.map(p => p.module),
      ...removedPermissions.map(p => p.module)
    ];
    return allModules.filter(m => !currentModules.includes(m));
  };

  const handleAddPermission = (module) => {
    // This would typically call an API to add a permission
    console.log("Adding permission for:", module);
    setShowAddMenu(false);
  };

  const handleRemovePermission = (type, module) => {
    // This would typically call an API to remove a permission
    console.log("Removing permission:", type, module);
  };

  return (
    <div className="permission-override-container">
      <div className="override-section">
        <h3>Role</h3>
        <div className="role-badge">
          {user?.role || "Admin"}
        </div>
        <p className="override-description">
          Your base role and its associated permissions
        </p>
      </div>

      {addedPermissions.length > 0 && (
        <div className="override-section">
          <h3>Added Permissions</h3>
          <div className="permissions-list">
            {addedPermissions.map((perm) => (
              <div key={`added-${perm.module}`} className="permission-item added">
                <span className="permission-badge added-badge">+</span>
                <span className="permission-name">{perm.module}</span>
                <button
                  className="remove-button"
                  onClick={() => handleRemovePermission("added", perm.module)}
                  title="Remove added permission"
                >
                  <X size={16} />
                </button>
              </div>
            ))}
          </div>
          <p className="override-description">
            Additional permissions beyond your base role
          </p>
        </div>
      )}

      {removedPermissions.length > 0 && (
        <div className="override-section">
          <h3>Removed Permissions</h3>
          <div className="permissions-list">
            {removedPermissions.map((perm) => (
              <div key={`removed-${perm.module}`} className="permission-item removed">
                <span className="permission-badge removed-badge">−</span>
                <span className="permission-name">{perm.module}</span>
                <button
                  className="remove-button"
                  onClick={() => handleRemovePermission("removed", perm.module)}
                  title="Restore removed permission"
                >
                  <X size={16} />
                </button>
              </div>
            ))}
          </div>
          <p className="override-description">
            Permissions removed from your base role
          </p>
        </div>
      )}

      {getAvailableModules().length > 0 && (
        <div className="override-section">
          <div className="add-permission-header">
            <h3>Add Permission</h3>
            <div className="add-dropdown">
              <button
                className="add-button"
                onClick={() => setShowAddMenu(!showAddMenu)}
              >
                <Plus size={16} />
                Add Permission
                <ChevronDown size={16} />
              </button>
              {showAddMenu && (
                <div className="add-menu">
                  {getAvailableModules().map((module) => (
                    <button
                      key={module}
                      className="add-menu-item"
                      onClick={() => handleAddPermission(module)}
                    >
                      {module}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
