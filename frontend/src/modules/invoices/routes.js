import { lazy } from "react";

const InvoicesPage = lazy(() => import("./pages/InvoicesPage"));
const InvoiceDetailsPage = lazy(() => import("./pages/InvoiceDetailsPage"));

export default [
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
