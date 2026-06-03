import { lazy } from "react";

const UniversityStructurePage = lazy(() => import("./pages/UniversityStructurePage"));

export default [
  {
    path: "/admin/university",
    component: UniversityStructurePage,
    permission: "university.structure.view",
    pageType: "management",
    menuItem: {
      category: "Organization",
      label: "University Structure",
      icon: "Building2",
    },
  },
];
