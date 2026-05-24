import { lazy } from "react";

const StaffDirectory = lazy(() => import("./pages/StaffDirectory"));

export default [
  {
    path: "/admin/staff",
    component: StaffDirectory,
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "People Management",
      label: "Staff Directory",
      icon: "Users",
    },
  },
];
