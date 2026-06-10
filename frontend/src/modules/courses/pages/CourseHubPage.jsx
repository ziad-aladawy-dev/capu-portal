import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  BookOpen, Search, X, Library, CalendarDays, AlertTriangle, RefreshCw,
} from "lucide-react";
import DataTable from "../../../core/components/DataTable";
import StatusBadge from "../../../core/components/StatusBadge";
import EntityHoverCard from "../../../core/components/EntityHoverCard";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { useCourses } from "../../../core/query/useCourses";
import { COURSE_CATEGORIES, getCourseCategoryLabel } from "../../../core/services/courseService";
import CourseDetailDrawer from "../components/CourseDetailDrawer";
import "../styles/courseHub.css";

const PAGE_SIZE = 20;

export default function CourseHubPage() {
  const { t } = useTranslation();
  const { selectedSemesterObj, selectedSemester } = useAcademic();

  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [category, setCategory] = useState("");
  const [activeOnly, setActiveOnly] = useState("");
  const [page, setPage] = useState(1);
  const [compact, setCompact] = useState(true);
  const [activeCourse, setActiveCourse] = useState(null);

  const queryParams = useMemo(() => ({
    page,
    pageSize: PAGE_SIZE,
    search: appliedSearch || undefined,
    category: category !== "" ? Number(category) : undefined,
    isActive: activeOnly !== "" ? activeOnly === "true" : undefined,
  }), [page, appliedSearch, category, activeOnly]);

  const { data, isLoading, error, refetch } = useCourses(queryParams);
  const courses = useMemo(() => data?.items || [], [data]);
  const totalPages = data?.totalPages || 1;
  const totalCount = data?.totalCount || 0;

  const stats = useMemo(() => {
    const active = courses.filter((c) => c.isActive).length;
    const closed = courses.filter((c) => c.isClosed).length;
    const credits = courses.reduce((a, c) => a + (c.creditHours || 0), 0);
    return { onPage: courses.length, active, closed, credits };
  }, [courses]);

  const applySearch = () => { setAppliedSearch(search); setPage(1); };
  const clearSearch = () => { setSearch(""); setAppliedSearch(""); setPage(1); };

  const columns = [
    {
      key: "code", label: t("code") || "Code", width: 130,
      render: (v, row) => (
        <span onClick={(e) => e.stopPropagation()}>
          <EntityHoverCard
            trigger={<span className="ch-code-pill">{v}</span>}
            title={row.title}
            rows={[
              { label: "Credit hours", value: row.creditHours },
              { label: "Category", value: getCourseCategoryLabel(row.category) },
              { label: "Status", value: row.isActive ? "Active" : "Inactive" },
              { label: "Record", value: row.isClosed ? "Closed" : "Open" },
            ]}
            onClick={() => setActiveCourse(row)}
          />
        </span>
      ),
    },
    {
      key: "title", label: t("title") || "Title", nowrap: false,
      render: (v) => <span style={{ fontWeight: 600, color: "#1a1f5e" }}>{v}</span>,
    },
    {
      key: "creditHours", label: t("credit_hours") || "Credits", align: "center", width: 80,
      render: (v) => <span style={{ fontVariantNumeric: "tabular-nums" }}>{v}</span>,
    },
    {
      key: "category", label: t("category") || "Category", width: 170,
      render: (v) => <span style={{ fontSize: 12, color: "#4b5563" }}>{getCourseCategoryLabel(v)}</span>,
    },
    {
      key: "isActive", label: t("status") || "Status", width: 100,
      render: (v) => <StatusBadge status={v ? "active" : "inactive"} />,
    },
    {
      key: "isClosed", label: "Record", width: 90,
      render: (v) => <StatusBadge status={v ? "closed" : "open"} label={v ? "Closed" : "Open"} />,
    },
    {
      key: "_nav", label: "", width: 60, align: "right",
      render: () => <span style={{ fontSize: 11, color: "#1a1f5e", fontWeight: 600 }}>Open →</span>,
    },
  ];

  return (
    <div className="ch-page">
      <div className="ch-header">
        <div className="ch-header-left">
          <Library size={20} />
          <div>
            <h1>Course Hub</h1>
            <p>
              Browse the catalog and drill into each course's offerings &amp; schedule
              {selectedSemesterObj ? ` — ${selectedSemester}` : ""}
            </p>
          </div>
        </div>
      </div>

      {!selectedSemesterObj && (
        <div role="status" style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 14px", background: "#fffbeb", border: "1px solid #fcd34d", borderRadius: 8, color: "#92400e", fontSize: 13, marginBottom: 16 }}>
          <CalendarDays size={15} />
          No semester selected — courses are listed, but offerings open per-semester. Pick a semester in the context bar for full detail.
        </div>
      )}

      {/* Stat strip */}
      <div className="ch-stats">
        <div className="ch-stat"><span className="ch-stat-value">{totalCount}</span><span className="ch-stat-label">Total Courses</span></div>
        <div className="ch-stat-divider" />
        <div className="ch-stat success"><span className="ch-stat-value">{stats.active}</span><span className="ch-stat-label">Active (page)</span></div>
        <div className="ch-stat"><span className="ch-stat-value">{stats.closed}</span><span className="ch-stat-label">Closed (page)</span></div>
        <div className="ch-stat-divider" />
        <div className="ch-stat"><span className="ch-stat-value">{stats.credits}</span><span className="ch-stat-label">Credits (page)</span></div>
      </div>

      {/* Toolbar */}
      <div className="ch-toolbar">
        <div className="ch-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search courses by code or title…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && applySearch()}
            aria-label="Search courses"
          />
          {search && <button className="btn-cancel" style={{ padding: 4 }} onClick={clearSearch}><X size={14} /></button>}
        </div>
        <select className="ch-select" value={category} onChange={(e) => { setCategory(e.target.value); setPage(1); }} aria-label="Filter by category">
          <option value="">All categories</option>
          {COURSE_CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
        </select>
        <select className="ch-select" value={activeOnly} onChange={(e) => { setActiveOnly(e.target.value); setPage(1); }} aria-label="Filter by active">
          <option value="">Active &amp; inactive</option>
          <option value="true">Active only</option>
          <option value="false">Inactive only</option>
        </select>
        <button className="btn-primary" onClick={applySearch}><Search size={13} /> Search</button>
        {(appliedSearch || category || activeOnly) && (
          <button className="btn-cancel" onClick={() => { clearSearch(); setCategory(""); setActiveOnly(""); }}>
            <X size={13} /> Clear
          </button>
        )}
      </div>

      {error && (
        <div role="alert" style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 14px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 13, marginBottom: 12 }}>
          <AlertTriangle size={16} /> {error.message || "Failed to load courses"}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      <DataTable
        columns={columns}
        data={courses}
        loading={isLoading}
        error={error?.message}
        emptyIcon={BookOpen}
        emptyTitle="No courses found"
        emptyMessage="No courses match your search criteria."
        pagination={{ pageNumber: page, totalPages }}
        onPageChange={setPage}
        onRowClick={(row) => setActiveCourse(row)}
        rowKey="id"
        compact={compact}
        onCompactToggle={() => setCompact((c) => !c)}
        tableLabel="Course catalog"
      />

      <CourseDetailDrawer
        open={!!activeCourse}
        onClose={() => setActiveCourse(null)}
        course={activeCourse}
        semester={selectedSemesterObj}
      />
    </div>
  );
}
