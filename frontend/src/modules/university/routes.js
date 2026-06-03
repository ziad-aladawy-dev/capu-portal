import { lazy } from "react";

const UniversityStructurePage = lazy(() => import("./pages/UniversityStructurePage"));

export default [
  {
<<<<<<< Updated upstream
    path: "/admin/university-structure",
    component: UniversityStructurePage,
    permission: "structure.structure.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Administration",
=======
    path: "/admin/university",
    component: UniversityStructurePage,
    permission: "university.structure.view",
    pageType: "management",
    menuItem: {
      category: "Organization",
>>>>>>> Stashed changes
      label: "University Structure",
      icon: "Building2",
    },
  },
];
