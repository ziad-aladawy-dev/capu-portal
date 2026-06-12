import { lazy } from "react";

const StudentDashboard = lazy(() => import("./pages/StudentDashboard"));
const StudentProfile = lazy(() => import("./pages/StudentProfile"));
const StudentCourses = lazy(() => import("./pages/StudentCourses"));
const CourseRegistration = lazy(() => import("./pages/CourseRegistration"));
const StudentGrades = lazy(() => import("./pages/StudentGrades"));
const StudentTranscript = lazy(() => import("./pages/StudentTranscript"));
const StudentSchedule = lazy(() => import("./pages/StudentSchedule"));
const StudentPayments = lazy(() => import("./pages/StudentPaymentsPage"));
const PaymentReturn = lazy(() => import("./pages/PaymentReturn"));

const StudentServicesCatalog = lazy(() => import("./pages/StudentServicesCatalog"));
const StudentServiceDetails = lazy(() => import("./pages/StudentServiceDetails"));
const RequestSubmission = lazy(() => import("./pages/RequestSubmission"));
const MyRequests = lazy(() => import("./pages/MyRequests"));
const StudentRequestDetails = lazy(() => import("./pages/StudentRequestDetails"));
const StudentNotifications = lazy(() => import("./pages/StudentNotifications"));

const PORTAL_ROUTES = [
  {
    path: "/student",
    permission: "student.dashboard.view",
    pageType: "management",
    applicableTo: "student",
    isRedirect: true,
    redirectTo: "/student/dashboard",
  },
  {
    path: "/student/dashboard",
    component: StudentDashboard,
    permission: "student.dashboard.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Dashboard",
      icon: "LayoutDashboard",
    },
    meta: { title: "Dashboard", showInBottomNav: true, bottomNavIndex: 0, group: "main", skeleton: "dashboard" },
  },
  {
    path: "/student/profile",
    component: StudentProfile,
    permission: "student.profile.view",
    pageType: "entity",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Profile",
      icon: "UserCog",
    },
    meta: { title: "Profile", group: "account", skeleton: "profile" },
  },
  {
    path: "/student/courses",
    component: StudentCourses,
    permission: "student.courses.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "My Courses",
      icon: "BookOpen",
    },
    meta: { title: "My Courses", showInBottomNav: true, bottomNavIndex: 1, group: "academic", skeleton: "cards" },
  },
  {
    path: "/student/courses/register",
    component: CourseRegistration,
    permission: "student.courses.register",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/student/grades",
    component: StudentGrades,
    permission: "student.grades.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "My Grades",
      icon: "FileText",
    },
    meta: { title: "My Grades", group: "academic", skeleton: "cards" },
  },
  {
    path: "/student/transcript",
    component: StudentTranscript,
    permission: "student.grades.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Transcript",
      icon: "GraduationCap",
    },
    meta: { title: "Transcript", group: "academic", skeleton: "document" },
  },
  {
    path: "/student/payments",
    component: StudentPayments,
    permission: "student.dashboard.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Payments & Fees",
      icon: "Receipt",
    },
    meta: { title: "Payments & Fees", group: "account", skeleton: "cards" },
  },
  {
    path: "/student/payments/return",
    component: PaymentReturn,
    permission: "student.dashboard.view",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/student/schedule",
    component: StudentSchedule,
    permission: "student.schedule.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Schedule",
      icon: "CalendarRange",
    },
    meta: { title: "Schedule", group: "academic", skeleton: "calendar" },
  },
  {
    path: "/student/services",
    component: StudentServicesCatalog,
    permission: "student.services.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Services",
      icon: "LayoutGrid",
    },
    meta: { title: "Services", showInBottomNav: true, bottomNavIndex: 2, group: "services", skeleton: "cards" },
  },
  {
    path: "/student/services/:id",
    component: StudentServiceDetails,
    permission: "student.services.view",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/student/services/:id/apply",
    component: RequestSubmission,
    permission: "student.services.request",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/student/requests",
    component: MyRequests,
    permission: "student.requests.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "My Requests",
      icon: "ClipboardList",
    },
    meta: { title: "My Requests", showInBottomNav: true, bottomNavIndex: 3, group: "services", skeleton: "list", badge: "requests" },
  },
  {
    path: "/student/requests/:id",
    component: StudentRequestDetails,
    permission: "student.requests.view",
    pageType: "entity",
    applicableTo: "student",
  },
  {
    path: "/student/notifications",
    component: StudentNotifications,
    permission: "student.dashboard.view",
    pageType: "management",
    applicableTo: "student",
    menuItem: {
      category: "Student",
      label: "Notifications",
      icon: "Bell",
    },
    meta: { title: "Notifications", group: "account", skeleton: "list", badge: "notifications" },
  },
];

export default PORTAL_ROUTES;

/**
 * menuItem labels are English phrases; translate via the same snake_case key
 * convention the admin sidebar uses ("My Courses" → "my_courses"), with "&"
 * normalized to "and" ("Payments & Fees" → "payments_and_fees").
 */
export function portalLabelKey(label) {
  return label.toLowerCase().replace(/&/g, "and").replace(/\s+/g, "_");
}

/** Routes that appear anywhere in portal navigation (have a menuItem + meta). */
export function getPortalNavRoutes() {
  return PORTAL_ROUTES.filter((r) => r.menuItem && r.meta);
}

/** The 4 primary bottom-nav tabs, ordered by bottomNavIndex. */
export function getBottomNavRoutes() {
  return getPortalNavRoutes()
    .filter((r) => r.meta.showInBottomNav)
    .sort((a, b) => a.meta.bottomNavIndex - b.meta.bottomNavIndex);
}

/** Everything else — shown in the "More" sheet on mobile. */
export function getMoreSheetRoutes() {
  return getPortalNavRoutes().filter((r) => !r.meta.showInBottomNav);
}

/** Drawer groups for desktop: academic / services / account. */
export function getDrawerGroups() {
  const groups = { main: [], academic: [], services: [], account: [] };
  for (const r of getPortalNavRoutes()) {
    (groups[r.meta.group] || groups.main).push(r);
  }
  return groups;
}
