import { lazy } from "react";

const ProgramsPage = lazy(() => import("./pages/ProgramsPage"));

export default [
  {
    path: "/admin/programs",
    component: ProgramsPage,
    permission: "programs.programs.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Programs",
      icon: "BookOpen",
    },
  },
];
