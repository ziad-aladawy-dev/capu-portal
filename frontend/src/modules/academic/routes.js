import { lazy } from "react";

const AcademicDashboardPage = lazy(() => import("./pages/Dashboard/AcademicDashboardPage"));
const CourseCatalogPage = lazy(() => import("./pages/CourseCatalog/CourseCatalogPage"));
const CourseOfferingsPage = lazy(() => import("./pages/CourseOfferings/CourseOfferingsPage"));
const AcademicPlansPage = lazy(() => import("./pages/AcademicPlans/AcademicPlansPage"));
const ProgramsPage = lazy(() => import("./pages/Programs/ProgramsPage"));
const ScheduleSlotsPage = lazy(() => import("./pages/Schedule/ScheduleSlotsPage"));
const SchedulingMatrixPage = lazy(() => import("./pages/Schedule/SchedulingMatrixPage"));

// Unified Academic Suite. The category tab bar is rendered once by
// DashboardLayout (core/components/CategoryTabs) from menuItem metadata —
// pages no longer wrap themselves. Old module paths redirect below so
// bookmarks survive.
export default [
  {
    path: "/admin/academic/dashboard",
    component: AcademicDashboardPage,
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Overview",
      icon: "BarChart3",
    },
  },
  {
    path: "/admin/academic/courses",
    component: CourseCatalogPage,
    permission: "courses.courses.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Course Catalog",
      icon: "BookOpen",
    },
  },
  {
    path: "/admin/academic/offerings",
    component: CourseOfferingsPage,
    permission: "course-offerings.course-offerings.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Course Offerings",
      icon: "CalendarCheck",
    },
  },
  {
    path: "/admin/academic/plans",
    component: AcademicPlansPage,
    permission: "courses.academic-plans.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Academic Plans",
      icon: "ClipboardList",
    },
  },
  {
    path: "/admin/academic/programs",
    component: ProgramsPage,
    permission: "programs.programs.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Programs",
      icon: "GraduationCap",
    },
  },
  {
    path: "/admin/academic/schedule",
    component: ScheduleSlotsPage,
    permission: "schedule.schedule-slots.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Schedule",
      icon: "Clock",
    },
  },
  {
    path: "/admin/academic/scheduling-matrix",
    component: SchedulingMatrixPage,
    permission: "schedule.schedule-slots.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Academic",
      label: "Scheduling Matrix",
      icon: "CalendarRange",
    },
  },

  // ---- Legacy path redirects (old module layout) ----
  { path: "/admin/courses", isRedirect: true, redirectTo: "/admin/academic/courses", permission: "courses.courses.view" },
  { path: "/admin/academic/course-hub", isRedirect: true, redirectTo: "/admin/academic/courses", permission: "courses.courses.view" },
  { path: "/admin/academic-plans", isRedirect: true, redirectTo: "/admin/academic/plans", permission: "courses.academic-plans.view" },
  { path: "/admin/academic/course-offerings", isRedirect: true, redirectTo: "/admin/academic/offerings", permission: "course-offerings.course-offerings.view" },
  { path: "/admin/programs", isRedirect: true, redirectTo: "/admin/academic/programs", permission: "programs.programs.view" },
];
