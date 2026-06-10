import { lazy } from "react";

const ScheduleSlotsPage = lazy(() => import("./pages/ScheduleSlotsPage"));
const SchedulingMatrixPage = lazy(() => import("./pages/SchedulingMatrixPage"));

export default [
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
];
