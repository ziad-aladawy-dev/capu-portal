import {
  LayoutDashboard,
  Building2,
  Users,
  UserPlus,
  Shield,
  Plug,
  GraduationCap,
  UserCog,
  BookOpen,
  ClipboardList,
  Receipt,
  Bell,
  FolderTree,
  FileText,
  Home,
  BarChart3,
  Calendar,
  User,
  CalendarRange,
  CalendarCheck,
  Clock,
  RefreshCw,
  ShieldCheck,
  Package,
  GitBranch,
  HandCoins,
  ReceiptText,
  LayoutGrid,
} from "lucide-react";
import { getGroupedMenuItems } from "../router/routeRegistry";

const ICON_MAP = {
  LayoutDashboard,
  Building2,
  Users,
  UserPlus,
  Shield,
  Plug,
  GraduationCap,
  UserCog,
  BookOpen,
  ClipboardList,
  Receipt,
  Bell,
  FolderTree,
  FileText,
  Home,
  BarChart3,
  Calendar,
  User,
  CalendarRange,
  CalendarCheck,
  Clock,
  RefreshCw,
  ShieldCheck,
  Package,
  GitBranch,
  HandCoins,
  ReceiptText,
  LayoutGrid,
};

const CATEGORY_ICONS = {
  Overview: LayoutDashboard,
  Administration: Building2,
  "People Management": Users,
  "Security & Access": Shield,
  Academic: BookOpen,
  "Student Information": FileText,
  Finance: Receipt,
  Student: GraduationCap,
};

function resolveIcon(iconName) {
  return ICON_MAP[iconName] || LayoutDashboard;
}

export function buildMenu(canAccess) {
  const grouped = getGroupedMenuItems();
  const menu = [];

  for (const [category, items] of Object.entries(grouped)) {
    const filteredItems = items.filter((item) => {
      if (!item.permission) return true;
      return canAccess(item.permission, 1);
    });

    if (filteredItems.length === 0) continue;

    menu.push({
      category,
      icon: CATEGORY_ICONS[category] || Building2,
      items: filteredItems.map((item) => ({
        label: item.label,
        path: item.path,
        icon: resolveIcon(item.icon),
      })),
    });
  }

  return menu;
}

export function getCategoryIcon(category) {
  return CATEGORY_ICONS[category] || Building2;
}
