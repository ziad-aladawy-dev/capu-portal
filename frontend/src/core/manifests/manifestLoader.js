import landingManifest from "../../modules/landing/manifest.json";
import adminManifest from "../../modules/admin/manifest.json";
import universityManifest from "../../modules/university/manifest.json";
import usersManifest from "../../modules/users/manifest.json";
import staffManifest from "../../modules/staff/manifest.json";
import studentsManifest from "../../modules/students/manifest.json";
import permissionsManifest from "../../modules/permissions/manifest.json";

const MODULE_MANIFESTS = [
  landingManifest,
  adminManifest,
  universityManifest,
  usersManifest,
  staffManifest,
  studentsManifest,
  permissionsManifest,
];

export function getAllRoutes() {
  return MODULE_MANIFESTS.flatMap((m) => m.routes || []);
}

export function getAllMenuItems() {
  return MODULE_MANIFESTS.flatMap((m) => m.menuItems || []);
}

export function getGroupedMenuItems() {
  const items = getAllMenuItems();
  const grouped = {};

  for (const item of items) {
    if (!grouped[item.category]) {
      grouped[item.category] = [];
    }
    grouped[item.category].push(item);
  }

  return grouped;
}

export function getAllPermissions() {
  const perms = {};

  for (const m of MODULE_MANIFESTS) {
    if (m.permissions?.resources) {
      for (const r of m.permissions.resources) {
        perms[r] = { moduleId: m.id, resource: r };
      }
    }
  }

  return perms;
}

export function getModuleManifest(moduleId) {
  return MODULE_MANIFESTS.find((m) => m.id === moduleId) || null;
}

export { MODULE_MANIFESTS };
