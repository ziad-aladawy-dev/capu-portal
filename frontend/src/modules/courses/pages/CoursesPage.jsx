import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import {
  BookOpen, Plus, Edit2, Trash2, X, Search, AlertTriangle, RefreshCw,
  Lock, Unlock, Download, ChevronLeft, ChevronRight,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import * as courseService from "../../../core/services/courseService";
import { useToast } from "../../../core/components/Toast";
import { SkeletonTable } from "../../../core/components/Skeleton";
import EmptyState from "../../../core/components/EmptyState";
import BulkActionBar from "../../../core/components/BulkActionBar";
import "../styles/courses.css";

const EMPTY_FORM = {
  code: "",
  title: "",
  creditHours: 3,
  category: 0,
};

const PAGE_SIZE = 20;

function CoursesPage() {
  const { addToast } = useToast();
  const searchRef = useRef(null);

  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedIds, setSelectedIds] = useState(new Set());

  const [modalMode, setModalMode] = useState(null);
  const [editCourse, setEditCourse] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const [lifecycleLoading, setLifecycleLoading] = useState(null);

  const loadCourses = useCallback(async (p = 1, append = false) => {
    setLoading(true);
    setError(null);
    try {
      const params = {
        Page: p,
        PageSize: PAGE_SIZE,
        Search: search.trim() || undefined,
        Category: categoryFilter !== "" ? Number(categoryFilter) : undefined,
        IsActive: true,
      };
      const result = await courseService.searchCourses(params);
      const items = result?.items || result || [];
      if (append) {
        setCourses(prev => [...prev, ...items]);
      } else {
        setCourses(Array.isArray(items) ? items : []);
      }
      setTotalCount(result?.totalCount || 0);
      setPage(p);
    } catch (err) {
      setError(err.message || "Failed to load courses");
      setCourses([]);
    } finally {
      setLoading(false);
    }
  }, [search, categoryFilter]);

  useEffect(() => {
    loadCourses(1);
  }, [refreshKey]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const handleSearch = () => {
    setPage(1);
    setSelectedIds(new Set());
    loadCourses(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") handleSearch();
  };

  const openCreate = () => {
    setModalMode("create");
    setEditCourse(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const openEdit = (course) => {
    setModalMode("edit");
    setEditCourse(course);
    setForm({
      code: course.code,
      title: course.title,
      creditHours: course.creditHours,
      category: course.category,
    });
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditCourse(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const validateForm = () => {
    if (!form.code.trim()) { setFormError("Course code is required"); return false; }
    if (!form.title.trim()) { setFormError("Title is required"); return false; }
    if (!Number.isFinite(form.creditHours) || form.creditHours < 0 || form.creditHours > 30) {
      setFormError("Credit hours must be between 0 and 30");
      return false;
    }
    setFormError("");
    return true;
  };

  const handleSave = async (e) => {
    e.preventDefault();
    if (!validateForm()) return;
    setSaving(true);
    try {
      if (modalMode === "create") {
        await courseService.createCourse({
          code: form.code.trim(),
          title: form.title.trim(),
          creditHours: Number(form.creditHours),
          category: Number(form.category),
        });
        addToast("Course created successfully", "success");
      } else if (modalMode === "edit" && editCourse) {
        await courseService.updateCourse(editCourse.id, {
          title: form.title.trim(),
          creditHours: Number(form.creditHours),
          category: Number(form.category),
        });
        addToast("Course updated successfully", "success");
      }
      closeModal();
      setRefreshKey(k => k + 1);
    } catch (err) {
      if (err.status === 409) {
        setFormError("A course with this code already exists");
      } else {
        setFormError(err.message || "Failed to save course");
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await courseService.deleteCourse(deleteTarget.id);
      addToast("Course deleted", "success");
      setDeleteTarget(null);
      setRefreshKey(k => k + 1);
    } catch (err) {
      setError(err.message || "Failed to delete course");
      setDeleteTarget(null);
    } finally {
      setDeleting(false);
    }
  };

  const handleClose = async (course) => {
    setLifecycleLoading(course.id);
    try {
      await courseService.closeCourse(course.id);
      addToast(`${course.code} closed`, "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to close course", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleOpen = async (course) => {
    setLifecycleLoading(course.id);
    try {
      await courseService.openCourse(course.id);
      addToast(`${course.code} reopened`, "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to reopen course", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const allSelected = courses.length > 0 && courses.every(c => selectedIds.has(c.id));
  const someSelected = courses.some(c => selectedIds.has(c.id));

  const handleSelectAll = () => {
    if (allSelected) setSelectedIds(new Set());
    else setSelectedIds(new Set(courses.map(c => c.id)));
  };

  const handleSelectOne = (id) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      const result = await courseService.bulkDeleteCourses(ids);
      addToast(`${ids.length} course(s) deleted`, "success");
      setSelectedIds(new Set());
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

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
          <PermissionGate resource="courses.courses" minLevel={2}>
            <button className="courses-btn courses-btn-primary" onClick={openCreate}>
              <Plus size={14} />
              Create Course
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="courses-error" style={{ padding: "12px 16px", marginBottom: 16, flexDirection: "row" }}>
          <AlertTriangle size={16} className="courses-error-icon" />
          <p style={{ margin: 0, fontSize: 13, flex: 1 }}>{error}</p>
          <button className="courses-btn courses-btn-outline" style={{ padding: "4px 12px", fontSize: 12 }} onClick={() => setRefreshKey(k => k + 1)}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      <div className="courses-toolbar">
        <div className="courses-search">
          <Search size={14} />
          <input
            ref={searchRef}
            type="text"
            placeholder="Search by code or title…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          {search && (
            <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
              onClick={() => { setSearch(""); setRefreshKey(k => k + 1); }}>
              <X size={14} />
            </button>
          )}
        </div>
        <select
          className="courses-filter-select"
          value={categoryFilter}
          onChange={(e) => { setCategoryFilter(e.target.value); setPage(1); setRefreshKey(k => k + 1); }}
        >
          <option value="">All categories</option>
          {courseService.COURSE_CATEGORIES.map((c) => (
            <option key={c.value} value={c.value}>{c.label}</option>
          ))}
        </select>
        <button className="courses-btn courses-btn-outline" onClick={handleSearch}>
          <Search size={13} /> Search
        </button>
      </div>

      {loading ? (
        <SkeletonTable rows={8} cols={6} />
      ) : courses.length === 0 ? (
        <div style={{ marginTop: 16 }}>
          <EmptyState
            icon={BookOpen}
            title="No courses found"
            message={totalCount === 0 ? "Create your first course to start building the catalog." : "Try adjusting your search or filters."}
            actionLabel={totalCount === 0 ? "Create Course" : undefined}
            onAction={totalCount === 0 ? openCreate : undefined}
          />
        </div>
      ) : (
        <div className="courses-table-wrapper">
          <table className="courses-table">
            <thead>
              <tr>
                <th style={{ width: 36 }}>
                  <input type="checkbox" checked={allSelected} ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }} onChange={handleSelectAll} />
                </th>
                <th>#</th>
                <th>Code</th>
                <th>Title</th>
                <th>Credits</th>
                <th>Category</th>
                <th>Status</th>
                <th>Record</th>
                <th className="col-actions">Actions</th>
              </tr>
            </thead>
            <tbody>
              {courses.map((course, idx) => (
                <tr key={course.id} className={selectedIds.has(course.id) ? "selected-row" : ""}>
                  <td onClick={e => e.stopPropagation()}>
                    <input type="checkbox" checked={selectedIds.has(course.id)} onChange={() => handleSelectOne(course.id)} />
                  </td>
                  <td style={{ color: "#6b7280" }}>{(page - 1) * PAGE_SIZE + idx + 1}</td>
                  <td><strong style={{ fontFamily: "Space Mono, monospace" }}>{course.code}</strong></td>
                  <td>{course.title}</td>
                  <td>{course.creditHours}</td>
                  <td>
                    <span className="courses-badge courses-badge-cat">
                      {courseService.getCourseCategoryLabel(course.category)}
                    </span>
                  </td>
                  <td>
                    <span className={`courses-badge ${course.isActive ? "courses-badge-active" : "courses-badge-inactive"}`}>
                      {course.isActive ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td>
                    <span className={`courses-badge ${course.isClosed ? "courses-badge-inactive" : "courses-badge-active"}`}>
                      {course.isClosed ? "Closed" : "Open"}
                    </span>
                  </td>
                  <td className="col-actions">
                    <PermissionGate resource="courses.courses" minLevel={3}>
                      <button className="courses-action-btn edit" onClick={() => openEdit(course)} title="Edit">
                        <Edit2 size={13} />
                      </button>
                    </PermissionGate>
                    {course.isClosed ? (
                      <PermissionGate resource="courses.courses" minLevel={4}>
                        <button className="courses-action-btn edit" onClick={() => handleOpen(course)} disabled={lifecycleLoading === course.id} title="Reopen">
                          <Unlock size={13} />
                        </button>
                      </PermissionGate>
                    ) : (
                      <PermissionGate resource="courses.courses" minLevel={3}>
                        <button className="courses-action-btn edit" onClick={() => handleClose(course)} disabled={lifecycleLoading === course.id} title="Close">
                          <Lock size={13} />
                        </button>
                      </PermissionGate>
                    )}
                    <PermissionGate resource="courses.courses" minLevel={5}>
                      <button className="courses-action-btn delete" onClick={() => setDeleteTarget(course)} title="Delete">
                        <Trash2 size={13} />
                      </button>
                    </PermissionGate>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 8, marginTop: 20 }}>
          <button className="courses-btn courses-btn-outline" disabled={page <= 1} onClick={() => loadCourses(page - 1)}>
            <ChevronLeft size={14} /> Previous
          </button>
          {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
            let pn;
            if (totalPages <= 7) pn = i + 1;
            else {
              const mid = Math.floor(7 / 2);
              if (page <= mid + 1) pn = i + 1;
              else if (page >= totalPages - mid) pn = totalPages - 7 + i + 1;
              else pn = page - mid + i;
            }
            return (
              <button key={pn} className={`courses-btn ${pn === page ? "courses-btn-primary" : "courses-btn-outline"}`}
                style={{ minWidth: 36, justifyContent: "center" }} onClick={() => loadCourses(pn)}>
                {pn}
              </button>
            );
          })}
          <button className="courses-btn courses-btn-outline" disabled={page >= totalPages} onClick={() => loadCourses(page + 1)}>
            Next <ChevronRight size={14} />
          </button>
        </div>
      )}

      <BulkActionBar
        selectedCount={selectedIds.size}
        onClear={() => setSelectedIds(new Set())}
        actions={[
          {
            label: "Delete", icon: <Trash2 size={13} />, variant: "danger",
            onClick: handleBulkDelete, requiresPermission: true,
            permissionResource: "courses.courses", permissionLevel: 5,
          },
        ]}
      />

      {modalMode && (
        <div className="courses-modal-overlay" onClick={closeModal}>
          <div className="courses-modal" onClick={(e) => e.stopPropagation()}>
            <div className="courses-modal-header">
              <h2>{modalMode === "create" ? "Create Course" : "Edit Course"}</h2>
              <button className="courses-modal-close" onClick={closeModal}><X size={16} /></button>
            </div>
            <form onSubmit={handleSave}>
              <div className="courses-modal-body">
                <div className="courses-form-group">
                  <label>Code</label>
                  <input id="course-code" type="text" className="courses-form-input" value={form.code}
                    onChange={(e) => setForm(p => ({ ...p, code: e.target.value }))}
                    placeholder="e.g. CS101" disabled={modalMode === "edit"} maxLength={32} autoFocus />
                  {modalMode === "edit" && <span className="courses-form-hint">Code cannot be changed after creation.</span>}
                </div>
                <div className="courses-form-group">
                  <label>Title</label>
                  <input id="course-title" type="text" className="courses-form-input" value={form.title}
                    onChange={(e) => setForm(p => ({ ...p, title: e.target.value }))}
                    placeholder="e.g. Introduction to Computer Science" maxLength={200} />
                </div>
                <div className="courses-form-group">
                  <label>Credit Hours</label>
                  <input id="course-credit" type="number" className="courses-form-input" value={form.creditHours}
                    min={0} max={30} onChange={(e) => setForm(p => ({ ...p, creditHours: Number(e.target.value) }))} />
                </div>
                <div className="courses-form-group">
                  <label>Category</label>
                  <select id="course-cat" className="courses-form-select" value={form.category}
                    onChange={(e) => setForm(p => ({ ...p, category: Number(e.target.value) }))}>
                    {courseService.COURSE_CATEGORIES.map((c) => (
                      <option key={c.value} value={c.value}>{c.label}</option>
                    ))}
                  </select>
                </div>
                {formError && <span className="courses-form-error">{formError}</span>}
              </div>
              <div className="courses-modal-footer">
                <button type="button" className="courses-btn courses-btn-outline" onClick={closeModal}>Cancel</button>
                <button type="submit" className="courses-btn courses-btn-primary" disabled={saving}>
                  {saving ? "Saving…" : modalMode === "create" ? "Create" : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {deleteTarget && (
        <div className="courses-modal-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="courses-modal" onClick={(e) => e.stopPropagation()}>
            <div className="courses-modal-header">
              <h2>Delete Course</h2>
              <button className="courses-modal-close" onClick={() => setDeleteTarget(null)}><X size={16} /></button>
            </div>
            <div className="courses-delete-body">
              <AlertTriangle size={36} className="courses-delete-icon" />
              <p>Delete <strong>{deleteTarget.code}</strong>?</p>
              <p className="courses-delete-hint">This will remove the course from the catalog.</p>
            </div>
            <div className="courses-modal-footer">
              <button className="courses-btn courses-btn-outline" onClick={() => setDeleteTarget(null)} disabled={deleting}>Cancel</button>
              <button className="courses-btn courses-btn-danger" onClick={handleDelete} disabled={deleting}>
                {deleting ? "Deleting…" : "Delete"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default CoursesPage;
