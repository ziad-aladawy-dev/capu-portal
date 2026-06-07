import { lazy } from "react";

const AcademicPlansPage = lazy(() => import("./pages/AcademicPlansPage"));

export default [
  {
    path: "/admin/academic-plans",
    component: AcademicPlansPage,
    permission: "courses.academic-plans.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Academic Plans",
      icon: "ClipboardList",
    },
  },
];
