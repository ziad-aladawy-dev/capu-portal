import { useCallback, useEffect, useMemo, useState } from "react";
import {
  BookOpen, Plus, Edit2, Trash2, X, Search, AlertTriangle, RefreshCw,
} from "lucide-react";
import * as courseService from "../../../core/services/courseService";
import "../styles/courses.css";

const EMPTY_FORM = {
  code: "",
  title: "",
  creditHours: 3,
  category: 0,
  isActive: true,
};

function CoursesPage() {
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");

  const [modalMode, setModalMode] = useState(null);
  const [editCourse, setEditCourse] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const loadCourses = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await courseService.fetchActiveCourses();
      setCourses(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load courses");
      setCourses([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadCourses();
  }, [loadCourses]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return courses.filter((c) => {
      if (q && !c.code.toLowerCase().includes(q) && !c.title.toLowerCase().includes(q)) {
        return false;
      }
      if (categoryFilter !== "" && c.category !== Number(categoryFilter)) {
        return false;
      }
      return true;
    });
  }, [courses, search, categoryFilter]);

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
      isActive: course.isActive,
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
    if (!form.code.trim()) {
      setFormError("Course code is required");
      return false;
    }
    if (!form.title.trim()) {
      setFormError("Title is required");
      return false;
    }
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
      } else if (modalMode === "edit" && editCourse) {
        await courseService.updateCourse(editCourse.id, {
          title: form.title.trim(),
          creditHours: Number(form.creditHours),
          category: Number(form.category),
          isActive: form.isActive,
        });
      }
      closeModal();
      await loadCourses();
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
      setDeleteTarget(null);
      await loadCourses();
    } catch (err) {
      setError(err.message || "Failed to delete course");
      setDeleteTarget(null);
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="courses-page">
        <div className="courses-loading">
          <div className="courses-spinner" />
          <p>Loading courses…</p>
        </div>
      </div>
    );
  }

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
        <div>
          <button className="courses-btn courses-btn-primary" onClick={openCreate}>
            <Plus size={14} />
            Create Course
          </button>
        </div>
      </div>

      {error && (
        <div
          className="courses-error"
          style={{
            padding: "12px 16px",
            marginBottom: 16,
            flexDirection: "row",
            background: "#fef2f2",
            border: "1px solid #fecaca",
          }}
        >
          <AlertTriangle size={16} className="courses-error-icon" />
          <p style={{ margin: 0, fontSize: 13 }}>{error}</p>
          <button
            className="courses-btn courses-btn-outline"
            style={{ marginLeft: "auto", padding: "4px 12px", fontSize: 12 }}
            onClick={loadCourses}
          >
            <RefreshCw size={11} />
            Retry
          </button>
        </div>
      )}

      <div className="courses-toolbar">
        <div className="courses-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search by code or title…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <select
          className="courses-filter-select"
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
        >
          <option value="">All categories</option>
          {courseService.COURSE_CATEGORIES.map((c) => (
            <option key={c.value} value={c.value}>
              {c.label}
            </option>
          ))}
        </select>
      </div>

      {filtered.length === 0 ? (
        <div className="courses-empty">
          <BookOpen size={40} />
          <h3>No courses found</h3>
          <p>
            {courses.length === 0
              ? "Create your first course to start building the catalog."
              : "Adjust the filters above to find a course."}
          </p>
          {courses.length === 0 && (
            <button className="courses-btn courses-btn-primary" onClick={openCreate}>
              <Plus size={14} />
              Create Course
            </button>
          )}
        </div>
      ) : (
        <div className="courses-table-wrapper">
          <table className="courses-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Title</th>
                <th>Credit Hours</th>
                <th>Category</th>
                <th>Status</th>
                <th className="col-actions">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((course) => (
                <tr key={course.id}>
                  <td>
                    <strong style={{ fontFamily: "Space Mono, monospace" }}>
                      {course.code}
                    </strong>
                  </td>
                  <td>{course.title}</td>
                  <td>{course.creditHours}</td>
                  <td>
                    <span className="courses-badge courses-badge-cat">
                      {courseService.getCourseCategoryLabel(course.category)}
                    </span>
                  </td>
                  <td>
                    <span
                      className={`courses-badge ${
                        course.isActive ? "courses-badge-active" : "courses-badge-inactive"
                      }`}
                    >
                      {course.isActive ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td className="col-actions">
                    <button
                      className="courses-action-btn edit"
                      onClick={() => openEdit(course)}
                      title="Edit"
                    >
                      <Edit2 size={13} />
                    </button>
                    <button
                      className="courses-action-btn delete"
                      onClick={() => setDeleteTarget(course)}
                      title="Delete"
                    >
                      <Trash2 size={13} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modalMode && (
        <div className="courses-modal-overlay" onClick={closeModal}>
          <div className="courses-modal" onClick={(e) => e.stopPropagation()}>
            <div className="courses-modal-header">
              <h2>{modalMode === "create" ? "Create Course" : "Edit Course"}</h2>
              <button className="courses-modal-close" onClick={closeModal}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleSave}>
              <div className="courses-modal-body">
                <div className="courses-form-group">
                  <label htmlFor="course-code">Code</label>
                  <input
                    id="course-code"
                    type="text"
                    className="courses-form-input"
                    value={form.code}
                    onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))}
                    placeholder="e.g. CS101"
                    disabled={modalMode === "edit"}
                    maxLength={32}
                    autoFocus
                  />
                  {modalMode === "edit" && (
                    <span className="courses-form-hint">Code cannot be changed after creation.</span>
                  )}
                </div>

                <div className="courses-form-group">
                  <label htmlFor="course-title">Title</label>
                  <input
                    id="course-title"
                    type="text"
                    className="courses-form-input"
                    value={form.title}
                    onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
                    placeholder="e.g. Introduction to Computer Science"
                    maxLength={200}
                  />
                </div>

                <div className="courses-form-group">
                  <label htmlFor="course-credit">Credit Hours</label>
                  <input
                    id="course-credit"
                    type="number"
                    className="courses-form-input"
                    value={form.creditHours}
                    min={0}
                    max={30}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, creditHours: Number(e.target.value) }))
                    }
                  />
                </div>

                <div className="courses-form-group">
                  <label htmlFor="course-cat">Category</label>
                  <select
                    id="course-cat"
                    className="courses-form-select"
                    value={form.category}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, category: Number(e.target.value) }))
                    }
                  >
                    {courseService.COURSE_CATEGORIES.map((c) => (
                      <option key={c.value} value={c.value}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                </div>

                {modalMode === "edit" && (
                  <label className="courses-checkbox-row">
                    <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={(e) =>
                        setForm((p) => ({ ...p, isActive: e.target.checked }))
                      }
                    />
                    Active
                  </label>
                )}

                {formError && <span className="courses-form-error">{formError}</span>}
              </div>
              <div className="courses-modal-footer">
                <button
                  type="button"
                  className="courses-btn courses-btn-outline"
                  onClick={closeModal}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="courses-btn courses-btn-primary"
                  disabled={saving}
                >
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
              <button className="courses-modal-close" onClick={() => setDeleteTarget(null)}>
                <X size={16} />
              </button>
            </div>
            <div className="courses-delete-body">
              <AlertTriangle size={36} className="courses-delete-icon" />
              <p>
                Delete <strong>{deleteTarget.code}</strong>?
              </p>
              <p className="courses-delete-hint">
                This will remove the course from the catalog. Plans referencing it may
                become inconsistent.
              </p>
            </div>
            <div className="courses-modal-footer">
              <button
                className="courses-btn courses-btn-outline"
                onClick={() => setDeleteTarget(null)}
                disabled={deleting}
              >
                Cancel
              </button>
              <button
                className="courses-btn courses-btn-danger"
                onClick={handleDelete}
                disabled={deleting}
              >
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
