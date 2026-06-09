import { lazy } from "react";

const SyncPage = lazy(() => import("./pages/SyncPage"));

export default [
  {
    path: "/admin/sync",
    component: SyncPage,
    permission: "sync.sync.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Administration",
      label: "SIS Integration",
      icon: "RefreshCw",
    },
  },
];
