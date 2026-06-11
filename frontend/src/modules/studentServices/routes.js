import { lazy } from "react";

const StaffDashboard = lazy(() => import("./pages/admin/StaffDashboard"));
const ServicesManagement = lazy(() => import("./pages/admin/ServicesManagement"));
const ServiceDetailsPage = lazy(() => import("./pages/admin/ServiceDetailsPage"));
const ServiceBuilder = lazy(() => import("./pages/admin/ServiceBuilder"));
const RequestsManagement = lazy(() => import("./pages/admin/RequestsManagement"));
const RequestReview = lazy(() => import("./pages/admin/RequestReview"));
const NotificationsCenter = lazy(() => import("./pages/admin/NotificationsCenter"));

const StudentDashboard = lazy(() => import("./pages/student/StudentDashboard"));
const MyRequests = lazy(() => import("./pages/student/MyRequests"));
const RequestSubmission = lazy(() => import("./pages/student/RequestSubmission"));
const ServiceDetails = lazy(() => import("./pages/student/ServiceDetails"));
const StudentNotificationsCenter = lazy(() => import("./pages/student/StudentNotificationsCenter"));
const StudentRequestDetails = lazy(() => import("./pages/student/StudentRequestDetails"));

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

  {
    path: "/student/student-services",
    permission: "student-services.student.dashboard.view",
    pageType: "management",
    applicableTo: "student",
    isRedirect: true,
    redirectTo: "/student/student-services/dashboard",
  },
  {
    path: "/student/student-services/dashboard",
    component: StudentDashboard,
    permission: "student-services.student.dashboard.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "My Dashboard",
      icon: "LayoutDashboard",
    },
  },
  {
    path: "/student/student-services/requests",
    component: MyRequests,
    permission: "student-services.student.request.view.own",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "My Requests",
      icon: "ClipboardList",
    },
  },
  {
    path: "/student/student-services/requests/create",
    component: RequestSubmission,
    permission: "student-services.student.request.create",
    pageType: "entity",
  },
  {
    path: "/student/student-services/requests/:id",
    component: StudentRequestDetails,
    permission: "student-services.student.request.view.own",
    pageType: "entity",
  },
  {
    path: "/student/student-services/services/:id",
    component: ServiceDetails,
    permission: "student-services.student.service.view",
    pageType: "entity",
  },
  {
    path: "/student/student-services/notifications",
    component: StudentNotificationsCenter,
    permission: "student-services.student.notifications.view",
    pageType: "management",
    menuItem: {
      category: "Student Services",
      label: "Notifications",
      icon: "Bell",
    },
  },
];

export default routes;