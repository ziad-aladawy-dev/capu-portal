import { useState, useEffect, useCallback } from "react";
import { Plus, Edit2, Trash2, X, AlertTriangle, ArrowLeft } from "lucide-react";
import * as academicService from "../../../core/services/academicService";
import SemesterForm from "./SemesterForm";

function SemesterManager({ academicYear, onBack }) {
  const [semesters, setSemesters] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [modalMode, setModalMode] = useState(null);
  const [editSemester, setEditSemester] = useState(null);
  const [formData, setFormData] = useState({ name: "", startDate: "", endDate: "" });
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const fetchSemesters = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await academicService.fetchSemesters(academicYear.id);
      setSemesters(data?.items || data || []);
    } catch (err) {
      setError(err.message || "Failed to load semesters");
      setSemesters([]);
    } finally {
      setLoading(false);
    }
  }, [academicYear.id]);

  useEffect(() => {
    fetchSemesters();
  }, [fetchSemesters]);

  const openCreate = () => {
    setModalMode("create");
    setEditSemester(null);
    setFormData({ name: "", startDate: "", endDate: "" });
    setFormError("");
  };

  const openEdit = (semester) => {
    setModalMode("edit");
    setEditSemester(semester);
    setFormData({
      name: semester.name,
      startDate: semester.startDate ? semester.startDate.split("T")[0] : "",
      endDate: semester.endDate ? semester.endDate.split("T")[0] : "",
    });
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditSemester(null);
    setFormData({ name: "", startDate: "", endDate: "" });
    setFormError("");
  };

  const validateForm = () => {
    const trimmed = formData.name.trim();
    if (!trimmed) {
      setFormError("Semester name is required");
      return false;
    }
    if (trimmed.length < 2) {
      setFormError("Semester name must be at least 2 characters");
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
        await academicService.createSemester({
          ...formData,
          academicYearId: academicYear.id,
        });
      } else if (modalMode === "edit" && editSemester) {
        await academicService.updateSemester(editSemester.id, formData);
      }
      closeModal();
      await fetchSemesters();
    } catch (err) {
      setFormError(err.message || "Failed to save semester");
    } finally {
      setSaving(false);
    }
  };

  const openDelete = (semester) => {
    setDeleteTarget(semester);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await academicService.deleteSemester(deleteTarget.id);
      setDeleteTarget(null);
      await fetchSemesters();
    } catch (err) {
      setError(err.message || "Failed to delete semester");
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="semester-manager-container">
      <div className="sm-header">
        <button className="btn-back" onClick={onBack}>
          <ArrowLeft size={20} /> Back to Academic Years
        </button>
        <div>
          <h1>Manage Semesters</h1>
          <p>{academicYear.name}</p>
        </div>
        <button className="btn-create" onClick={openCreate}>
          <Plus size={18} /> New Semester
        </button>
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
          <p>Loading semesters...</p>
        </div>
      ) : semesters.length === 0 ? (
        <div className="empty-state">
          <p>No semesters yet for this academic year</p>
          <button className="btn-primary" onClick={openCreate}>
            Create First Semester
          </button>
        </div>
      ) : (
        <div className="sm-table-wrapper">
          <table className="sm-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Start Date</th>
                <th>End Date</th>
                <th>Duration</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {semesters.map((semester) => {
                const start = new Date(semester.startDate);
                const end = new Date(semester.endDate);
                const days = Math.floor((end - start) / (1000 * 60 * 60 * 24));
                return (
                  <tr key={semester.id}>
                    <td>{semester.name}</td>
                    <td>{start.toLocaleDateString()}</td>
                    <td>{end.toLocaleDateString()}</td>
                    <td>{days} days</td>
                    <td className="actions-cell">
                      <button
                        className="btn-icon edit"
                        onClick={() => openEdit(semester)}
                        title="Edit"
                      >
                        <Edit2 size={16} />
                      </button>
                      <button
                        className="btn-icon delete"
                        onClick={() => openDelete(semester)}
                        title="Delete"
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Create/Edit Modal */}
      {modalMode && (
        <SemesterForm
          mode={modalMode}
          semester={editSemester}
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
              <h3>Delete Semester</h3>
            </div>
            <p className="modal-body">
              Are you sure you want to delete <strong>{deleteTarget.name}</strong>?
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

export default SemesterManager;
