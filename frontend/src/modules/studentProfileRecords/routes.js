import { lazy } from "react";

const StudentProfileRecordsPage = lazy(() => import("./pages/StudentProfileRecordsPage"));

export default [
  {
    path: "/admin/students/:studentId/profile-records",
    component: StudentProfileRecordsPage,
    permission: "student-information.profile-records.view",
    pageType: "entity",
    applicableTo: "student",
  },
];
