import React, { useState } from "react";
import { Save, RotateCcw } from "lucide-react";
import { usePermissions } from "../../hooks/use-permissions";
import { useBlocker } from "react-router-dom";
import { ModuleAccordion } from "./ModuleAccordion";
import "./PermissionMatrix.css";

export const PermissionMatrix = ({ initialPermissions = [], onSave = null }) => {
  const {
    permissions,
    hasUnsavedChanges,
    updatePermission,
    setModuleLevel,
    savePermissions,
    resetChanges
  } = usePermissions(initialPermissions);

  const [showSaveConfirm, setShowSaveConfirm] = useState(false);

  // Use React Router's useBlocker to prevent navigation with unsaved changes
  useBlocker(
    ({ currentLocation, nextLocation }) =>
      hasUnsavedChanges && currentLocation.pathname !== nextLocation.pathname
  );

  // Group permissions by module
  const groupedPermissions = permissions.reduce((acc, perm) => {
    if (!acc[perm.module]) {
      acc[perm.module] = [];
    }
    acc[perm.module].push({
      name: perm.resource,
      level: perm.level
    });
    return acc;
  }, {});

  const handleSave = async () => {
    const success = await savePermissions();
    if (success && onSave) {
      onSave(permissions);
    }
    setShowSaveConfirm(false);
  };

  const handleDiscard = () => {
    resetChanges();
  };

  const handleReset = () => {
    // Confirm before resetting
    if (window.confirm("Are you sure you want to discard all unsaved changes?")) {
      handleDiscard();
    }
  };

  return (
    <div className="permission-matrix">
      {/* Header */}
      <div className="permission-matrix-header">
        <div>
          <h1 className="matrix-title">Permission Management</h1>
          <p className="matrix-subtitle">
            Manage module access and resource-level permissions for users
          </p>
        </div>

        {hasUnsavedChanges && (
          <div className="unsaved-indicator">
            <span className="unsaved-dot"></span>
            <span className="unsaved-text">Unsaved Changes</span>
          </div>
        )}
      </div>

      {/* Actions Bar */}
      <div className="permission-actions">
        <button 
          className={`action-btn save-btn ${!hasUnsavedChanges ? "disabled" : ""}`}
          onClick={() => setShowSaveConfirm(true)}
          disabled={!hasUnsavedChanges}
          title="Save permission changes"
        >
          <Save size={18} />
          Save Changes
        </button>

        <button 
          className={`action-btn reset-btn ${!hasUnsavedChanges ? "disabled" : ""}`}
          onClick={handleReset}
          disabled={!hasUnsavedChanges}
          title="Discard unsaved changes"
        >
          <RotateCcw size={18} />
          Discard Changes
        </button>
      </div>

      {/* Confirmation Dialog */}
      {showSaveConfirm && (
        <div className="modal-overlay" onClick={() => setShowSaveConfirm(false)}>
          <div className="modal-content">
            <h3>Save Changes?</h3>
            <p>Are you sure you want to save these permission changes?</p>
            <div className="modal-actions">
              <button 
                className="modal-btn secondary"
                onClick={() => setShowSaveConfirm(false)}
              >
                Cancel
              </button>
              <button 
                className="modal-btn primary"
                onClick={handleSave}
              >
                Save Changes
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modules */}
      <div className="modules-container">
        {Object.entries(groupedPermissions).map(([module, resources]) => (
          <ModuleAccordion
            key={module}
            module={module}
            resources={resources}
            onPermissionChange={updatePermission}
            onSetModuleLevel={setModuleLevel}
          />
        ))}
      </div>

      {/* Empty State */}
      {Object.keys(groupedPermissions).length === 0 && (
        <div className="empty-state">
          <p>No permissions configured yet.</p>
        </div>
      )}
    </div>
  );
};
