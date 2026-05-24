import { lazy } from "react";

const NotificationsPage = lazy(() => import("./pages/NotificationsPage"));

export default [
  {
    path: "/admin/notifications",
    component: NotificationsPage,
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Overview",
      label: "Notifications",
      icon: "Bell",
    },
  },
];
