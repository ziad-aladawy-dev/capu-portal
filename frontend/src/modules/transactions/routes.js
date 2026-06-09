import { lazy } from "react";

const TransactionsPage = lazy(() => import("./pages/TransactionsPage"));

export default [
  {
    path: "/admin/finance/transactions",
    component: TransactionsPage,
    permission: "payments.transactions.view",
    pageType: "management",
    applicableTo: "both",
    menuItem: {
      category: "Finance",
      label: "Transactions",
      icon: "Receipt",
    },
  },
];
