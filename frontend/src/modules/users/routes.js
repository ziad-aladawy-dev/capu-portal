import { lazy } from "react";

<<<<<<< Updated upstream
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
=======
const UserManagement = lazy(() => import("./pages/UserManagement"));
const UserDetails = lazy(() => import("./pages/UserDetails"));
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
    component: UserDetails,
    permission: "users.management.view",
    pageType: "entity",
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
>>>>>>> Stashed changes
  },
];
