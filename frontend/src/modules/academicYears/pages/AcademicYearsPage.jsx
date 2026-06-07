import { useState, useEffect, useCallback } from "react";
import { Plus, Edit2, Trash2, AlertTriangle, Calendar, Clock, CheckCircle, Lock, Unlock, BarChart3, Table2 } from "lucide-react";
import * as academicService from "../../../core/services/academicService";
import AcademicYearForm from "../components/AcademicYearForm";
import SemesterManager from "../components/SemesterManager";
import AcademicTimeline from "../components/AcademicTimeline";
import "../styles/academicYears.css";

const PAGE_SIZE = 10;

function AcademicYearsPage() {
  const [academicYears, setAcademicYears] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [modalMode, setModalMode] = useState(null);
  const [editYear, setEditYear] = useState(null);
  const [formData, setFormData] = useState({ name: "", startDate: "", endDate: "", isCurrent: false });
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const [selectedYear, setSelectedYear] = useState(null);
  const [showSemesterManager, setShowSemesterManager] = useState(false);
  const [viewMode, setViewMode] = useState("table");
  const [lifecycleLoading, setLifecycleLoading] = useState(null);

  const fetchAcademicYears = useCallback(async (p = 1) => {
    setLoading(true);
    setError(null);
    try {
      const data = await academicService.fetchAcademicYears();
      // Handle pagination client-side since backend might not paginate
      const start = (p - 1) * PAGE_SIZE;
      const end = start + PAGE_SIZE;
      setAcademicYears((data?.items || data || []).slice(start, end));
      setTotalCount(data?.totalCount || data?.length || 0);
      setPage(p);
    } catch (err) {
      setError(err.message || "Failed to load academic years");
      setAcademicYears([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAcademicYears(1);
  }, [fetchAcademicYears]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const openCreate = () => {
    setModalMode("create");
    setEditYear(null);
    setFormData({ name: "", startDate: "", endDate: "", isCurrent: false });
    setFormError("");
  };

  const openEdit = (year) => {
    setModalMode("edit");
    setEditYear(year);
    setFormData({
      name: year.name,
      startDate: year.startDate ? year.startDate.split("T")[0] : "",
      endDate: year.endDate ? year.endDate.split("T")[0] : "",
      isCurrent: year.isCurrent,
    });
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditYear(null);
    setFormData({ name: "", startDate: "", endDate: "", isCurrent: false });
    setFormError("");
  };

  const validateForm = () => {
    const trimmed = formData.name.trim();
    if (!trimmed) {
      setFormError("Academic year name is required");
      return false;
    }
    if (trimmed.length < 3) {
      setFormError("Academic year name must be at least 3 characters");
      return false;
    }
    if (!formData.startDate) {
      setFormError("Start date is required");
      return false;
    }
    if (!formData.endDate) {
      setFormError("End date is required");
      return false;
    }
    if (new Date(formData.startDate) >= new Date(formData.endDate)) {
      setFormError("End date must be after start date");
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
        await academicService.createAcademicYear(formData);
      } else if (modalMode === "edit" && editYear) {
        await academicService.updateAcademicYear(editYear.id, formData);
      }
      closeModal();
      await fetchAcademicYears(page);
    } catch (err) {
      setFormError(err.message || "Failed to save academic year");
    } finally {
      setSaving(false);
    }
  };

  const openDelete = (year) => {
    setDeleteTarget(year);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await academicService.deleteAcademicYear(deleteTarget.id);
      setDeleteTarget(null);
      await fetchAcademicYears(page > 1 && academicYears.length === 1 ? page - 1 : page);
    } catch (err) {
      setError(err.message || "Failed to delete academic year");
    } finally {
      setDeleting(false);
    }
  };

  const handleSetCurrent = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.resolveCurrentAcademicYear(year.id);
      await fetchAcademicYears(page);
    } catch (err) {
      setError(err.message || "Failed to set current academic year");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleCloseYear = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.closeAcademicYear(year.id);
      await fetchAcademicYears(page);
    } catch (err) {
      setError(err.message || "Failed to close academic year");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleReopenYear = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.reopenAcademicYear(year.id);
      await fetchAcademicYears(page);
    } catch (err) {
      setError(err.message || "Failed to reopen academic year");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const openSemesterManager = (year) => {
    setSelectedYear(year);
    setShowSemesterManager(true);
  };

  if (showSemesterManager && selectedYear) {
    return (
      <SemesterManager
        academicYear={selectedYear}
        onBack={() => {
          setShowSemesterManager(false);
          setSelectedYear(null);
          fetchAcademicYears(page);
        }}
      />
    );
  }

  return (
    <div className="academic-years-container">
      <div className="ay-header">
        <div>
          <h1>Academic Years Management</h1>
          <p>Manage academic years and semesters for the institution</p>
        </div>
        <div className="ay-header-actions">
          <div className="ay-view-toggle">
            <button
              className={`ay-toggle-btn ${viewMode === "table" ? "active" : ""}`}
              onClick={() => setViewMode("table")}
              title="Table View"
            >
              <Table2 size={16} />
            </button>
            <button
              className={`ay-toggle-btn ${viewMode === "timeline" ? "active" : ""}`}
              onClick={() => setViewMode("timeline")}
              title="Timeline View"
            >
              <BarChart3 size={16} />
            </button>
          </div>
          <button className="btn-create" onClick={openCreate}>
            <Plus size={18} /> New Academic Year
          </button>
        </div>
      </div>

      {error && (
        <div className="alert alert-error">
          <AlertTriangle size={18} />
          {error}
        </div>
      )}

      {loading ? (
        <div className="loading-container">
          <div className="spinner"></div>
          <p>Loading academic years...</p>
        </div>
      ) : academicYears.length === 0 ? (
        <div className="empty-state">
          <Calendar size={48} />
          <h3>No Academic Years Found</h3>
          <p>Create your first academic year to get started</p>
          <button className="btn-primary" onClick={openCreate}>
            Create Academic Year
          </button>
        </div>
      ) : viewMode === "timeline" ? (
        <AcademicTimeline
          years={academicYears}
          onSetCurrent={handleSetCurrent}
          onCloseYear={handleCloseYear}
          onReopenYear={handleReopenYear}
          onDeleteYear={openDelete}
          onManageSemesters={openSemesterManager}
          lifecycleLoading={lifecycleLoading}
        />
      ) : (
        <>
          <div className="ay-table-wrapper">
            <table className="ay-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Start Date</th>
                  <th>End Date</th>
                  <th>Status</th>
                  <th>Semesters</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {academicYears.map((year) => (
                  <tr key={year.id} className={year.isCurrent ? "current-year" : ""}>
                    <td className="name-cell">
                      <span className="year-name">{year.name}</span>
                      {year.isCurrent && <span className="badge current">Current</span>}
                    </td>
                    <td>{year.startDate ? new Date(year.startDate).toLocaleDateString() : "—"}</td>
                    <td>{year.endDate ? new Date(year.endDate).toLocaleDateString() : "—"}</td>
                    <td>
                      <span className={`status-badge ${year.isCurrent ? "active" : "inactive"}`}>
                        {year.isCurrent ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>
                      <button
                        className="btn-semester"
                        onClick={() => openSemesterManager(year)}
                        title="Manage Semesters"
                      >
                        <Clock size={16} /> Manage
                      </button>
                    </td>
                    <td className="actions-cell">
                      {!year.isCurrent && (
                        <button
                          className="btn-icon set-current"
                          onClick={() => handleSetCurrent(year)}
                          disabled={lifecycleLoading === year.id}
                          title="Set as Current Year"
                        >
                          <CheckCircle size={16} />
                        </button>
                      )}
                      <button
                        className="btn-icon edit"
                        onClick={() => openEdit(year)}
                        title="Edit"
                      >
                        <Edit2 size={16} />
                      </button>
                      {year.isCurrent ? (
                        <button
                          className="btn-icon close"
                          onClick={() => handleCloseYear(year)}
                          disabled={lifecycleLoading === year.id}
                          title="Close Year"
                        >
                          <Lock size={16} />
                        </button>
                      ) : (
                        <button
                          className="btn-icon reopen"
                          onClick={() => handleReopenYear(year)}
                          disabled={lifecycleLoading === year.id}
                          title="Reopen Year"
                        >
                          <Unlock size={16} />
                        </button>
                      )}
                      <button
                        className="btn-icon delete"
                        onClick={() => openDelete(year)}
                        title="Delete"
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="pagination">
              <button
                disabled={page === 1}
                onClick={() => fetchAcademicYears(page - 1)}
              >
                Previous
              </button>
              <span>
                Page {page} of {totalPages}
              </span>
              <button
                disabled={page === totalPages}
                onClick={() => fetchAcademicYears(page + 1)}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}

      {/* Create/Edit Modal */}
      {modalMode && (
        <AcademicYearForm
          mode={modalMode}
          year={editYear}
          formData={formData}
          setFormData={setFormData}
          formError={formError}
          saving={saving}
          onSave={handleSave}
          onClose={closeModal}
        />
      )}

      {/* Delete Confirmation Modal */}
      {deleteTarget && (
        <div className="modal-overlay" onClick={() => !deleting && setDeleteTarget(null)}>
          <div className="modal-content delete-modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <AlertTriangle size={24} className="warning-icon" />
              <h3>Delete Academic Year</h3>
            </div>
            <p className="modal-body">
              Are you sure you want to delete <strong>{deleteTarget.name}</strong>? This action cannot be undone.
              {deleteTarget.semesters?.length > 0 && (
                <span className="warning-text">
                  This year has {deleteTarget.semesters.length} semester(s) that will also be deleted.
                </span>
              )}
            </p>
            <div className="modal-footer">
              <button
                className="btn-cancel"
                onClick={() => setDeleteTarget(null)}
                disabled={deleting}
              >
                Cancel
              </button>
              <button
                className="btn-delete"
                onClick={handleDelete}
                disabled={deleting}
              >
                {deleting ? "Deleting..." : "Delete"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default AcademicYearsPage;
