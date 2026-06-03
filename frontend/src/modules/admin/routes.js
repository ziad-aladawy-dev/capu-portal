import { lazy } from "react";

const AdminDashboard = lazy(() => import("./pages/AdminDashboard"));

export default [
  {
    path: "/admin",
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    isRedirect: true,
    redirectTo: "/admin/dashboard",
  },
  {
    path: "/admin/dashboard",
    component: AdminDashboard,
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Overview",
      label: "Dashboard",
      icon: "LayoutDashboard",
    },
  },
];
