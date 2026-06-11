import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  BookOpen, CalendarCheck, ClipboardList, GraduationCap, Clock, CalendarRange,
} from "lucide-react";
import { usePermission } from "../../../core/auth/usePermission";
import styles from "./AcademicShell.module.css";

// The route registry renders flat routes (no nested <Outlet/> layouts), so the
// academic sub-navigation is a shell component each academic page wraps.
const TABS = [
  { slug: "courses", labelKey: "nav.courses", icon: BookOpen, resource: "courses.courses" },
  { slug: "offerings", labelKey: "nav.offerings", icon: CalendarCheck, resource: "course-offerings.course-offerings" },
  { slug: "plans", labelKey: "nav.plans", icon: ClipboardList, resource: "courses.academic-plans" },
  { slug: "programs", labelKey: "nav.programs", icon: GraduationCap, resource: "programs.programs" },
  { slug: "schedule", labelKey: "nav.schedule", icon: Clock, resource: "schedule.schedule-slots" },
  { slug: "scheduling-matrix", labelKey: "nav.matrix", icon: CalendarRange, resource: "schedule.schedule-slots" },
];

export default function AcademicShell({ children }) {
  const { t } = useTranslation("academic");
  const { can } = usePermission();

  return (
    <div className={styles.shell}>
      <nav className={styles.tabs} aria-label={t("nav.title")}>
        {TABS.filter((tab) => can(tab.resource, 1)).map((tab) => {
          const Icon = tab.icon;
          return (
            <NavLink
              key={tab.slug}
              to={`/admin/academic/${tab.slug}`}
              className={({ isActive }) => (isActive ? styles.tabActive : styles.tab)}
            >
              <Icon size={14} />
              {t(tab.labelKey)}
            </NavLink>
          );
        })}
      </nav>
      <div className={styles.content}>{children}</div>
    </div>
  );
}
