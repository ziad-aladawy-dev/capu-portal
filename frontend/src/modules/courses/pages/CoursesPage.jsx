import { useState, useMemo } from "react";
import {
  BookOpen, Plus, Edit2, Trash2, X, Search, AlertTriangle,
  AlertCircle, RefreshCw,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import { COURSE_CATEGORIES, getCourseCategoryLabel } from "../../../core/services/courseService";
import { useToast } from "../../../core/components/Toast";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import {
  useCourses, useCreateCourse, useUpdateCourse,
  useDeleteCourse, useCloseCourse, useOpenCourse, useBulkDeleteCourses,
} from "../../../core/query/useCourses";
import { findCoursePrerequisiteCycle } from "../../../core/utils/dagValidator";
import "../styles/courses.css";

const PAGE_SIZE = 20;
const EMPTY_FORM = { code: "", title: "", creditHours: 3, category: 0, prerequisites: [] };

function CoursesPage() {
  const { addToast } = useToast();

  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [compact, setCompact] = useState(false);

  const [showHealthWarning, setShowHealthWarning] = useState(false);

  const queryParams = { page, pageSize: PAGE_SIZE, search, category: categoryFilter };
  const { data: courseData, isLoading, error, refetch } = useCourses(queryParams);
  const courses = courseData?.items || [];
  const totalPages = courseData?.totalPages || 1;

  // Fetch ALL courses for health check & DAG validation
  const { data: allCourseData } = useCourses({ page: 1, pageSize: 10000 });
  const allCourses = allCourseData?.items || [];

  const createCourse = useCreateCourse();
  const updateCourse = useUpdateCourse();
  const deleteCourse = useDeleteCourse();
  const closeCourse = useCloseCourse();
  const openCourse = useOpenCourse();
  const bulkDelete = useBulkDeleteCourses();

  const [drawerMode, setDrawerMode] = useState(null);
  const [editCourse, setEditCourse] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState("");

  const [confirmAction, setConfirmAction] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const [prereqWarning, setPrereqWarning] = useState(null);

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); };

  const openCreateDrawer = () => {
    setDrawerMode("create");
    setEditCourse(null);
    setForm(EMPTY_FORM);
    setFormError("");
    setPrereqWarning(null);
  };
  const openEditDrawer = (course) => {
    setDrawerMode("edit");
    setEditCourse(course);
    setForm({ code: course.code, title: course.title, creditHours: course.creditHours, category: course.category, prerequisites: course.prerequisites || [] });
    setFormError("");
    setPrereqWarning(null);
  };
  const closeDrawer = () => {
    setDrawerMode(null);
    setEditCourse(null);
    setForm(EMPTY_FORM);
    setFormError("");
    setPrereqWarning(null);
  };

  const validateForm = () => {
    if (!form.code.trim()) { setFormError("Course code is required"); return false; }
    if (!form.title.trim()) { setFormError("Title is required"); return false; }
    if (!Number.isFinite(form.creditHours) || form.creditHours < 0 || form.creditHours > 30) {
      setFormError("Credit hours must be between 0 and 30"); return false;
    }
    // DAG check for prerequisite cycles
    if (editCourse && form.prerequisites?.length > 0) {
      const cycleResult = findCoursePrerequisiteCycle(editCourse.id, allCourses);
      if (cycleResult?.inCycle) {
        setFormError(`Prerequisite cycle detected involving: ${cycleResult.cycle.join(" → ")}`);
        setPrereqWarning(cycleResult);
        return false;
      }
    }
    setFormError(""); return true;
  };

  const handleSave = async (e) => {
    e.preventDefault();
    if (!validateForm()) return;
    try {
      if (drawerMode === "create") {
        await createCourse.mutateAsync(form);
        addToast("Course created", "success");
      } else if (drawerMode === "edit" && editCourse) {
        await updateCourse.mutateAsync({ id: editCourse.id, ...form });
        addToast("Course updated", "success");
      }
      closeDrawer();
    } catch (err) {
      if (err.status === 409) setFormError("A course with this code already exists");
      else setFormError(err.message || "Failed to save course");
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteCourse.mutateAsync(deleteTarget.id);
      addToast("Course deleted", "success");
      setDeleteTarget(null);
    } catch (err) {
      addToast(err.message || "Failed to delete course", "error");
      setDeleteTarget(null);
    }
  };

  const handleClose = async () => {
    if (!confirmAction?.course) return;
    try {
      await closeCourse.mutateAsync(confirmAction.course.id);
      addToast(`${confirmAction.course.code} closed`, "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to close course", "error");
      setConfirmAction(null);
    }
  };

  const handleOpen = async () => {
    if (!confirmAction?.course) return;
    try {
      await openCourse.mutateAsync(confirmAction.course.id);
      addToast(`${confirmAction.course.code} reopened`, "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to reopen course", "error");
      setConfirmAction(null);
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await bulkDelete.mutateAsync(ids);
      addToast(`${ids.length} course(s) deleted`, "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

  // Catalog Health: courses with offeringCount === 0
  // Note: offeringCount requires cross-module data not currently exposed via the API
  const orphanCourses = useMemo(() => {
    if (!allCourseData?.items) return [];
    return allCourseData.items.filter((c) => !c.offeringCount && c.offeringCount !== undefined);
  }, [allCourseData]);

  // Build prerequisite list UI
  const availablePrereqs = useMemo(() => {
    if (!allCourses) return [];
    return allCourses.filter((c) => !editCourse || c.id !== editCourse.id);
  }, [allCourses, editCourse]);

  return (
    <div className="courses-page">
      <div className="courses-header">
        <div className="courses-header-left">
          <BookOpen size={22} />
          <div>
            <h1>Course Catalog</h1>
            <p>Manage course offerings, credit hours and categories.</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <button
            className={`courses-btn courses-btn-outline ${showHealthWarning ? "danger" : ""}`}
            onClick={() => setShowHealthWarning((p) => !p)}
            title="Catalog Health"
          >
            <AlertCircle size={13} /> Health
          </button>
          <PermissionGate resource="courses.courses" minLevel={2}>
            <button className="courses-btn courses-btn-primary" onClick={openCreateDrawer}>
              <Plus size={14} /> Create Course
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="courses-error" role="alert" style={{ padding: "12px 16px", marginBottom: 16, flexDirection: "row" }}>
          <AlertTriangle size={16} className="courses-error-icon" />
          <p style={{ margin: 0, fontSize: 13, flex: 1 }}>{error.message || "Failed to load courses"}</p>
          <button className="courses-btn courses-btn-outline" style={{ padding: "4px 12px", fontSize: 12 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      {/* Catalog Health Alert */}
      {showHealthWarning && orphanCourses.length > 0 && (
        <div role="alert" style={{ padding: "12px 16px", marginBottom: 16, background: "#fffbeb", border: "1px solid #fcd34d", borderRadius: 8, fontSize: 13 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
            <AlertTriangle size={16} style={{ color: "#d97706" }} />
            <strong style={{ color: "#92400e" }}>Catalog Health Warning</strong>
          </div>
          <p style={{ margin: "0 0 8px", color: "#92400e" }}>
            <strong>{orphanCourses.length}</strong> course(s) in the catalog have never been offered. These may be obsolete:
          </p>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
            {orphanCourses.slice(0, 10).map((c) => (
              <span key={c.id} style={{ padding: "2px 8px", background: "#fef3c7", borderRadius: 4, fontSize: 11, fontWeight: 600, color: "#92400e" }}>
                {c.code}
              </span>
            ))}
            {orphanCourses.length > 10 && (
              <span style={{ padding: "2px 8px", fontSize: 11, color: "#6b7280" }}>
                +{orphanCourses.length - 10} more
              </span>
            )}
          </div>
        </div>
      )}

      <div className="courses-toolbar">
        <div className="courses-search">
          <Search size={14} />
          <input type="text" placeholder="Search by code or title…" value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSearch()} aria-label="Search courses" />
          {search && (
            <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
              onClick={() => { setSearch(""); setPage(1); setSelectedIds(new Set()); }}>
              <X size={14} />
            </button>
          )}
        </div>
        <select className="courses-filter-select" value={categoryFilter}
          onChange={(e) => { setCategoryFilter(e.target.value); setPage(1); setSelectedIds(new Set()); }}
          aria-label="Filter by category">
          <option value="">All categories</option>
          {COURSE_CATEGORIES.map((c) => (<option key={c.value} value={c.value}>{c.label}</option>))}
        </select>
        <button className="courses-btn courses-btn-outline" onClick={handleSearch}>
          <Search size={13} /> Search
        </button>
      </div>

      <DataTable
        columns={[
          { key: "code", label: "Code", render: (v) => <strong style={{ fontFamily: "Space Mono, monospace" }}>{v}</strong> },
          { key: "title", label: "Title" },
          { key: "creditHours", label: "Credits", align: "center" },
          { key: "category", label: "Category", render: (v) => <span className="courses-badge courses-badge-cat">{getCourseCategoryLabel(v)}</span> },
          { key: "isActive", label: "Status", render: (v) => <StatusBadge status={v ? "active" : "inactive"} label={v ? "Active" : "Inactive"} /> },
          { key: "isClosed", label: "Record", render: (v) => <StatusBadge status={v ? "closed" : "open"} label={v ? "Closed" : "Open"} /> },
          {
            key: "offeringCount", label: "Offerings", align: "center",
            render: (v, row) => {
              if (v === 0) return <StatusBadge status="draft" label="0" style={{ background: "#fef3c7", color: "#92400e" }} />;
              if (!v && v !== 0) return <span style={{ color: "#9ca3af" }}>—</span>;
              return <span style={{ fontFamily: "Space Mono, monospace" }}>{v}</span>;
            },
          },
          { key: "actions", label: "Actions", render: (_, row) => (
            <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
              <PermissionGate resource="courses.courses" minLevel={3}>
                <button className="courses-action-btn edit" onClick={() => openEditDrawer(row)} title="Edit"><Edit2 size={13} /></button>
              </PermissionGate>
              {row.isClosed ? (
                <PermissionGate resource="courses.courses" minLevel={4}>
                  <button className="courses-action-btn edit" onClick={() => setConfirmAction({ type: "open", course: row })}
                    disabled={openCourse.isPending} title="Reopen"><Trash2 size={13} /></button>
                </PermissionGate>
              ) : (
                <PermissionGate resource="courses.courses" minLevel={3}>
                  <button className="courses-action-btn edit" onClick={() => setConfirmAction({ type: "close", course: row })}
                    disabled={closeCourse.isPending} title="Close Record"><Trash2 size={13} /></button>
                </PermissionGate>
              )}
              <PermissionGate resource="courses.courses" minLevel={5}>
                <button className="courses-action-btn delete" onClick={() => setDeleteTarget(row)} title="Delete"><Trash2 size={13} /></button>
              </PermissionGate>
            </div>
          ), nowrap: true },
        ]}
        data={courses}
        loading={isLoading}
        error={error?.message}
        emptyIcon={BookOpen}
        emptyTitle="No courses found"
        emptyMessage="Create your first course to start building the catalog."
        emptyActionLabel="Create Course"
        emptyAction={openCreateDrawer}
        pagination={{ pageNumber: page, totalPages }}
        onPageChange={setPage}
        selectedIds={selectedIds}
        onSelectAll={() => {
          if (courses.every((c) => selectedIds.has(c.id))) setSelectedIds(new Set());
          else setSelectedIds(new Set(courses.map((c) => c.id)));
        }}
        onSelectOne={(id) => {
          setSelectedIds((prev) => { const n = new Set(prev); if (n.has(id)) n.delete(id); else n.add(id); return n; });
        }}
        compact={compact}
        onCompactToggle={() => setCompact((c) => !c)}
      />

      {selectedIds.size > 0 && (
        <div className="bulk-bar">
          <span>{selectedIds.size} selected</span>
          <button onClick={() => setSelectedIds(new Set())}>Clear</button>
          <PermissionGate resource="courses.courses" minLevel={5}>
            <button className="bulk-danger" onClick={handleBulkDelete} disabled={bulkDelete.isPending}>
              <Trash2 size={12} /> Delete Selected
            </button>
          </PermissionGate>
        </div>
      )}

      {/* Create/Edit Drawer */}
      <Drawer
        open={!!drawerMode}
        onClose={closeDrawer}
        title={drawerMode === "create" ? "Create Course" : "Edit Course"}
        width={440}
        loading={createCourse.isPending || updateCourse.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={closeDrawer}>Cancel</button>
            <button className="btn-primary" onClick={handleSave}>
              {createCourse.isPending || updateCourse.isPending ? "Saving..." : drawerMode === "create" ? "Create" : "Save Changes"}
            </button>
          </>
        }
      >
        {formError && (
          <div role="alert" style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16 }}>
            {formError}
          </div>
        )}

        {prereqWarning && (
          <div role="alert" style={{ padding: "8px 12px", background: "#fffbeb", border: "1px solid #fcd34d", borderRadius: 8, color: "#92400e", fontSize: 12, marginBottom: 16 }}>
            <AlertTriangle size={12} style={{ marginRight: 4 }} />
            Cycle detected: {prereqWarning.cycle.join(" → ")}
          </div>
        )}

        <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="courses-form-group">
            <label htmlFor="course-code">Code</label>
            <input id="course-code" type="text" className="courses-form-input" value={form.code}
              onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))}
              placeholder="e.g. CS101" disabled={drawerMode === "edit"} maxLength={32} autoFocus />
            {drawerMode === "edit" && <span className="courses-form-hint">Code cannot be changed after creation.</span>}
          </div>
          <div className="courses-form-group">
            <label htmlFor="course-title">Title</label>
            <input id="course-title" type="text" className="courses-form-input" value={form.title}
              onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
              placeholder="e.g. Introduction to Computer Science" maxLength={200} />
          </div>
          <div className="courses-form-group">
            <label htmlFor="course-credit">Credit Hours</label>
            <input id="course-credit" type="number" className="courses-form-input" value={form.creditHours}
              min={0} max={30} onChange={(e) => setForm((p) => ({ ...p, creditHours: Number(e.target.value) }))} />
          </div>
          <div className="courses-form-group">
            <label htmlFor="course-cat">Category</label>
            <select id="course-cat" className="courses-form-select" value={form.category}
              onChange={(e) => setForm((p) => ({ ...p, category: Number(e.target.value) }))}>
              {COURSE_CATEGORIES.map((c) => (<option key={c.value} value={c.value}>{c.label}</option>))}
            </select>
          </div>

          {/* Prerequisites - only show in edit mode */}
          {drawerMode === "edit" && (
            <div className="courses-form-group">
              <label>Prerequisites</label>
              <select multiple value={form.prerequisites || []}
                onChange={(e) => {
                  const selected = Array.from(e.target.selectedOptions, (opt) => opt.value);
                  setForm((p) => ({ ...p, prerequisites: selected }));
                }}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 12, minHeight: 80, fontFamily: "inherit" }}>
                {availablePrereqs.map((c) => (
                  <option key={c.id} value={c.id}>{c.code} — {c.title}</option>
                ))}
              </select>
              <small style={{ color: "#6b7280", fontSize: 10 }}>
                Hold Ctrl/Cmd to select multiple. Cycles are detected automatically.
              </small>
            </div>
          )}
        </form>
      </Drawer>

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title="Delete Course"
        message={`Delete ${deleteTarget?.code}?`}
        detail="This will remove the course from the catalog."
        confirmLabel="Delete"
        variant="danger"
        loading={deleteCourse.isPending}
      />

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={confirmAction?.type === "close" ? handleClose : handleOpen}
        title={confirmAction?.type === "close" ? "Close Record" : "Reopen Record"}
        message={`${confirmAction?.type === "close" ? "Close" : "Reopen"} ${confirmAction?.course?.code}?`}
        detail={confirmAction?.type === "close" ? "A closed course will be marked as inactive." : "A reopened course will be available for use again."}
        confirmLabel={confirmAction?.type === "close" ? "Close" : "Reopen"}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closeCourse.isPending || openCourse.isPending}
      />
    </div>
  );
}

export default CoursesPage;
