import {
  MODULE_MANIFESTS,
  getAllRoutes,
  getAllMenuItems,
  getGroupedMenuItems,
  getAllPermissions,
  getModuleManifest,
} from "./core/manifests/manifestLoader";

const modulesConfig = {
  manifests: MODULE_MANIFESTS,
  getAllRoutes,
  getAllMenuItems,
  getGroupedMenuItems,
  getAllPermissions,
  getModuleManifest,
};

export default modulesConfig;
