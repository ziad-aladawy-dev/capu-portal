import { lazy } from "react";

const StudentDashboard = lazy(() => import("./pages/StudentDashboard"));
const StudentProfile = lazy(() => import("./pages/StudentProfile"));
const StudentCourses = lazy(() => import("./pages/StudentCourses"));
const CourseRegistration = lazy(() => import("./pages/CourseRegistration"));
const StudentGrades = lazy(() => import("./pages/StudentGrades"));
const StudentSchedule = lazy(() => import("./pages/StudentSchedule"));

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
];
