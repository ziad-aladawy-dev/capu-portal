import { lazy } from "react";

const UserManagement = lazy(() => import("./pages/UserManagement"));
const UserHubPage = lazy(() => import("./pages/UserHubPage"));
const AddStaff = lazy(() => import("./pages/AddStaff"));
const EditStaff = lazy(() => import("./pages/EditStaff"));
const AddStudent = lazy(() => import("./pages/AddStudent"));
const EditStudent = lazy(() => import("./pages/EditStudent"));

export default [
  {
    path: "/admin/users",
    component: UserManagement,
    permission: "users.management.view",
    pageType: "management",
    menuItem: {
      category: "Management",
      label: "User Management",
      icon: "Users",
    },
  },
  {
    path: "/admin/users/:id",
    component: UserHubPage,
    permission: "users.management.view",
    pageType: "entity",
    applicableTo: "both",
  },
  {
    path: "/admin/users/staff/add",
    component: AddStaff,
    permission: "users.management.insert",
    pageType: "entity",
  },
  {
    path: "/admin/users/staff/:id/edit",
    component: EditStaff,
    permission: "users.management.edit",
    pageType: "entity",
  },
  {
    path: "/admin/users/students/add",
    component: AddStudent,
    permission: "users.management.insert",
    pageType: "entity",
  },
  {
    path: "/admin/users/students/:id/edit",
    component: EditStudent,
    permission: "users.management.edit",
    pageType: "entity",
  },
];
