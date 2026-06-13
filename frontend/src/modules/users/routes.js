import { lazy } from "react";

const UserManagement = lazy(() => import("./pages/UserManagement"));

const AddStaff = lazy(() => import("./pages/AddStaff"));
const EditStaff = lazy(() => import("./pages/EditStaff"));
const AddStudent = lazy(() => import("./pages/AddStudent"));
const EditStudent = lazy(() => import("./pages/EditStudent"));
const UserDetails = lazy(() => import("./pages/UserDetails"));
const StaffAssignedWorkflowsPage = lazy(() => import("./pages/StaffAssignedWorkflowsPage"));

export default [
  {
    path: "/admin/users",
    component: UserManagement,
    permission: "users.users.view",
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
    permission: "users.users.view",
    pageType: "entity",
  },
  {
    path: "/admin/users/:id/assigned-workflows",
    component: StaffAssignedWorkflowsPage,
    permission: "student-services.requests.view",
    pageType: "entity",
  },

  {
    path: "/admin/users/staff/add",
    component: AddStaff,
    permission: "users.users.insert",
    pageType: "entity",
  },
  {
    path: "/admin/users/staff/:id/edit",
    component: EditStaff,
    permission: "users.users.editclose",
    pageType: "entity",
  },
  {
    path: "/admin/users/students/add",
    component: AddStudent,
    permission: "users.users.insert",
    pageType: "entity",
  },
  {
    path: "/admin/users/students/:id/edit",
    component: EditStudent,
    permission: "users.users.editclose",
    pageType: "entity",
  },
];
