import { lazy } from "react";

const FinanceDashboardPage = lazy(() => import("./pages/FinanceDashboardPage"));
const TreasuryHubPage = lazy(() => import("./pages/TreasuryHubPage"));
const BillingSetupPage = lazy(() => import("./pages/BillingSetupPage"));

export default [
  {
    path: "/admin/finance/dashboard",
    component: FinanceDashboardPage,
    permission: "dashboard.dashboard.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Overview",
      icon: "BarChart3",
    },
  },
  {
    path: "/admin/finance/treasury",
    component: TreasuryHubPage,
    permission: "payments.transactions.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Treasury Collections",
      icon: "HandCoins",
    },
  },
  {
    path: "/admin/finance/billing-setup",
    component: BillingSetupPage,
    permission: "payments.transactions.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Billing Setup",
      icon: "ReceiptText",
    },
  },
];
