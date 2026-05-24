import { lazy } from "react";

const PermissionTreePage = lazy(() => import("./pages/PermissionTreePage"));

export default [
  {
    path: "/admin/authorization",
    component: PermissionTreePage,
    permission: "permissions.permissions.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Security & Access",
      label: "Permission Tree",
      icon: "FolderTree",
    },
  },
];
