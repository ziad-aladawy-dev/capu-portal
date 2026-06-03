import { lazy } from "react";

const AdminDashboard = lazy(() => import("./pages/AdminDashboard"));

export default [
  {
    path: "/admin",
<<<<<<< Updated upstream
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
=======
    permission: "admin.dashboard.view",
    pageType: "management",
>>>>>>> Stashed changes
    isRedirect: true,
    redirectTo: "/admin/dashboard",
  },
  {
    path: "/admin/dashboard",
    component: AdminDashboard,
<<<<<<< Updated upstream
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Overview",
=======
    permission: "admin.dashboard.view",
    pageType: "management",
    menuItem: {
      category: "Admin",
>>>>>>> Stashed changes
      label: "Dashboard",
      icon: "LayoutDashboard",
    },
  },
];
