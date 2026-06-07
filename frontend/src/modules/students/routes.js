import { lazy } from "react";

const StudentDirectory = lazy(() => import("./pages/StudentDirectory"));
const StudentDetailPage = lazy(() => import("./pages/StudentDetailPage"));

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
  {
    path: "/admin/students/:id",
    component: StudentDetailPage,
    permission: "users.users.view",
    pageType: "entity",
    applicableTo: "student",
  },
];
