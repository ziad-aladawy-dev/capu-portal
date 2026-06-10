import { lazy } from "react";

const AuditLogsPage = lazy(() => import("./pages/AuditLogsPage"));

export default [
  {
    path: "/admin/system/audit-logs",
    component: AuditLogsPage,
    permission: "system.audit-logs.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "Administration",
      label: "Audit Logs",
      icon: "ShieldCheck",
    },
  },
];
