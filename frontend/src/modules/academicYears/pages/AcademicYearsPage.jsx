import { useState, useEffect, useCallback, useMemo } from "react";
import { Plus, Edit2, Trash2, AlertTriangle, Calendar, Clock, CheckCircle, Lock, Unlock, BarChart3, Table2, Search, X, ChevronLeft, ChevronRight } from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import * as academicService from "../../../core/services/academicService";
import { useToast } from "../../../core/components/Toast";
import { SkeletonTable } from "../../../core/components/Skeleton";
import EmptyState from "../../../core/components/EmptyState";
import BulkActionBar from "../../../core/components/BulkActionBar";
import AcademicYearForm from "../components/AcademicYearForm";
import SemesterManager from "../components/SemesterManager";
import AcademicTimeline from "../components/AcademicTimeline";
import "../styles/academicYears.css";

const PAGE_SIZE = 10;

function AcademicYearsPage() {
  const { addToast } = useToast();
  const [allYears, setAllYears] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

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

  const [selectedIds, setSelectedIds] = useState(new Set());

  const fetchAcademicYears = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await academicService.fetchAcademicYears();
      const items = data?.items || data || [];
      setAllYears(Array.isArray(items) ? items : []);
    } catch (err) {
      setError(err.message || "Failed to load academic years");
      setAllYears([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAcademicYears();
  }, [fetchAcademicYears]);

  const filtered = useMemo(() => {
    if (!search.trim()) return allYears;
    const q = search.toLowerCase();
    return allYears.filter(y => y.name.toLowerCase().includes(q));
  }, [allYears, search]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));

  const displayYears = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  const handleSearch = () => {
    setPage(1);
    setSelectedIds(new Set());
  };

  const handleSearchKeyDown = (e) => {
    if (e.key === "Enter") handleSearch();
  };

  const handleViewChange = (mode) => {
    setViewMode(mode);
    if (mode === "timeline") setSelectedIds(new Set());
  };

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
        addToast("Academic year created", "success");
      } else if (modalMode === "edit" && editYear) {
        await academicService.updateAcademicYear(editYear.id, formData);
        addToast("Academic year updated", "success");
      }
      closeModal();
      await fetchAcademicYears();
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
      addToast("Academic year deleted", "success");
      setDeleteTarget(null);
      setSelectedIds(prev => { const n = new Set(prev); n.delete(deleteTarget.id); return n; });
      await fetchAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to delete academic year", "error");
    } finally {
      setDeleting(false);
    }
  };

  const handleSetCurrent = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.resolveCurrentAcademicYear();
      addToast(`${year.name} set as current year`, "success");
      await fetchAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to set current year", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleCloseYear = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.closeAcademicYear(year.id);
      addToast(`${year.name} closed`, "success");
      await fetchAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to close academic year", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleReopenYear = async (year) => {
    setLifecycleLoading(year.id);
    try {
      await academicService.reopenAcademicYear(year.id);
      addToast(`${year.name} reopened`, "success");
      await fetchAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to reopen academic year", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const openSemesterManager = (year) => {
    setSelectedYear(year);
    setShowSemesterManager(true);
  };

  const allSelected = displayYears.length > 0 && displayYears.every(y => selectedIds.has(y.id));
  const someSelected = displayYears.some(y => selectedIds.has(y.id));

  const handleSelectAll = () => {
    if (allSelected) setSelectedIds(new Set());
    else setSelectedIds(new Set(displayYears.map(y => y.id)));
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
      for (const id of ids) {
        await academicService.deleteAcademicYear(id);
      }
      addToast(`${ids.length} year(s) deleted`, "success");
      setSelectedIds(new Set());
      await fetchAcademicYears();
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

  if (showSemesterManager && selectedYear) {
    return (
      <SemesterManager
        academicYear={selectedYear}
        onBack={() => {
          setShowSemesterManager(false);
          setSelectedYear(null);
          fetchAcademicYears();
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
              onClick={() => handleViewChange("table")}
              title="Table View"
            >
              <Table2 size={16} />
            </button>
            <button
              className={`ay-toggle-btn ${viewMode === "timeline" ? "active" : ""}`}
              onClick={() => handleViewChange("timeline")}
              title="Timeline View"
            >
              <BarChart3 size={16} />
            </button>
          </div>
          <PermissionGate resource="academics.academic-years" minLevel={2}>
            <button className="btn-create" onClick={openCreate}>
              <Plus size={18} /> New Academic Year
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="alert alert-error">
          <AlertTriangle size={18} />
          {error}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }}
            onClick={() => fetchAcademicYears()}>
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <SkeletonTable rows={6} cols={6} />
      ) : allYears.length === 0 && viewMode === "table" ? (
        <EmptyState icon={Calendar} title="No Academic Years Found"
          message="Create your first academic year to get started"
          actionLabel="Create Academic Year" onAction={openCreate} />
      ) : viewMode === "timeline" ? (
        <AcademicTimeline
          years={allYears}
          onSetCurrent={handleSetCurrent}
          onCloseYear={handleCloseYear}
          onReopenYear={handleReopenYear}
          onDeleteYear={openDelete}
          onManageSemesters={openSemesterManager}
          lifecycleLoading={lifecycleLoading}
        />
      ) : (
        <>
          <div style={{ display: "flex", gap: 8, marginBottom: 16, alignItems: "center" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: "5px 10px", flex: 1, maxWidth: 320 }}>
              <Search size={13} style={{ color: "#6b7280" }} />
              <input
                type="text"
                placeholder="Search by name…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={handleSearchKeyDown}
                style={{ border: "none", outline: "none", flex: 1, fontSize: 13, fontFamily: "inherit", color: "#1a1f5e" }}
              />
              {search && (
                <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
                  onClick={() => { setSearch(""); setPage(1); setSelectedIds(new Set()); }}>
                  <X size={13} />
                </button>
              )}
            </div>
            <button className="btn-cancel" style={{ padding: "5px 12px", fontSize: 12 }}
              onClick={handleSearch}>
              <Search size={12} /> Search
            </button>
            {filtered.length !== allYears.length && (
              <span style={{ fontSize: 12, color: "#6b7280" }}>
                {filtered.length} of {allYears.length}
              </span>
            )}
          </div>

          {displayYears.length === 0 ? (
            <EmptyState icon={Calendar} title="No matching years"
              message="Try a different search term." />
          ) : (
            <>
              <div className="ay-table-wrapper">
                <table className="ay-table">
                  <thead>
                    <tr>
                      <th style={{ width: 36 }}>
                        <input type="checkbox" checked={allSelected}
                          ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }}
                          onChange={handleSelectAll} />
                      </th>
                      <th>Name</th>
                      <th>Start Date</th>
                      <th>End Date</th>
                      <th>Status</th>
                      <th>Record</th>
                      <th>Semesters</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {displayYears.map((year) => (
                      <tr key={year.id} className={year.isCurrent ? "current-year" : ""}>
                        <td onClick={e => e.stopPropagation()}>
                          <input type="checkbox" checked={selectedIds.has(year.id)} onChange={() => handleSelectOne(year.id)} />
                        </td>
                        <td className="name-cell">
                          <span className="year-name">{year.name}</span>
                          {year.isCurrent && <span className="badge current">Current</span>}
                          {year.isClosed && <span className="badge current" style={{ background: "#f3f4f6", color: "#6b7280" }}>Closed</span>}
                        </td>
                        <td>{year.startDate ? new Date(year.startDate).toLocaleDateString() : "—"}</td>
                        <td>{year.endDate ? new Date(year.endDate).toLocaleDateString() : "—"}</td>
                        <td>
                          <span className={`status-badge ${year.isCurrent ? "active" : "inactive"}`}>
                            {year.isCurrent ? "Active" : "Inactive"}
                          </span>
                        </td>
                        <td>
                          <span className={`status-badge ${!year.isClosed ? "active" : "inactive"}`}
                            style={{ background: year.isClosed ? "#f3f4f6" : "#dcfce7", color: year.isClosed ? "#6b7280" : "#166534" }}>
                            {year.isClosed ? "Closed" : "Open"}
                          </span>
                        </td>
                        <td>
                          <PermissionGate resource="academics.academic-years" minLevel={3}>
                            <button
                              className="btn-semester"
                              onClick={() => openSemesterManager(year)}
                              title="Manage Semesters"
                            >
                              <Clock size={16} /> Manage
                            </button>
                          </PermissionGate>
                        </td>
                        <td className="actions-cell">
                          {!year.isCurrent && !year.isClosed && (
                            <PermissionGate resource="academics.academic-years" minLevel={3}>
                              <button
                                className="btn-icon set-current"
                                onClick={() => handleSetCurrent(year)}
                                disabled={lifecycleLoading === year.id}
                                title="Set as Current Year"
                              >
                                <CheckCircle size={16} />
                              </button>
                            </PermissionGate>
                          )}
                          <PermissionGate resource="academics.academic-years" minLevel={3}>
                            <button
                              className="btn-icon edit"
                              onClick={() => openEdit(year)}
                              title="Edit"
                            >
                              <Edit2 size={16} />
                            </button>
                          </PermissionGate>
                          {year.isClosed ? (
                            <PermissionGate resource="academics.academic-years" minLevel={4}>
                              <button
                                className="btn-icon reopen"
                                onClick={() => handleReopenYear(year)}
                                disabled={lifecycleLoading === year.id}
                                title="Reopen Year"
                              >
                                <Unlock size={16} />
                              </button>
                            </PermissionGate>
                          ) : (
                            <PermissionGate resource="academics.academic-years" minLevel={3}>
                              <button
                                className="btn-icon close"
                                onClick={() => handleCloseYear(year)}
                                disabled={lifecycleLoading === year.id}
                                title="Close Year"
                              >
                                <Lock size={16} />
                              </button>
                            </PermissionGate>
                          )}
                          <PermissionGate resource="academics.academic-years" minLevel={5}>
                            <button
                              className="btn-icon delete"
                              onClick={() => openDelete(year)}
                              title="Delete"
                            >
                              <Trash2 size={16} />
                            </button>
                          </PermissionGate>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {totalPages > 1 && (
                <div className="pagination" style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 8, marginTop: 20 }}>
                  <button className="btn-cancel" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page <= 1} onClick={() => { setPage(p => p - 1); setSelectedIds(new Set()); }}>
                    <ChevronLeft size={14} /> Previous
                  </button>
                  {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
                    let pn;
                    if (totalPages <= 5) pn = i + 1;
                    else {
                      const mid = Math.floor(5 / 2);
                      if (page <= mid + 1) pn = i + 1;
                      else if (page >= totalPages - mid) pn = totalPages - 5 + i + 1;
                      else pn = page - mid + i;
                    }
                    return (
                      <button key={pn} className={`btn-cancel ${pn === page ? "btn-primary" : "btn-cancel"}`}
                        style={{ minWidth: 36, justifyContent: "center", padding: "4px 8px", fontSize: 12, border: pn === page ? "none" : undefined }}
                        onClick={() => { setPage(pn); setSelectedIds(new Set()); }}>
                        {pn}
                      </button>
                    );
                  })}
                  <button className="btn-cancel" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page >= totalPages} onClick={() => { setPage(p => p + 1); setSelectedIds(new Set()); }}>
                    Next <ChevronRight size={14} />
                  </button>
                </div>
              )}
            </>
          )}
        </>
      )}

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

      <BulkActionBar
        selectedCount={selectedIds.size}
        onClear={() => setSelectedIds(new Set())}
        actions={[
          {
            label: "Delete", icon: <Trash2 size={13} />, variant: "danger",
            onClick: handleBulkDelete, requiresPermission: true,
            permissionResource: "academics.academic-years", permissionLevel: 5,
          },
        ]}
      />
    </div>
  );
}

export default AcademicYearsPage;
