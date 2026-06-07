import { lazy } from "react";

const ScheduleSlotsPage = lazy(() => import("./pages/ScheduleSlotsPage"));

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
];
