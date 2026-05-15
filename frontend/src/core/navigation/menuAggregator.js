import {
  LayoutDashboard,
  Building2,
  Users,
  UserPlus,
  Shield,
  Plug,
  GraduationCap,
  UserCog,
} from "lucide-react";
import { getGroupedMenuItems } from "../manifests/manifestLoader";

const ICON_MAP = {
  LayoutDashboard,
  Building2,
  Users,
  UserPlus,
  Shield,
  Plug,
  GraduationCap,
  UserCog,
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
      items: filteredItems.map((item) => ({
        label: item.label,
        path: item.path,
        icon: resolveIcon(item.icon),
      })),
    });
  }

  return menu;
}
