import { createElement } from "react";
import { Navigate } from "react-router-dom";
import RouteGuard from "../auth/RouteGuard";
import { getAllRoutes } from "../manifests/manifestLoader";

import AdminDashboard from "../../modules/admin/pages/AdminDashboard";
import UniversityStructurePage from "../../modules/university/pages/UniversityStructurePage";
import UserManagement from "../../modules/users/pages/UserManagement";
import AddStudent from "../../modules/users/pages/AddStudent";
import EditStudent from "../../modules/users/pages/EditStudent";
import AddStaff from "../../modules/users/pages/AddStaff";
import EditStaff from "../../modules/users/pages/EditStaff";
import UserDetails from "../../modules/users/pages/UserDetails";
import StaffDirectory from "../../modules/staff/pages/StaffDirectory";
import StudentDirectory from "../../modules/students/pages/StudentDirectory";
import PermissionsPage from "../../modules/permissions/pages/PermissionsPage";
import RolesPage from "../../modules/permissions/pages/RolesPage";
import AcademicCalendarPage from "../../modules/academic/pages/AcademicCalendarPage";
import NotificationsPage from "../../modules/notifications/pages/NotificationsPage";

const COMPONENT_MAP = {
  AdminDashboard,
  UniversityStructurePage,
  UserManagement,
  AddStudent,
  EditStudent,
  AddStaff,
  EditStaff,
  UserDetails,
  StaffDirectory,
  StudentDirectory,
  PermissionsPage,
  RolesPage,
  AcademicCalendarPage,
  NotificationsPage,
};

function Placeholder({ title = "Page" }) {
  return createElement("h1", null, title);
}
COMPONENT_MAP.Placeholder = Placeholder;

function getComponent(componentName) {
  const Component = COMPONENT_MAP[componentName];

  if (!Component) {
    console.warn(`Component "${componentName}" not found in registry`);
    return function MissingComponent() {
      return createElement(
        "div",
        { style: { padding: 24, color: "#c9a84c" } },
        `Module component "${componentName}" not loaded.`
      );
    };
  }

  return Component;
}

export function buildProtectedRoutes() {
  const manifestRoutes = getAllRoutes();
  const routes = [];

  for (const r of manifestRoutes) {
    if (r.component === "Navigate") {
      routes.push({
        path: r.path,
        element: createElement(Navigate, { to: r.props.to, replace: true }),
        permission: r.permission,
      });
      continue;
    }

    const Component = getComponent(r.component);
    const props = r.props || {};

    routes.push({
      path: r.path,
      element: createElement(
        RouteGuard,
        { resource: r.permission, minLevel: 1 },
        createElement(Component, props)
      ),
      permission: r.permission,
    });
  }

  return routes;
}
