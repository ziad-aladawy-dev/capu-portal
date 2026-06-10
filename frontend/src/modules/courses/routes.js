import { lazy } from "react";

const CoursesPage = lazy(() => import("./pages/CoursesPage"));
const CourseHubPage = lazy(() => import("./pages/CourseHubPage"));

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
  {
    path: "/admin/academic/course-hub",
    component: CourseHubPage,
    permission: "courses.courses.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Course Hub",
      icon: "Library",
    },
  },
];
