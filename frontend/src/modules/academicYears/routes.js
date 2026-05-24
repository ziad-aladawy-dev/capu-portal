import { lazy } from "react";

const AcademicYearsPage = lazy(() => import("./pages/AcademicYearsPage"));

export default [
  {
    path: "/admin/academic-years",
    component: AcademicYearsPage,
    permission: "academicYears.academicYears.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Administration",
      label: "Academic Years",
      icon: "CalendarRange",
    },
  },
];
