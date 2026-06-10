import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import {
  Search, GraduationCap, Users, BookOpen,
  FileText, ArrowRight, Clock, X,
} from "lucide-react";
import * as studentService from "../services/studentService";
import * as staffService from "../services/staffService";
import * as courseService from "../services/courseService";
import "./commandPalette.css";

const RECENT_KEY = "capu_cmd_recent";
const MAX_RECENT = 5;

const QUICK_PAGES = [
  { title: "Dashboard", path: "/admin/dashboard", type: "page" },
  { title: "Student Directory", path: "/admin/students", type: "page" },
  { title: "Staff Directory", path: "/admin/staff", type: "page" },
  { title: "Course Catalog", path: "/admin/courses", type: "page" },
  { title: "Academic Plans", path: "/admin/academic-plans", type: "page" },
  { title: "Programs", path: "/admin/programs", type: "page" },

  { title: "Roles", path: "/admin/roles", type: "page" },
  { title: "Permissions", path: "/admin/permissions", type: "page" },
  { title: "Permission Tree", path: "/admin/authorization", type: "page" },
  { title: "University Structure", path: "/admin/university", type: "page" },
  { title: "Academic Years", path: "/admin/academic-years", type: "page" },
  { title: "Course Offerings", path: "/admin/academic/course-offerings", type: "page" },
  { title: "Schedule", path: "/admin/academic/schedule", type: "page" },
  { title: "Student Services", path: "/admin/student-services/dashboard", type: "page" },
  { title: "Notifications", path: "/admin/notifications", type: "page" },
  { title: "Finance Dashboard", path: "/admin/finance", type: "page" },
  { title: "Transactions", path: "/admin/finance/transactions", type: "page" },
  { title: "Workflows", path: "/admin/student-services/workflows", type: "page" },
  { title: "Profile Records", path: "/admin/student-information/profile-records", type: "page" },
  { title: "SIS Integration", path: "/admin/sync", type: "page" },
  { title: "Audit Logs", path: "/admin/system/audit-logs", type: "page" },
];

const CATEGORY_ICONS = {
  student: GraduationCap,
  staff: Users,
  course: BookOpen,

  page: FileText,
};

const CATEGORY_LABELS = {
  student: "Students",
  staff: "Staff",
  course: "Courses",

  page: "Pages",
};

function loadRecent() {
  try {
    const raw = localStorage.getItem(RECENT_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch { return []; }
}

function saveRecent(list) {
  localStorage.setItem(RECENT_KEY, JSON.stringify(list.slice(0, MAX_RECENT)));
}

function CommandPalette({ onClose }) {
  const navigate = useNavigate();
  const inputRef = useRef(null);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [focusIndex, setFocusIndex] = useState(0);
  const [recentItems, setRecentItems] = useState(loadRecent);
  const debounceRef = useRef(null);
  const bodyRef = useRef(null);

  // Focus input on open
  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  // Close on Escape
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  // Search logic
  const doSearch = useCallback(async (q) => {
    if (!q.trim()) {
      setResults([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    const all = [];

    // Search pages first (instant, no API)
    const qLower = q.toLowerCase();
    QUICK_PAGES.forEach((p) => {
      if (p.title.toLowerCase().includes(qLower)) {
        all.push({ ...p, subtitle: p.path });
      }
    });

    // Search entities from API in parallel
    try {
      const [students, staff, courses] = await Promise.allSettled([
        studentService.searchStudents({ search: q, page: 1, pageSize: 5 }),
        staffService.searchStaff({ search: q, page: 1, pageSize: 5 }),
        courseService.fetchActiveCourses(),
      ]);

      if (students.status === "fulfilled" && students.value?.items) {
        students.value.items.forEach((s) => {
          all.push({
            title: s.name,
            subtitle: s.studentCode || s.email || "",
            path: `/admin/users/${s.id}`,
            type: "student",
            id: s.id,
          });
        });
      }

      if (staff.status === "fulfilled" && staff.value?.items) {
        staff.value.items.forEach((s) => {
          all.push({
            title: s.name,
            subtitle: s.employeeCode || s.email || "",
            path: `/admin/users/${s.id}`,
            type: "staff",
            id: s.id,
          });
        });
      }

      if (courses.status === "fulfilled" && Array.isArray(courses.value)) {
        courses.value
          .filter((c) =>
            c.code.toLowerCase().includes(qLower) ||
            c.title.toLowerCase().includes(qLower)
          )
          .slice(0, 5)
          .forEach((c) => {
            all.push({
              title: c.title,
              subtitle: c.code,
              path: `/admin/courses`,
              type: "course",
              id: c.id,
            });
          });
      }

    } catch {
      // Silently fail — show what we have
    }

    setResults(all);
    setFocusIndex(0);
    setLoading(false);
  }, []);

  // Debounced search
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => doSearch(query), 250);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, doSearch]);

  // Group results by type
  const grouped = useMemo(() => {
    const groups = {};
    for (const item of results) {
      const cat = item.type || "page";
      if (!groups[cat]) groups[cat] = [];
      groups[cat].push(item);
    }
    return groups;
  }, [results]);

  // Flat list for keyboard nav
  const flatItems = useMemo(() => {
    const flat = [];
    for (const cat of Object.keys(grouped)) {
      for (const item of grouped[cat]) {
        flat.push(item);
      }
    }
    return flat;
  }, [grouped]);

  const displayItems = query.trim() ? flatItems : recentItems;

  const handleSelect = useCallback((item) => {
    // Add to recent
    const updated = [
      { title: item.title, subtitle: item.subtitle, path: item.path, type: item.type },
      ...recentItems.filter((r) => r.path !== item.path),
    ].slice(0, MAX_RECENT);
    setRecentItems(updated);
    saveRecent(updated);

    navigate(item.path);
    onClose();
  }, [navigate, onClose, recentItems]);

  const handleKeyDown = (e) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIndex((prev) => Math.min(prev + 1, displayItems.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIndex((prev) => Math.max(prev - 1, 0));
    } else if (e.key === "Enter" && displayItems[focusIndex]) {
      e.preventDefault();
      handleSelect(displayItems[focusIndex]);
    }
  };

  // Scroll focused item into view
  useEffect(() => {
    const el = bodyRef.current?.querySelector(`.cmd-item.is-focused`);
    if (el) el.scrollIntoView({ block: "nearest" });
  }, [focusIndex]);

  const clearRecent = () => {
    setRecentItems([]);
    saveRecent([]);
  };

  return (
    <div className="cmd-palette-overlay" onClick={onClose}>
      <div className="cmd-palette" onClick={(e) => e.stopPropagation()}>
        {/* Search input */}
        <div className="cmd-palette-input-wrap">
          <Search size={16} />
          <input
            ref={inputRef}
            className="cmd-palette-input"
            type="text"
            placeholder="Search students, staff, courses, pages…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          <span className="cmd-palette-kbd">ESC</span>
        </div>

        {/* Results body */}
        <div className="cmd-palette-body" ref={bodyRef}>
          {loading && (
            <div className="cmd-palette-loading">
              <Search size={14} />
              Searching…
            </div>
          )}

          {!loading && query.trim() && results.length === 0 && (
            <div className="cmd-palette-empty">
              <Search size={24} />
              <span>No results for "{query}"</span>
            </div>
          )}

          {!loading && query.trim() && Object.keys(grouped).map((cat) => {
            const Icon = CATEGORY_ICONS[cat] || FileText;
            return (
              <div key={cat}>
                <div className="cmd-group-label">
                  <Icon size={11} />
                  {CATEGORY_LABELS[cat] || cat}
                </div>
                {grouped[cat].map((item, idx) => {
                  const globalIdx = flatItems.indexOf(item);
                  const ItemIcon = CATEGORY_ICONS[item.type] || FileText;
                  return (
                    <button
                      key={`${item.path}-${idx}`}
                      className={`cmd-item ${globalIdx === focusIndex ? "is-focused" : ""}`}
                      onClick={() => handleSelect(item)}
                      onMouseEnter={() => setFocusIndex(globalIdx)}
                    >
                      <div className={`cmd-item-icon type-${item.type}`}>
                        <ItemIcon size={15} />
                      </div>
                      <div className="cmd-item-text">
                        <span className="cmd-item-title">{item.title}</span>
                        {item.subtitle && <span className="cmd-item-subtitle">{item.subtitle}</span>}
                      </div>
                      <span className={`cmd-item-badge type-${item.type}`}>
                        {CATEGORY_LABELS[item.type] || "Page"}
                      </span>
                      <ArrowRight size={12} style={{ color: "rgba(255,255,255,0.15)" }} />
                    </button>
                  );
                })}
              </div>
            );
          })}

          {/* Recent searches (when no query) */}
          {!query.trim() && !loading && recentItems.length > 0 && (
            <>
              <div className="cmd-recent-header">
                <span><Clock size={10} style={{ marginRight: 4, verticalAlign: "middle" }} /> Recent</span>
                <button className="cmd-recent-clear" onClick={clearRecent}>Clear</button>
              </div>
              {recentItems.map((item, idx) => {
                const ItemIcon = CATEGORY_ICONS[item.type] || FileText;
                return (
                  <button
                    key={`recent-${idx}`}
                    className={`cmd-item ${idx === focusIndex ? "is-focused" : ""}`}
                    onClick={() => handleSelect(item)}
                    onMouseEnter={() => setFocusIndex(idx)}
                  >
                    <div className={`cmd-item-icon type-${item.type || "page"}`}>
                      <ItemIcon size={15} />
                    </div>
                    <div className="cmd-item-text">
                      <span className="cmd-item-title">{item.title}</span>
                      {item.subtitle && <span className="cmd-item-subtitle">{item.subtitle}</span>}
                    </div>
                    <ArrowRight size={12} style={{ color: "rgba(255,255,255,0.15)" }} />
                  </button>
                );
              })}
            </>
          )}

          {/* Blank state — no recent, no query */}
          {!query.trim() && !loading && recentItems.length === 0 && (
            <div className="cmd-palette-empty">
              <Search size={24} />
              <span>Start typing to search…</span>
            </div>
          )}
        </div>

        {/* Footer hints */}
        <div className="cmd-palette-footer">
          <span><kbd>↑</kbd> <kbd>↓</kbd> Navigate</span>
          <span><kbd>↵</kbd> Open</span>
          <span><kbd>ESC</kbd> Close</span>
        </div>
      </div>
    </div>
  );
}

export default CommandPalette;
