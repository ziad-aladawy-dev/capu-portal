import { lazy } from "react";

const AddStudent = lazy(() => import("./pages/AddStudent"));
const EditStudent = lazy(() => import("./pages/EditStudent"));
const AddStaff = lazy(() => import("./pages/AddStaff"));
const EditStaff = lazy(() => import("./pages/EditStaff"));
const UserHubPage = lazy(() => import("./pages/UserHubPage"));

export default [
  {
    path: "/admin/users/add-student",
    component: AddStudent,
    permission: "users.users.insert",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/admin/users/edit-student/:id",
    component: EditStudent,
    permission: "users.users.editclose",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/admin/users/add-staff",
    component: AddStaff,
    permission: "users.users.insert",
    pageType: "entity",
    applicableTo: "staff",
  },
  {
    path: "/admin/users/edit-staff/:id",
    component: EditStaff,
    permission: "users.users.editclose",
    pageType: "entity",
    applicableTo: "staff",
  },
  {
    path: "/admin/users/:id",
    component: UserHubPage,
    permission: "users.users.view",
    pageType: "entity",
    applicableTo: "both",
  },
];
