// Single source of truth for the permission level model shared by
// RolesPage, PermissionsPage and RolePermissionsModal. The backend speaks
// action names (View/Insert/EditClose/Open/Delete); the UI speaks levels 0-5.

export const ACTION_LEVELS = [
  { value: 0, label: "No Permission" },
  { value: 1, label: "View" },
  { value: 2, label: "Insert" },
  { value: 3, label: "Edit" },
  { value: 4, label: "Open" },
  { value: 5, label: "Delete" },
];

export const ACTION_NAME_TO_LEVEL = {
  View: 1, Insert: 2, EditClose: 3, Open: 4, Delete: 5,
};

export const LEVEL_TO_ACTION = { 1: "View", 2: "Insert", 3: "EditClose", 4: "Open", 5: "Delete" };

// UI label -> backend action name ("Edit" renders in the UI but the API expects "EditClose")
export const LABEL_TO_ACTION = {
  View: "View",
  Insert: "Insert",
  Edit: "EditClose",
  Open: "Open",
  Delete: "Delete",
};

export const PERMISSION_RESOURCES = {
  ROLES: "permissions.roles",
  PERMISSIONS: "permissions.permissions",
};

// Highest assigned action level on a resource's permission list.
export function computeResourceLevel(permissions) {
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

// Translated level labels; call with the i18n t function inside a component.
export function getLevelLabels(t) {
  return {
    0: t("no_permission"),
    1: t("view"),
    2: t("insert"),
    3: t("edit"),
    4: t("open_level"),
    5: t("delete"),
  };
}
