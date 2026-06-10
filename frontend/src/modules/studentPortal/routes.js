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

const StudentServiceDetails = lazy(() => import("./pages/StudentServiceDetails"));
const RequestSubmission = lazy(() => import("./pages/RequestSubmission"));
const MyRequests = lazy(() => import("./pages/MyRequests"));
const StudentRequestDetails = lazy(() => import("./pages/StudentRequestDetails"));
const StudentNotifications = lazy(() => import("./pages/StudentNotifications"));

export default [
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
  },
];
