import { lazy } from "react";
import { staffDirectoryConfig } from "../../core/components/directory/directoryConfigs";

const DirectoryPage = lazy(() => import("../../core/components/directory/DirectoryPage"));

export default [
  {
    path: "/admin/staff",
    component: DirectoryPage,
    props: { config: staffDirectoryConfig },
    permission: "users.users.view",
    pageType: "management",
    applicableTo: "staff",
    menuItem: {
      category: "People Management",
      label: "Staff Directory",
      icon: "Users",
    },
  },
];
