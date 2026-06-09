import { lazy } from "react";

const StudentProfileRecordsPage = lazy(() => import("./pages/StudentProfileRecordsPage"));
const StandaloneProfileRecordsPage = lazy(() => import("./pages/StandaloneProfileRecordsPage"));

export default [
  {
    path: "/admin/students/:studentId/profile-records",
    component: StudentProfileRecordsPage,
    permission: "student-information.profile-records.view",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/admin/student-information/profile-records",
    component: StandaloneProfileRecordsPage,
    permission: "student-information.profile-records.view",
    pageType: "management",
    menuItem: {
      category: "Student Information",
      label: "Profile Records",
      icon: "FileText",
    },
  },
];
