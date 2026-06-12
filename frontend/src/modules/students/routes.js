import { lazy } from "react";
import { studentDirectoryConfig } from "../../core/components/directory/directoryConfigs";

const PeopleDashboardPage = lazy(() => import("./pages/PeopleDashboardPage"));
const DirectoryPage = lazy(() => import("../../core/components/directory/DirectoryPage"));
const StudentDetailPage = lazy(() => import("./pages/StudentDetailPage"));
const StudentAcademicHubPage = lazy(() => import("./pages/StudentAcademicHubPage"));

export default [
  {
    path: "/admin/people/dashboard",
    component: PeopleDashboardPage,
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "People Management",
      label: "Overview",
      icon: "BarChart3",
    },
  },
  {
    path: "/admin/students",
    component: DirectoryPage,
    props: { config: studentDirectoryConfig },
    permission: "users.users.view",
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
  {
    path: "/admin/students/:id/academics",
    component: StudentAcademicHubPage,
    permission: "users.users.view",
    pageType: "entity",
    applicableTo: "student",
  },
];
