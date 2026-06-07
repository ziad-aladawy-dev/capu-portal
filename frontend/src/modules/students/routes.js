import { lazy } from "react";
import { studentDirectoryConfig } from "../../core/components/directory/directoryConfigs";

const DirectoryPage = lazy(() => import("../../core/components/directory/DirectoryPage"));
const StudentDetailPage = lazy(() => import("./pages/StudentDetailPage"));

export default [
  {
    path: "/admin/students",
    component: DirectoryPage,
    props: { config: studentDirectoryConfig },
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
