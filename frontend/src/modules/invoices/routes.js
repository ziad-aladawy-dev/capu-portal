import { lazy } from "react";

const AdminFinanceDashboard = lazy(() => import("./pages/AdminFinanceDashboard"));
const StudentFinancialDetailsPage = lazy(() => import("./pages/StudentFinancialDetailsPage"));

export default [
  {
    path: "/admin/finance",
    component: AdminFinanceDashboard,
    permission: "payments.invoices.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Dashboard",
      icon: "LayoutDashboard",
    },
  },
  {
    path: "/admin/finance/student/:studentId",
    component: StudentFinancialDetailsPage,
    permission: "payments.invoices.view",
    pageType: "entity",
    applicableTo: "both",
  },
];
