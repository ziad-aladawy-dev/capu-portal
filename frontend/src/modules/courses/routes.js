import { lazy } from "react";

const CoursesPage = lazy(() => import("./pages/CoursesPage"));

export default [
  {
    path: "/admin/courses",
    component: CoursesPage,
    permission: "courses.courses.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Course Catalog",
      icon: "BookOpen",
    },
  },
];
