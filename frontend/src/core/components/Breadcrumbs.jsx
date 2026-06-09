import { useMemo } from "react";
import { Link, useLocation } from "react-router-dom";
import { Home, ChevronRight } from "lucide-react";
import "./breadcrumbs.css";

/**
 * Mapping of URL path segments to human-readable labels.
 * Extend as new routes are added.
 */
const SEGMENT_LABELS = {
  admin: "Home",
  dashboard: "Dashboard",
  students: "Students",
  staff: "Staff",
  users: "Users",
  courses: "Courses",

  roles: "Roles",
  permissions: "Permissions",
  authorization: "Authorization Matrix",
  notifications: "Notifications",
  "university-structure": "University Structure",
  "academic-years": "Academic Years",
  "academic-plans": "Academic Plans",
  academic: "Academic",
  "course-offerings": "Course Offerings",
  schedule: "Schedule",
  settings: "Settings",
  "audit-log": "Audit Log",
  "system-health": "System Health",
  system: "System",
  "audit-logs": "Audit Logs",
  workflows: "Workflows",
  "profile-records": "Profile Records",
  "student-information": "Student Information",
  programs: "Programs",
  sync: "SIS Integration",
  transactions: "Transactions",
  finance: "Finance",
  "add-student": "Add Student",
  "add-staff": "Add Staff",
  "edit-student": "Edit Student",
  "edit-staff": "Edit Staff",
  add: "Add New",
  create: "Create",
};

/**
 * Check if a segment looks like an ID (UUID or hex string)
 */
function isIdSegment(segment) {
  return /^[0-9a-f]{8}(-[0-9a-f]{4}){0,3}(-[0-9a-f]{4,12})?$/i.test(segment);
}

function Breadcrumbs() {
  const location = useLocation();

  const crumbs = useMemo(() => {
    const segments = location.pathname.split("/").filter(Boolean);
    if (segments.length <= 1) return []; // Don't show for root paths

    const items = [];
    let pathAccum = "";

    for (let i = 0; i < segments.length; i++) {
      const seg = segments[i];
      pathAccum += `/${seg}`;
      const isCurrent = i === segments.length - 1;

      // ID segments are dynamic entities, not pages — never link
      if (isIdSegment(seg)) {
        items.push({
          label: "Detail",
          path: pathAccum,
          isCurrent,
          isLink: false,
        });
        continue;
      }

      const label = SEGMENT_LABELS[seg] || seg.charAt(0).toUpperCase() + seg.slice(1).replace(/-/g, " ");

      // Don't link category segments that don't resolve to real pages:
      // 1. Segments before a nested ID with more segments after it
      //    (e.g., `staff` in /admin/users/staff/<uuid>/edit)
      // 2. staff/students sub-categories under /admin/users/
      //    (e.g., `/admin/users/staff` or `/admin/users/students` are not real routes)
      const nextSeg = segments[i + 1];
      const hasNestedId = nextSeg && isIdSegment(nextSeg) && i + 2 < segments.length;
      const isSubCategory = i > 0 && segments[i - 1] === "users" && (seg === "staff" || seg === "students") && !isCurrent;

      items.push({
        label,
        path: pathAccum,
        isCurrent,
        isHome: seg === "admin",
        isLink: !hasNestedId && !isSubCategory,
      });
    }

    return items;
  }, [location.pathname]);

  if (crumbs.length <= 1) return null;

  return (
    <nav className="breadcrumb-bar" aria-label="Breadcrumb">
      {crumbs.map((crumb, idx) => (
        <span key={crumb.path} style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          {idx > 0 && (
            <ChevronRight size={11} className="breadcrumb-separator" />
          )}
          {crumb.isCurrent || !crumb.isLink ? (
            <span className={`breadcrumb-item${crumb.isCurrent ? ' is-current' : ''}`}>
              {crumb.label}
            </span>
          ) : (
            <Link to={crumb.path} className="breadcrumb-item">
              {crumb.isHome && <Home size={12} className="breadcrumb-home-icon" />}
              {crumb.label}
            </Link>
          )}
        </span>
      ))}
    </nav>
  );
}

export default Breadcrumbs;
