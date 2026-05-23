import { createElement, lazy, Suspense } from "react";
import { Navigate } from "react-router-dom";
import RouteGuard from "../auth/RouteGuard";
import { getAllRoutes } from "../manifests/manifestLoader";

function lazyLoad(importFn) {
  const LazyComponent = lazy(importFn);
  return function WrappedLazy(props) {
    return createElement(
      Suspense,
      { fallback: createElement("div", {
        style: {
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: 60,
          fontFamily: '"Outfit", sans-serif',
          color: "#9ca3af",
          fontSize: 13,
        }
      }, "Loading...") },
      createElement(LazyComponent, props)
    );
  };
}

const COMPONENT_MAP = {
  AdminDashboard: lazyLoad(() => import("../../modules/admin/pages/AdminDashboard")),
  UniversityStructurePage: lazyLoad(() => import("../../modules/university/pages/UniversityStructurePage")),
  UserManagement: lazyLoad(() => import("../../modules/users/pages/UserManagement")),
  AddStudent: lazyLoad(() => import("../../modules/users/pages/AddStudent")),
  EditStudent: lazyLoad(() => import("../../modules/users/pages/EditStudent")),
  AddStaff: lazyLoad(() => import("../../modules/users/pages/AddStaff")),
  EditStaff: lazyLoad(() => import("../../modules/users/pages/EditStaff")),
  UserDetails: lazyLoad(() => import("../../modules/users/pages/UserDetails")),
  UserHubPage: lazyLoad(() => import("../../modules/users/pages/UserHubPage")),
  StaffDirectory: lazyLoad(() => import("../../modules/staff/pages/StaffDirectory")),
  StudentDirectory: lazyLoad(() => import("../../modules/students/pages/StudentDirectory")),
  PermissionsPage: lazyLoad(() => import("../../modules/permissions/pages/PermissionsPage")),
  RolesPage: lazyLoad(() => import("../../modules/permissions/pages/RolesPage")),
  CoursesPage: lazyLoad(() => import("../../modules/courses/pages/CoursesPage")),
  AcademicPlansPage: lazyLoad(() => import("../../modules/academicPlans/pages/AcademicPlansPage")),
  AcademicYearsPage: lazyLoad(() => import("../../modules/academicYears/pages/AcademicYearsPage")),
  StudentDashboard: lazyLoad(() => import("../../modules/studentPortal/pages/StudentDashboard")),
  StudentProfile: lazyLoad(() => import("../../modules/studentPortal/pages/StudentProfile")),
  StudentCourses: lazyLoad(() => import("../../modules/studentPortal/pages/StudentCourses")),
  CourseRegistration: lazyLoad(() => import("../../modules/studentPortal/pages/CourseRegistration")),
  StudentGrades: lazyLoad(() => import("../../modules/studentPortal/pages/StudentGrades")),
  StudentSchedule: lazyLoad(() => import("../../modules/studentPortal/pages/StudentSchedule")),
  InvoicesPage: lazyLoad(() => import("../../modules/invoices/pages/InvoicesPage")),
  InvoiceDetailsPage: lazyLoad(() => import("../../modules/invoices/pages/InvoiceDetailsPage")),
  NotificationsPage: lazyLoad(() => import("../../modules/notifications/pages/NotificationsPage")),
  StudentProfileRecordsPage: lazyLoad(() => import("../../modules/studentProfileRecords/pages/StudentProfileRecordsPage")),
  PermissionTreePage: lazyLoad(() => import("../../modules/authorization/pages/PermissionTreePage")),
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

    const guardProps = r.permission
      ? { resource: r.permission, minLevel: 1 }
      : { resource: null, minLevel: 0 };

    routes.push({
      path: r.path,
      element: createElement(
        RouteGuard,
        guardProps,
        createElement(Component, props)
      ),
      permission: r.permission,
    });
  }

  return routes;
}
