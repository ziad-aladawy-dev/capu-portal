import { lazy } from "react";

const PermissionsPage = lazy(() => import("./pages/PermissionsPage"));
const RolesPage = lazy(() => import("./pages/RolesPage"));

export default [
  {
    path: "/admin/permissions",
    component: PermissionsPage,
    permission: "permissions.permissions.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Security & Access",
      label: "Permissions Matrix",
      icon: "Shield",
    },
  },
  {
    path: "/admin/roles",
    component: RolesPage,
    permission: "permissions.permissions.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Security & Access",
      label: "Roles",
      icon: "UserCog",
    },
  },
];
