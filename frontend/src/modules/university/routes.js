import { lazy } from "react";

const UniversityStructurePage = lazy(() => import("./pages/UniversityStructurePage"));

export default [
  {
    path: "/admin/university-structure",
    component: UniversityStructurePage,
    permission: "structure.structure.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Administration",
      label: "University Structure",
      icon: "Building2",
    },
  },
];
