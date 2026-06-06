import { lazy } from "react";

const AdminDashboard = lazy(() => import("./pages/AdminDashboard"));

export default [
  {
    path: "/admin",
    permission: "dashboard.dashboard.view",
    pageType: "management",
    isRedirect: true,
    redirectTo: "/admin/dashboard",
  },
  {
    path: "/admin/dashboard",
    component: AdminDashboard,
    permission: "dashboard.dashboard.view",
    pageType: "management",
    menuItem: {
      category: "Admin",
      label: "Dashboard",
      icon: "LayoutDashboard",
    },
  },
];
