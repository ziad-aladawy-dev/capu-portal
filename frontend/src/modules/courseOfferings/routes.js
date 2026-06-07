import { lazy } from "react";

const CourseOfferingsPage = lazy(() => import("./pages/CourseOfferingsPage"));

export default [
  {
    path: "/admin/academic/course-offerings",
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
];
