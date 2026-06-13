import { lazy } from "react";

const StaffDashboard = lazy(() => import("./pages/admin/StaffDashboard"));
const ServicesManagement = lazy(() => import("./pages/admin/ServicesManagement"));
const ServiceDetailsPage = lazy(() => import("./pages/admin/ServiceDetailsPage"));
const ServiceBuilder = lazy(() => import("./pages/admin/ServiceBuilder"));
const RequestsManagement = lazy(() => import("./pages/admin/RequestsManagement"));
const RequestReview = lazy(() => import("./pages/admin/RequestReview"));
const NotificationsCenter = lazy(() => import("./pages/admin/NotificationsCenter"));
const MyAssignedRequests = lazy(() => import("./pages/admin/MyAssignedRequests"));

const routes = [
  {
    path: "/admin/student-services",
    permission: "student-services.services.view",
    pageType: "management",
    applicableTo: "admin",
    isRedirect: true,
    redirectTo: "/admin/student-services/dashboard",
  },
  {
    path: "/admin/student-services/dashboard",
    component: StaffDashboard,
    permission: "student-services.services.view",
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
    permission: "student-services.services.view",
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
    permission: "student-services.services.insert",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/services/:id/edit",
    component: ServiceBuilder,
    permission: "student-services.services.editclose",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/services/:id",
    component: ServiceDetailsPage,
    permission: "student-services.services.view",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/requests",
    component: RequestsManagement,
    permission: "student-services.requests.view",
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
    permission: "student-services.requests.open",
    pageType: "entity",
  },
  {
    path: "/admin/student-services/assigned-to-me",
    component: MyAssignedRequests,
    permission: "student-services.requests.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "My Queue",
      icon: "UserCheck",
    },
  },
  {
    path: "/admin/student-services/notifications",
    component: NotificationsCenter,
    permission: "student-services.notifications.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "Notifications",
      icon: "Bell",
    },
  },
];

export default routes;