import { lazy } from "react";

const StudentDirectory = lazy(() => import("./pages/StudentDirectory"));

export default [
  {
    path: "/admin/students",
    component: StudentDirectory,
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "People Management",
      label: "Student Directory",
      icon: "GraduationCap",
    },
  },
];
