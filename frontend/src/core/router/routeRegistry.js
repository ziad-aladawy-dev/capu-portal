import { createElement, Suspense } from "react";
import { Navigate } from "react-router-dom";
import RouteGuard from "../auth/RouteGuard";

import adminRoutes from "../../modules/admin/routes";
import universityRoutes from "../../modules/university/routes";
import usersRoutes from "../../modules/users/routes";
import staffRoutes from "../../modules/staff/routes";
import studentsRoutes from "../../modules/students/routes";
import permissionsRoutes from "../../modules/permissions/routes";
import coursesRoutes from "../../modules/courses/routes";
import academicPlansRoutes from "../../modules/academicPlans/routes";
import academicYearsRoutes from "../../modules/academicYears/routes";
import studentPortalRoutes from "../../modules/studentPortal/routes";
import studentServicesRoutes from "../../modules/studentServices/routes";

import notificationsRoutes from "../../modules/notifications/routes";
import studentProfileRecordsRoutes from "../../modules/studentProfileRecords/routes";
import authorizationRoutes from "../../modules/authorization/routes";
import courseOfferingsRoutes from "../../modules/courseOfferings/routes";
import scheduleRoutes from "../../modules/schedule/routes";
import invoicesRoutes from "../../modules/invoices/routes";

const ALL_ROUTES = [
  ...adminRoutes,
  ...universityRoutes,
  ...usersRoutes,
  ...staffRoutes,
  ...studentsRoutes,
  ...permissionsRoutes,
  ...coursesRoutes,
  ...academicPlansRoutes,
  ...academicYearsRoutes,
  ...studentPortalRoutes,
  ...studentServicesRoutes,

  ...notificationsRoutes,
  ...studentProfileRecordsRoutes,
  ...authorizationRoutes,
  ...courseOfferingsRoutes,
  ...scheduleRoutes,
  ...invoicesRoutes,
];

const LOADING_FALLBACK = createElement(
  "div",
  {
    style: {
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      padding: 60,
      fontFamily: '"Outfit", sans-serif',
      color: "#9ca3af",
      fontSize: 13,
    },
  },
  "Loading..."
);

function wrapLazy(Component) {
  return function LazyWrapper(props) {
    return createElement(Suspense, { fallback: LOADING_FALLBACK },
      createElement(Component, props)
    );
  };
}

export function buildProtectedRoutes() {
  const routes = [];

  for (const r of ALL_ROUTES) {
    if (r.isRedirect) {
      routes.push({
        path: r.path,
        element: createElement(Navigate, { to: r.redirectTo, replace: true }),
        permission: r.permission,
      });
      continue;
    }

    const Component = wrapLazy(r.component);
    const guardProps = r.permission
      ? { resource: r.permission, minLevel: 1 }
      : { resource: null, minLevel: 0 };

    routes.push({
      path: r.path,
      element: createElement(
        RouteGuard,
        guardProps,
        createElement(Component, r.props || {})
      ),
      permission: r.permission,
    });
  }

  return routes;
}

export function getCurrentRouteInfo(pathname) {
  for (const route of ALL_ROUTES) {
    const pattern = route.path
      .replace(/\/:id\b/g, "/[^/]+")
      .replace(/\/:studentId\b/g, "/[^/]+")

    const regex = new RegExp(`^${pattern}$`);
    if (regex.test(pathname)) {
      return route;
    }
  }
  return null;
}

export function getGroupedMenuItems() {
  const items = ALL_ROUTES
    .filter((r) => r.menuItem)
    .map((r) => ({
      path: r.path,
      label: r.menuItem.label,
      icon: r.menuItem.icon,
      category: r.menuItem.category,
      permission: r.permission,
    }));

  const grouped = {};
  for (const item of items) {
    if (!grouped[item.category]) {
      grouped[item.category] = [];
    }
    grouped[item.category].push(item);
  }
  return grouped;
}
