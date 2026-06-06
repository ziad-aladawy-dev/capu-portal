import { lazy } from "react";

const StaffDashboard = lazy(() => import("./pages/admin/StaffDashboard"));
const ServicesManagement = lazy(() => import("./pages/admin/ServicesManagement"));
const ServiceBuilder = lazy(() => import("./pages/admin/ServiceBuilder"));
const RequestsManagement = lazy(() => import("./pages/admin/RequestsManagement"));
const RequestReview = lazy(() => import("./pages/admin/RequestReview"));
const NotificationsCenter = lazy(() => import("./pages/admin/NotificationsCenter"));

export default [
  {
    path: "/admin/student-services",
    permission: "studentServices.dashboard.view",
    pageType: "management",
    applicableTo: "admin",
    isRedirect: true,
    redirectTo: "/admin/student-services/dashboard",
  },
  {
    path: "/admin/student-services/dashboard",
    component: StaffDashboard,
    permission: "studentServices.dashboard.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "Services Dashboard",
      icon: "LayoutDashboard",
    },
  },
  {
    path: "/admin/student-services/services",
    component: ServicesManagement,
    permission: "studentServices.services.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "Services",
      icon: "Package",
    },
  },
  {
    path: "/admin/student-services/services/create",
    component: ServiceBuilder,
    permission: "studentServices.services.insert",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/services/:id/edit",
    component: ServiceBuilder,
    permission: "studentServices.services.edit",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/requests",
    component: RequestsManagement,
    permission: "studentServices.requests.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "Requests",
      icon: "ClipboardList",
    },
  },
  {
    path: "/admin/student-services/requests/:id",
    component: RequestReview,
    permission: "studentServices.requests.review",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/notifications",
    component: NotificationsCenter,
    permission: "studentServices.notifications.view",
    pageType: "management",
  },
];
