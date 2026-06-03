import { lazy } from "react";

const InvoicesPage = lazy(() => import("./pages/InvoicesPage"));
const InvoiceDetailsPage = lazy(() => import("./pages/InvoiceDetailsPage"));
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
  {
    path: "/admin/invoices",
    component: InvoicesPage,
    permission: "payments.invoices.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Invoices",
      icon: "Receipt",
    },
  },
  {
    path: "/admin/invoices/:invoiceId",
    component: InvoiceDetailsPage,
    permission: "payments.invoices.view",
    pageType: "entity",
    applicableTo: "both",
  },
];
