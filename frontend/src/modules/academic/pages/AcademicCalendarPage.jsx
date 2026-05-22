import { useState, useEffect, useCallback } from "react";
import {
  CalendarRange, Plus, Edit2, Trash2, X, AlertTriangle, RefreshCw,
  Calendar, CheckCircle, ChevronDown, ChevronRight, BookOpen, RotateCcw
} from "lucide-react";
import * as academicService from "../../../core/services/academicService";
import "../styles/academicCalendar.css";

function AcademicCalendarPage() {
  const [academicYears, setAcademicYears] = useState([]);
  const [semestersMap, setSemestersMap] = useState({});
  const [expandedYears, setExpandedYears] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeModal, setActiveModal] = useState(null);
  const [editTarget, setEditTarget] = useState(null);
  const [formData, setFormData] = useState({ name: "", startDate: "", endDate: "" });
  const [semesterForm, setSemesterForm] = useState({ name: "", order: "1", startDate: "", endDate: "" });
  const [formErrors, setFormErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [successMsg, setSuccessMsg] = useState("");

  const showSuccess = (msg) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(""), 3000);
  };

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const years = await academicService.fetchAcademicYears();
      setAcademicYears(years || []);
      const semPromises = (years || []).map(async (y) => {
        try {
          const sems = await academicService.fetchSemesters(y.id);
          return { yearId: y.id, semesters: sems || [] };
        } catch { return { yearId: y.id, semesters: [] }; }
      });
      const results = await Promise.all(semPromises);
      const map = {};
      results.forEach((r) => { map[r.yearId] = r.semesters; });
      setSemestersMap(map);
    } catch (err) {
      setError(err.message || "Failed to load academic data");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const toggleYear = (yearId) => {
    setExpandedYears((prev) => ({ ...prev, [yearId]: !prev[yearId] }));
  };

  const openCreateYear = () => {
    setActiveModal("createYear");
    setEditTarget(null);
    setFormData({ name: "", startDate: "", endDate: "" });
    setFormErrors({});
  };

  const openEditYear = (year) => {
    setActiveModal("editYear");
    setEditTarget(year);
    setFormData({
      name: year.name || "",
      startDate: year.startDate ? year.startDate.slice(0, 10) : "",
      endDate: year.endDate ? year.endDate.slice(0, 10) : "",
    });
    setFormErrors({});
  };

  const openCreateSemester = (yearId) => {
    setActiveModal("createSemester");
    setEditTarget({ academicYearId: yearId });
    setSemesterForm({ name: "", order: "1", startDate: "", endDate: "" });
    setFormErrors({});
  };

  const openEditSemester = (semester, yearId) => {
    setActiveModal("editSemester");
    setEditTarget({ ...semester, academicYearId: yearId });
    setSemesterForm({
      name: semester.name || "",
      order: String(semester.order || 1),
      startDate: semester.startDate ? semester.startDate.slice(0, 10) : "",
      endDate: semester.endDate ? semester.endDate.slice(0, 10) : "",
    });
    setFormErrors({});
  };

  const closeModal = () => {
    setActiveModal(null);
    setEditTarget(null);
    setFormErrors({});
  };

  const validateYearForm = () => {
    const errs = {};
    if (!formData.name.trim()) errs.name = "Name is required";
    if (!formData.startDate) errs.startDate = "Start date is required";
    if (!formData.endDate) errs.endDate = "End date is required";
    if (formData.startDate && formData.endDate && formData.startDate >= formData.endDate)
      errs.endDate = "End date must be after start date";
    setFormErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const validateSemesterForm = () => {
    const errs = {};
    if (!semesterForm.name.trim()) errs.name = "Name is required";
    if (!semesterForm.startDate) errs.startDate = "Start date is required";
    if (!semesterForm.endDate) errs.endDate = "End date is required";
    if (semesterForm.startDate && semesterForm.endDate && semesterForm.startDate >= semesterForm.endDate)
      errs.endDate = "End date must be after start date";
    setFormErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleCreateYear = async (e) => {
    e.preventDefault();
    if (!validateYearForm()) return;
    setSaving(true);
    try {
      await academicService.createAcademicYear({
        name: formData.name.trim(),
        startDate: formData.startDate,
        endDate: formData.endDate,
      });
      showSuccess("Academic year created successfully");
      closeModal();
      await fetchData();
    } catch (err) {
      setFormErrors({ submit: err.message || "Failed to create academic year" });
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateYear = async (e) => {
    e.preventDefault();
    if (!validateYearForm() || !editTarget) return;
    setSaving(true);
    try {
      await academicService.updateAcademicYear(editTarget.id, {
        name: formData.name.trim() || undefined,
        startDate: formData.startDate || undefined,
        endDate: formData.endDate || undefined,
      });
      showSuccess("Academic year updated successfully");
      closeModal();
      await fetchData();
    } catch (err) {
      setFormErrors({ submit: err.message || "Failed to update academic year" });
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteYear = async (year) => {
    if (!window.confirm(`Delete academic year "${year.name}"? This action cannot be undone.`)) return;
    setSaving(true);
    try {
      await academicService.deleteAcademicYear(year.id);
      showSuccess("Academic year deleted");
      await fetchData();
    } catch (err) {
      setError(err.message || "Failed to delete academic year");
    } finally {
      setSaving(false);
    }
  };

  const handleCreateSemester = async (e) => {
    e.preventDefault();
    if (!validateSemesterForm()) return;
    setSaving(true);
    try {
      await academicService.createSemester({
        academicYearId: editTarget.academicYearId,
        name: semesterForm.name.trim(),
        order: parseInt(semesterForm.order),
        startDate: semesterForm.startDate,
        endDate: semesterForm.endDate,
      });
      showSuccess("Semester created successfully");
      closeModal();
      await fetchData();
    } catch (err) {
      setFormErrors({ submit: err.message || "Failed to create semester" });
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateSemester = async (e) => {
    e.preventDefault();
    if (!validateSemesterForm() || !editTarget) return;
    setSaving(true);
    try {
      await academicService.updateSemester(editTarget.id, {
        name: semesterForm.name.trim() || undefined,
        order: semesterForm.order ? parseInt(semesterForm.order) : undefined,
        startDate: semesterForm.startDate || undefined,
        endDate: semesterForm.endDate || undefined,
      });
      showSuccess("Semester updated successfully");
      closeModal();
      await fetchData();
    } catch (err) {
      setFormErrors({ submit: err.message || "Failed to update semester" });
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteSemester = async (semester) => {
    if (!window.confirm(`Delete semester "${semester.name}"?`)) return;
    try {
      await academicService.deleteSemester(semester.id);
      showSuccess("Semester deleted");
      await fetchData();
    } catch (err) {
      setError(err.message || "Failed to delete semester");
    }
  };

  const handleResolveCurrentYear = async () => {
    try {
      await academicService.resolveCurrentAcademicYear();
      showSuccess("Academic year resolution triggered");
      await fetchData();
    } catch (err) {
      setError(err.message || "Resolution failed");
    }
  };

  const handleResolveCurrentSemester = async () => {
    try {
      await academicService.resolveCurrentSemester();
      showSuccess("Semester resolution triggered");
      await fetchData();
    } catch (err) {
      setError(err.message || "Resolution failed");
    }
  };

  const resolveModalButtonLabel = (modalName, createLabel) => {
    if (saving) return "Saving…";
    if (activeModal === modalName) return createLabel;
    return "Save Changes";
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleDateString("en-EG", {
        year: "numeric", month: "short", day: "numeric",
      });
    } catch { return "—"; }
  };

  if (loading && academicYears.length === 0) {
    return (
      <div className="ac-page">
        <div className="ac-loading">
          <div className="ac-spinner" />
          <p>Loading academic calendar…</p>
        </div>
      </div>
    );
  }

  if (error && academicYears.length === 0) {
    return (
      <div className="ac-page">
        <div className="ac-header">
          <div className="ac-header-left">
            <CalendarRange size={20} />
            <div><h1>Academic Calendar</h1><p>Manage academic years and semesters</p></div>
          </div>
        </div>
        <div className="ac-error">
          <AlertTriangle size={36} />
          <h3>Failed to load academic data</h3>
          <p>{error}</p>
          <button className="ac-btn ac-btn-outline" onClick={fetchData}><RefreshCw size={13} /> Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="ac-page">
      <div className="ac-header">
        <div className="ac-header-left">
          <div className="ac-header-icon"><CalendarRange size={20} /></div>
          <div>
            <h1>Academic Calendar</h1>
            <p>Manage academic years and their semesters</p>
          </div>
        </div>
        <div className="ac-header-actions">
          <button className="ac-btn ac-btn-soft" onClick={handleResolveCurrentYear} title="Auto-resolve current academic year">
            <RotateCcw size={12} /> Resolve Year
          </button>
          <button className="ac-btn ac-btn-soft" onClick={handleResolveCurrentSemester} title="Auto-resolve current semester">
            <RotateCcw size={12} /> Resolve Semester
          </button>
          <button className="ac-btn ac-btn-primary" onClick={openCreateYear}>
            <Plus size={14} /> New Year
          </button>
        </div>
      </div>

      {error && (
        <div className="ac-banner ac-banner-error">
          <AlertTriangle size={14} /><span>{error}</span>
          <button onClick={() => setError(null)}><X size={12} /></button>
        </div>
      )}

      {successMsg && (
        <div className="ac-banner ac-banner-success">
          <CheckCircle size={14} /><span>{successMsg}</span>
        </div>
      )}

      {academicYears.length === 0 ? (
        <div className="ac-empty">
          <CalendarRange size={40} />
          <h3>No academic years found</h3>
          <p>Create your first academic year to get started.</p>
          <button className="ac-btn ac-btn-primary" onClick={openCreateYear}>
            <Plus size={14} /> Create Academic Year
          </button>
        </div>
      ) : (
        <div className="ac-list">
          {academicYears.map((year) => {
            const sems = semestersMap[year.id] || [];
            const isExpanded = expandedYears[year.id] !== false;

            return (
              <div key={year.id} className={`ac-year-card ${year.isCurrent ? "is-current" : ""}`}>
                <div className="ac-year-header" onClick={() => toggleYear(year.id)}>
                  <div className="ac-year-toggle">
                    {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                  </div>
                  <div className="ac-year-info">
                    <h3>{year.name}</h3>
                    <div className="ac-year-meta">
                      <span>{formatDate(year.startDate)} — {formatDate(year.endDate)}</span>
                      {year.isCurrent && <span className="ac-badge ac-badge-current">Current</span>}
                    </div>
                  </div>
                  <div className="ac-year-actions">
                    <button className="ac-icon-btn" onClick={(e) => { e.stopPropagation(); openEditYear(year); }} title="Edit year">
                      <Edit2 size={12} />
                    </button>
                    <button className="ac-icon-btn ac-icon-btn-danger" onClick={(e) => { e.stopPropagation(); handleDeleteYear(year); }} title="Delete year">
                      <Trash2 size={12} />
                    </button>
                    <button className="ac-btn ac-btn-small" onClick={(e) => { e.stopPropagation(); openCreateSemester(year.id); }}>
                      <Plus size={11} /> Add Semester
                    </button>
                  </div>
                </div>

                {isExpanded && (
                  <div className="ac-semester-list">
                    {sems.length === 0 ? (
                      <div className="ac-no-semesters">
                        <BookOpen size={16} />
                        <span>No semesters defined for this year</span>
                      </div>
                    ) : (
                      sems.map((sem) => (
                        <div key={sem.id} className={`ac-semester-item ${sem.isCurrent ? "is-current" : ""}`}>
                          <div className="ac-semester-info">
                            <span className="ac-semester-order">S{sem.order}</span>
                            <div>
                              <strong>{sem.name}</strong>
                              <span className="ac-semester-dates">{formatDate(sem.startDate)} — {formatDate(sem.endDate)}</span>
                            </div>
                            {sem.isCurrent && <span className="ac-badge ac-badge-current">Active</span>}
                          </div>
                          <div className="ac-semester-actions">
                            <button className="ac-icon-btn" onClick={() => openEditSemester(sem, year.id)} title="Edit semester">
                              <Edit2 size={11} />
                            </button>
                            <button className="ac-icon-btn ac-icon-btn-danger" onClick={() => handleDeleteSemester(sem)} title="Delete semester">
                              <Trash2 size={11} />
                            </button>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Create/Edit Year Modal */}
      {(activeModal === "createYear" || activeModal === "editYear") && (
        <div className="ac-modal-overlay" onClick={closeModal}>
          <div className="ac-modal" onClick={(e) => e.stopPropagation()}>
            <div className="ac-modal-header">
              <h2>{activeModal === "createYear" ? "Create Academic Year" : "Edit Academic Year"}</h2>
              <button className="ac-modal-close" onClick={closeModal}><X size={16} /></button>
            </div>
            <form onSubmit={activeModal === "createYear" ? handleCreateYear : handleUpdateYear}>
              <div className="ac-modal-body">
                {formErrors.submit && <div className="ac-form-error-banner"><AlertTriangle size={12} />{formErrors.submit}</div>}
                <div className="ac-form-group">
                  <label>Year Name</label>
                  <input type="text" className={`ac-form-input ${formErrors.name ? "error" : ""}`}
                    value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    placeholder="e.g. 2024-2025" autoFocus maxLength={100} />
                  {formErrors.name && <span className="ac-form-error">{formErrors.name}</span>}
                </div>
                <div className="ac-form-row">
                  <div className="ac-form-group">
                    <label>Start Date</label>
                    <input type="date" className={`ac-form-input ${formErrors.startDate ? "error" : ""}`}
                      value={formData.startDate} onChange={(e) => setFormData({ ...formData, startDate: e.target.value })} />
                    {formErrors.startDate && <span className="ac-form-error">{formErrors.startDate}</span>}
                  </div>
                  <div className="ac-form-group">
                    <label>End Date</label>
                    <input type="date" className={`ac-form-input ${formErrors.endDate ? "error" : ""}`}
                      value={formData.endDate} onChange={(e) => setFormData({ ...formData, endDate: e.target.value })} />
                    {formErrors.endDate && <span className="ac-form-error">{formErrors.endDate}</span>}
                  </div>
                </div>
              </div>
              <div className="ac-modal-footer">
                <button type="button" className="ac-btn ac-btn-ghost" onClick={closeModal}>Cancel</button>
                <button type="submit" className="ac-btn ac-btn-primary" disabled={saving}>
                  {resolveModalButtonLabel("createYear", "Create")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Create/Edit Semester Modal */}
      {(activeModal === "createSemester" || activeModal === "editSemester") && (
        <div className="ac-modal-overlay" onClick={closeModal}>
          <div className="ac-modal" onClick={(e) => e.stopPropagation()}>
            <div className="ac-modal-header">
              <h2>{activeModal === "createSemester" ? "Add Semester" : "Edit Semester"}</h2>
              <button className="ac-modal-close" onClick={closeModal}><X size={16} /></button>
            </div>
            <form onSubmit={activeModal === "createSemester" ? handleCreateSemester : handleUpdateSemester}>
              <div className="ac-modal-body">
                {formErrors.submit && <div className="ac-form-error-banner"><AlertTriangle size={12} />{formErrors.submit}</div>}
                <div className="ac-form-group">
                  <label>Semester Name</label>
                  <input type="text" className={`ac-form-input ${formErrors.name ? "error" : ""}`}
                    value={semesterForm.name} onChange={(e) => setSemesterForm({ ...semesterForm, name: e.target.value })}
                    placeholder="e.g. Fall Semester" autoFocus maxLength={100} />
                  {formErrors.name && <span className="ac-form-error">{formErrors.name}</span>}
                </div>
                <div className="ac-form-group">
                  <label>Order (within academic year)</label>
                  <input type="number" className={`ac-form-input ${formErrors.order ? "error" : ""}`}
                    value={semesterForm.order} onChange={(e) => setSemesterForm({ ...semesterForm, order: e.target.value })}
                    min="1" max="12" />
                  {formErrors.order && <span className="ac-form-error">{formErrors.order}</span>}
                  <span className="ac-form-hint">1 = first semester, 2 = second, etc.</span>
                </div>
                <div className="ac-form-row">
                  <div className="ac-form-group">
                    <label>Start Date</label>
                    <input type="date" className={`ac-form-input ${formErrors.startDate ? "error" : ""}`}
                      value={semesterForm.startDate} onChange={(e) => setSemesterForm({ ...semesterForm, startDate: e.target.value })} />
                    {formErrors.startDate && <span className="ac-form-error">{formErrors.startDate}</span>}
                  </div>
                  <div className="ac-form-group">
                    <label>End Date</label>
                    <input type="date" className={`ac-form-input ${formErrors.endDate ? "error" : ""}`}
                      value={semesterForm.endDate} onChange={(e) => setSemesterForm({ ...semesterForm, endDate: e.target.value })} />
                    {formErrors.endDate && <span className="ac-form-error">{formErrors.endDate}</span>}
                  </div>
                </div>
              </div>
              <div className="ac-modal-footer">
                <button type="button" className="ac-btn ac-btn-ghost" onClick={closeModal}>Cancel</button>
                <button type="submit" className="ac-btn ac-btn-primary" disabled={saving}>
                  {resolveModalButtonLabel("createSemester", "Add")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default AcademicCalendarPage;
