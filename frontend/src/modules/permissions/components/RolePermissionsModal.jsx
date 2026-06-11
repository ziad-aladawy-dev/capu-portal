import { useState, useMemo } from "react";
import { X, Shield, Save } from "lucide-react";
import { useTranslation } from "react-i18next";
import { useRolePermissions, useUpdateRolePermissions } from "../../../core/query/usePermissionsData";
import { usePermission } from "../../../core/auth/usePermission";
import { useToast } from "../../../core/components/Toast";
import {
  LEVEL_TO_ACTION, PERMISSION_RESOURCES, computeResourceLevel,
} from "../../../core/constants/permissionLevels";
import PermissionMatrix from "./PermissionMatrix";
import "../styles/roles.css";

function RolePermissionsModal({ role, onClose }) {
  const { t } = useTranslation();
  const { can } = usePermission();
  const { addToast } = useToast();
  const canEdit = can(PERMISSION_RESOURCES.ROLES, 3);

  const permsQuery = useRolePermissions(role.id);
  const modules = useMemo(() => permsQuery.data ?? [], [permsQuery.data]);
  const updateRolePerms = useUpdateRolePermissions();
  const [pendingLevels, setPendingLevels] = useState({});

  const originalLevels = useMemo(() => {
    const levels = {};
    for (const mod of modules) {
      for (const res of (mod.resources || [])) {
        levels[`${mod.moduleId}::${res.resourceId}`] = computeResourceLevel(res.permissions || []);
      }
    }
    return levels;
  }, [modules]);

  const displayLevels = useMemo(
    () => ({ ...originalLevels, ...pendingLevels }),
    [originalLevels, pendingLevels]
  );

  const handleLevelChange = (moduleId, res, newLevel) => {
    const key = `${moduleId}::${res.resourceId}`;
    setPendingLevels((prev) => {
      if (newLevel === (originalLevels[key] || 0)) {
        const next = { ...prev };
        delete next[key];
        return next;
      }
      return { ...prev, [key]: newLevel };
    });
  };

  const saving = updateRolePerms.isPending;

  const handleSave = () => {
    if (saving) return;
    const resources = Object.entries(displayLevels)
      .filter(([, level]) => level > 0)
      .map(([compositeKey, level]) => ({
        resourceId: compositeKey.split("::")[1],
        actions: [LEVEL_TO_ACTION[level]].filter(Boolean),
      }));
    updateRolePerms.mutate({ roleId: role.id, resources }, {
      onSuccess: () => onClose(),
      onError: (err) => addToast(err.message || "Failed to save permissions", "error"),
    });
  };

  const totalConfigured = Object.values(displayLevels).filter((l) => l > 0).length;
  const totalResources = modules.reduce((sum, m) => sum + (m.resources?.length || 0), 0);

  return (
    <div className="roles-modal-overlay" onClick={onClose}>
      <div className="roles-modal" style={{ width: 720, maxWidth: "92vw", maxHeight: "85vh" }} onClick={(e) => e.stopPropagation()}>
        <div className="roles-modal-header">
          <div className="roles-modal-header-left">
            <Shield size={18} />
            <div>
              <h2>{t("role_permissions")}</h2>
              <p className="roles-modal-subtitle">{role.name}</p>
            </div>
          </div>
          <button className="roles-modal-close" onClick={onClose} disabled={saving}><X size={16} /></button>
        </div>

        <div className="roles-modal-body">
          {permsQuery.isPending ? (
            <div className="roles-loading" style={{ padding: "40px 0" }}>
              <div className="roles-spinner" />
              <p>{t("loading_permissions")}</p>
            </div>
          ) : modules.length === 0 ? (
            <div className="roles-empty" style={{ padding: "40px 0" }}>
              <p>{t("no_permissions_found")}</p>
            </div>
          ) : (
            <PermissionMatrix
              modules={modules}
              getLevel={(moduleId, res) => displayLevels[`${moduleId}::${res.resourceId}`] || 0}
              onLevelChange={handleLevelChange}
              canEdit={canEdit}
              height={360}
            />
          )}

          <div style={{ fontSize: 12, color: "var(--gray-400)", textAlign: "center", padding: "8px 0 0", borderTop: "1px solid #f0f1f8", marginTop: 12 }}>
            {t("resources_configured", { configured: totalConfigured, total: totalResources })}
          </div>
        </div>

        <div className="roles-modal-footer">
          <button className="btn-outline" onClick={onClose} disabled={saving}>{t("cancel")}</button>
          <button
            className="btn-primary"
            onClick={handleSave}
            disabled={!canEdit || saving || permsQuery.isPending}
            title={canEdit ? undefined : t("requires_permission_level", { defaultValue: 'Requires "Edit" access on roles', level: t("edit") })}
          >
            {saving ? t("saving") : <><Save size={14} /> {t("save_permissions")}</>}
          </button>
        </div>
      </div>
    </div>
  );
}

export default RolePermissionsModal;
