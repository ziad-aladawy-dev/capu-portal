import { useState, useMemo } from "react";
import {
  Plus, Edit2, Trash2, AlertTriangle, Calendar, Clock, Search, X,
  ChevronLeft, ChevronRight, ArrowLeft,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useToast } from "../../../core/components/Toast";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import CascadeImpactModal from "../../../core/components/CascadeImpactModal";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import AcademicYearWizard from "../components/AcademicYearWizard";
import AcademicTimeline from "../components/AcademicTimeline";
import {
  useAcademicYears, useCreateAcademicYear, useUpdateAcademicYear,
  useDeleteAcademicYear, useCloseAcademicYear, useReopenAcademicYear,
  useSetCurrentAcademicYear, useSemesters,
  useCreateSemester, useUpdateSemester, useDeleteSemester,
  useCloseSemester, useOpenSemester,
} from "../../../core/query/useAcademicYears";
import { getSemesterDateBounds } from "../../../core/utils/dateConstraints";
import useAcademicStore from "../../../core/stores/useAcademicStore";
import "../styles/academicYears.css";

const PAGE_SIZE = 10;

function AcademicYearsPage() {
  const { addToast } = useToast();
  const refreshAcademicYears = useAcademicStore((s) => s.refreshAcademicYears);

  const { data: allYears = [], isLoading, error: queryError, refetch } = useAcademicYears();
  const createYear = useCreateAcademicYear();
  const updateYear = useUpdateAcademicYear();
  const deleteYear = useDeleteAcademicYear();
  const closeYear = useCloseAcademicYear();
  const reopenYear = useReopenAcademicYear();
  const setCurrentYear = useSetCurrentAcademicYear();

  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [viewMode, setViewMode] = useState("table");
  const [compact, setCompact] = useState(false);

  const [showWizard, setShowWizard] = useState(false);

  const [editYearDrawer, setEditYearDrawer] = useState(null);
  const [editYearForm, setEditYearForm] = useState({ name: "", startDate: "", endDate: "", isCurrent: false });
  const [editYearError, setEditYearError] = useState("");

  const [cascadeTarget, setCascadeTarget] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);

  const [semesterYear, setSemesterYear] = useState(null);

  const [semDrawer, setSemDrawer] = useState(null);
  const [editSem, setEditSem] = useState(null);
  const [semForm, setSemForm] = useState({ name: "", startDate: "", endDate: "" });
  const [semFormError, setSemFormError] = useState("");
  const [semDelete, setSemDelete] = useState(null);

  const { data: semesters = [], isLoading: semsLoading } = useSemesters(semesterYear?.id);

  const createSem = useCreateSemester();
  const updateSem = useUpdateSemester();
  const deleteSem = useDeleteSemester();

  const semesterDateBounds = useMemo(() => {
    if (!semesterYear) return { minDate: "", maxDate: "" };
    return getSemesterDateBounds(semesterYear.startDate, semesterYear.endDate);
  }, [semesterYear]);

  const filtered = useMemo(() => {
    if (!search.trim()) return allYears;
    const q = search.toLowerCase();
    return allYears.filter((y) => y.name?.toLowerCase().includes(q));
  }, [allYears, search]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const displayYears = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); };

  const handleWizardSave = async (data) => {
    try {
      await createYear.mutateAsync({
        name: data.name,
        startDate: data.startDate,
        endDate: data.endDate,
        isCurrent: data.isCurrent,
      });
      if (data.semesters?.length > 0) {
        for (const sem of data.semesters) {
          try {
            await createSem.mutateAsync({ ...sem, academicYearId: null });
          } catch {}
        }
      }
      addToast("Academic year created with semesters", "success");
      setShowWizard(false);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to create academic year", "error");
    }
  };

  const openEditYearDrawer = (year) => {
    setEditYearDrawer(year);
    setEditYearForm({
      name: year.name,
      startDate: year.startDate?.split("T")[0] || "",
      endDate: year.endDate?.split("T")[0] || "",
      isCurrent: year.isCurrent,
    });
    setEditYearError("");
  };

  const handleEditYearSave = async () => {
    if (!editYearForm.name.trim()) { setEditYearError("Name is required"); return; }
    if (!editYearForm.startDate) { setEditYearError("Start date is required"); return; }
    if (!editYearForm.endDate) { setEditYearError("End date is required"); return; }
    if (new Date(editYearForm.startDate) >= new Date(editYearForm.endDate)) {
      setEditYearError("End date must be after start date"); return;
    }
    try {
      await updateYear.mutateAsync({ id: editYearDrawer.id, ...editYearForm });
      addToast("Academic year updated", "success");
      setEditYearDrawer(null);
      refreshAcademicYears();
    } catch (err) {
      setEditYearError(err.message || "Failed to update");
    }
  };

  const handleDeleteYear = async () => {
    if (!confirmAction) return;
    try {
      await deleteYear.mutateAsync(confirmAction.id);
      addToast("Academic year deleted", "success");
      setSelectedIds((prev) => { const n = new Set(prev); n.delete(confirmAction.id); return n; });
      setConfirmAction(null);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to delete", "error");
      setConfirmAction(null);
    }
  };

  const handleSetCurrent = async (year) => {
    try {
      await setCurrentYear.mutateAsync();
      addToast(`${year.name} set as current year`, "success");
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to set current year", "error");
    }
  };

  const handleCloseYear = async () => {
    if (!cascadeTarget) return;
    try {
      await closeYear.mutateAsync(cascadeTarget.id);
      addToast(`${cascadeTarget.name} closed`, "success");
      setCascadeTarget(null);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to close year", "error");
    }
  };

  const handleReopenYear = async (year) => {
    try {
      await reopenYear.mutateAsync(year.id);
      addToast(`${year.name} reopened`, "success");
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || "Failed to reopen", "error");
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      for (const id of ids) {
        await deleteYear.mutateAsync(id);
      }
      addToast(`${ids.length} year(s) deleted`, "success");
      setSelectedIds(new Set());
      refreshAcademicYears();
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

  const openCreateSemDrawer = () => {
    setSemDrawer("create");
    setEditSem(null);
    setSemForm({ name: "", startDate: "", endDate: "" });
    setSemFormError("");
  };

  const openEditSemDrawer = (sem) => {
    setSemDrawer("edit");
    setEditSem(sem);
    setSemForm({
      name: sem.name,
      startDate: sem.startDate?.split("T")[0] || "",
      endDate: sem.endDate?.split("T")[0] || "",
    });
    setSemFormError("");
  };

  const closeSemDrawer = () => {
    setSemDrawer(null);
    setEditSem(null);
    setSemFormError("");
  };

  const validateSemForm = () => {
    if (!semForm.name.trim()) { setSemFormError("Semester name is required"); return false; }
    if (!semForm.startDate) { setSemFormError("Start date is required"); return false; }
    if (!semForm.endDate) { setSemFormError("End date is required"); return false; }
    if (new Date(semForm.startDate) >= new Date(semForm.endDate)) {
      setSemFormError("End date must be after start date"); return false;
    }
    if (semesterDateBounds.minDate && semForm.startDate < semesterDateBounds.minDate) {
      setSemFormError("Start date must be within the academic year"); return false;
    }
    if (semesterDateBounds.maxDate && semForm.endDate > semesterDateBounds.maxDate) {
      setSemFormError("End date must be within the academic year"); return false;
    }
    setSemFormError("");
    return true;
  };

  const handleSemSave = async (e) => {
    e.preventDefault();
    if (!validateSemForm()) return;
    try {
      if (semDrawer === "create") {
        await createSem.mutateAsync({ ...semForm, academicYearId: semesterYear.id });
        addToast("Semester created", "success");
      } else if (semDrawer === "edit" && editSem) {
        await updateSem.mutateAsync({ id: editSem.id, ...semForm });
        addToast("Semester updated", "success");
      }
      closeSemDrawer();
    } catch (err) {
      setSemFormError(err.message || "Failed to save semester");
    }
  };

  const handleDeleteSem = async () => {
    if (!semDelete) return;
    try {
      await deleteSem.mutateAsync({ id: semDelete.id, academicYearId: semesterYear.id });
      setSemDelete(null);
    } catch (err) {
      addToast(err.message || "Failed to delete semester", "error");
    }
  };

  // Semester sub-view
  if (semesterYear) {
    return (
      <div className="academic-years-container">
        <div className="ay-header">
          <div className="ay-header-left">
            <button className="btn-back" onClick={() => setSemesterYear(null)} style={{ marginRight: 12 }}>
              <ArrowLeft size={20} />
            </button>
            <div>
              <h1>Manage Semesters — {semesterYear.name}</h1>
              <p>Create, edit, and manage semesters for this academic year</p>
            </div>
          </div>
          <PermissionGate resource="academics.academic-years" minLevel={2}>
            <button className="btn-create" onClick={openCreateSemDrawer}>
              <Plus size={18} /> New Semester
            </button>
          </PermissionGate>
        </div>

        {semsLoading ? (
          <div className="loading-container"><div className="spinner" /><p>Loading semesters...</p></div>
        ) : semesters.length === 0 ? (
          <div className="empty-state">
            <Clock size={40} />
            <p>No semesters yet for this academic year</p>
            <PermissionGate resource="academics.academic-years" minLevel={2}>
              <button className="btn-primary" onClick={openCreateSemDrawer}>Create First Semester</button>
            </PermissionGate>
          </div>
        ) : (
          <DataTable
            columns={[
              { key: "name", label: "Name", render: (v) => <span className="year-name">{v}</span> },
              { key: "startDate", label: "Start Date", render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "endDate", label: "End Date", render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "startDate", label: "Duration", render: (_, row) => {
                const start = new Date(row.startDate);
                const end = new Date(row.endDate);
                const days = Math.floor((end - start) / (1000 * 60 * 60 * 24));
                return <span style={{ fontFamily: "Space Mono, monospace" }}>{days} days</span>;
              }},
              { key: "actions", label: "Actions", render: (_, row) => (
                <div style={{ display: "flex", gap: 4 }}>
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button className="btn-icon edit" onClick={() => openEditSemDrawer(row)} title="Edit"><Edit2 size={16} /></button>
                  </PermissionGate>
                  <PermissionGate resource="academics.academic-years" minLevel={5}>
                    <button className="btn-icon delete" onClick={() => setSemDelete(row)} title="Delete"><Trash2 size={16} /></button>
                  </PermissionGate>
                </div>
              ), nowrap: true },
            ]}
            data={semesters}
            compact={compact}
            onCompactToggle={() => setCompact((c) => !c)}
          />
        )}

        <Drawer
          open={!!semDrawer}
          onClose={closeSemDrawer}
          title={semDrawer === "create" ? "Create Semester" : "Edit Semester"}
          width={420}
          loading={createSem.isPending || updateSem.isPending}
          footer={
            <>
              <button className="btn-cancel" onClick={closeSemDrawer}>Cancel</button>
              <button className="btn-primary" onClick={handleSemSave}>
                {createSem.isPending || updateSem.isPending ? "Saving..." : semDrawer === "create" ? "Create" : "Update"}
              </button>
            </>
          }
        >
          {semFormError && (
            <div role="alert" style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16 }}>
              {semFormError}
            </div>
          )}
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <div className="form-group">
              <label htmlFor="sem-name">Semester Name *</label>
              <input id="sem-name" type="text" value={semForm.name}
                onChange={(e) => setSemForm((p) => ({ ...p, name: e.target.value }))}
                placeholder="e.g. Fall, Spring, Summer"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
              <small style={{ color: "#6b7280", fontSize: 10 }}>
                Year bounds: {semesterDateBounds.minDate} → {semesterDateBounds.maxDate}
              </small>
            </div>
            <div style={{ display: "flex", gap: 12 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="sem-start">Start Date *</label>
                <input id="sem-start" type="date" value={semForm.startDate}
                  min={semesterDateBounds.minDate || undefined}
                  max={semForm.endDate || semesterDateBounds.maxDate || undefined}
                  onChange={(e) => setSemForm((p) => ({ ...p, startDate: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="sem-end">End Date *</label>
                <input id="sem-end" type="date" value={semForm.endDate}
                  min={semForm.startDate || semesterDateBounds.minDate || undefined}
                  max={semesterDateBounds.maxDate || undefined}
                  onChange={(e) => setSemForm((p) => ({ ...p, endDate: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
              </div>
            </div>
          </div>
        </Drawer>

        <ConfirmDialog
          open={!!semDelete}
          onClose={() => setSemDelete(null)}
          onConfirm={handleDeleteSem}
          title="Delete Semester"
          message={`Are you sure you want to delete ${semDelete?.name}?`}
          detail="This action cannot be undone."
          confirmLabel="Delete"
          variant="danger"
        />
      </div>
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
          <div className="ay-view-toggle" role="group" aria-label="View mode">
            <button
              className={`ay-toggle-btn ${viewMode === "table" ? "active" : ""}`}
              onClick={() => { setViewMode("table"); setSelectedIds(new Set()); }}
              title="Table View"
              aria-pressed={viewMode === "table"}
            ><Clock size={16} /></button>
            <button
              className={`ay-toggle-btn ${viewMode === "timeline" ? "active" : ""}`}
              onClick={() => { setViewMode("timeline"); setSelectedIds(new Set()); }}
              title="Timeline View"
              aria-pressed={viewMode === "timeline"}
            ><ChevronLeft size={16} /></button>
          </div>
          <PermissionGate resource="academics.academic-years" minLevel={2}>
            <button className="btn-create" onClick={() => setShowWizard(true)}>
              <Plus size={18} /> New Academic Year
            </button>
          </PermissionGate>
        </div>
      </div>

      {queryError && (
        <div className="alert alert-error" role="alert">
          <AlertTriangle size={18} />
          <span>{queryError.message || "Failed to load academic years"}</span>
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }} onClick={() => refetch()}>
            Retry
          </button>
        </div>
      )}

      {viewMode === "timeline" ? (
        allYears.length === 0 ? (
          <div className="empty-state"><Calendar size={40} /><p>No academic years yet</p></div>
        ) : (
          <AcademicTimeline
            years={allYears}
            onSetCurrent={handleSetCurrent}
            onCloseYear={(y) => setCascadeTarget(y)}
            onReopenYear={handleReopenYear}
            onDeleteYear={(y) => setConfirmAction({ type: "delete", id: y.id, name: y.name })}
            onManageSemesters={(y) => setSemesterYear(y)}
            lifecycleLoading={closeYear.isPending || reopenYear.isPending}
          />
        )
      ) : (
        <>
          <div style={{ display: "flex", gap: 8, marginBottom: 16, alignItems: "center" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: "5px 10px", flex: 1, maxWidth: 320 }}>
              <Search size={13} style={{ color: "#6b7280" }} />
              <input
                type="text" placeholder="Search by name…" value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handleSearch()}
                style={{ border: "none", outline: "none", flex: 1, fontSize: 13, fontFamily: "inherit", color: "#1a1f5e" }}
                aria-label="Search academic years"
              />
              {search && (
                <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
                  onClick={() => { setSearch(""); setPage(1); setSelectedIds(new Set()); }}>
                  <X size={13} />
                </button>
              )}
            </div>
            <button className="btn-cancel" style={{ padding: "5px 12px", fontSize: 12 }} onClick={handleSearch}>
              <Search size={12} /> Search
            </button>
            {filtered.length !== allYears.length && (
              <span style={{ fontSize: 12, color: "#6b7280" }}>{filtered.length} of {allYears.length}</span>
            )}
          </div>

          <DataTable
            columns={[
              { key: "name", label: "Name", render: (_, row) => (
                <span className="name-cell">
                  <span className="year-name">{row.name}</span>
                  {row.isCurrent && <StatusBadge status="active" label="Current" style={{ marginLeft: 8 }} />}
                  {row.isClosed && <StatusBadge status="closed" label="Closed" style={{ marginLeft: 8 }} />}
                </span>
              )},
              { key: "startDate", label: "Start Date", render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "endDate", label: "End Date", render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "isCurrent", label: "Status", render: (v) => <StatusBadge status={v ? "active" : "inactive"} label={v ? "Active" : "Inactive"} /> },
              { key: "isClosed", label: "Record", render: (v) => <StatusBadge status={v ? "closed" : "open"} label={v ? "Closed" : "Open"} /> },
              { key: "semesters", label: "Semesters", render: (_, row) => (
                <PermissionGate resource="academics.academic-years" minLevel={3}>
                  <button className="btn-semester" onClick={(e) => { e.stopPropagation(); setSemesterYear(row); }} title="Manage Semesters">
                    <Clock size={16} /> Manage
                  </button>
                </PermissionGate>
              )},
              { key: "actions", label: "Actions", render: (_, row) => (
                <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
                  {!row.isCurrent && !row.isClosed && (
                    <PermissionGate resource="academics.academic-years" minLevel={3}>
                      <button className="btn-icon set-current" onClick={() => handleSetCurrent(row)}
                        disabled={setCurrentYear.isPending} title="Set as Current Year">
                        <Clock size={16} />
                      </button>
                    </PermissionGate>
                  )}
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button className="btn-icon edit" onClick={() => openEditYearDrawer(row)} title="Edit"><Edit2 size={16} /></button>
                  </PermissionGate>
                  {row.isClosed ? (
                    <PermissionGate resource="academics.academic-years" minLevel={4}>
                      <button className="btn-icon reopen" onClick={() => handleReopenYear(row)}
                        disabled={reopenYear.isPending} title="Reopen Year"><Clock size={16} /></button>
                    </PermissionGate>
                  ) : (
                    <PermissionGate resource="academics.academic-years" minLevel={3}>
                      <button className="btn-icon close" onClick={() => setCascadeTarget(row)}
                        disabled={closeYear.isPending} title="Close Year"><Clock size={16} /></button>
                    </PermissionGate>
                  )}
                  <PermissionGate resource="academics.academic-years" minLevel={5}>
                    <button className="btn-icon delete" onClick={() => setConfirmAction({ type: "delete", id: row.id, name: row.name })}
                      title="Delete"><Trash2 size={16} /></button>
                  </PermissionGate>
                </div>
              ), nowrap: true },
            ]}
            data={displayYears}
            loading={isLoading}
            error={queryError?.message}
            emptyIcon={Calendar}
            emptyTitle="No Academic Years Found"
            emptyMessage="Create your first academic year to get started"
            emptyActionLabel="Create Academic Year"
            emptyAction={() => setShowWizard(true)}
            pagination={{ pageNumber: page, totalPages }}
            onPageChange={setPage}
            selectedIds={selectedIds}
            onSelectAll={() => {
              if (displayYears.every((y) => selectedIds.has(y.id))) setSelectedIds(new Set());
              else setSelectedIds(new Set(displayYears.map((y) => y.id)));
            }}
            onSelectOne={(id) => {
              setSelectedIds((prev) => {
                const n = new Set(prev);
                if (n.has(id)) n.delete(id); else n.add(id);
                return n;
              });
            }}
            compact={compact}
            onCompactToggle={() => setCompact((c) => !c)}
          />

          {selectedIds.size > 0 && (
            <div className="bulk-bar">
              <span>{selectedIds.size} selected</span>
              <button onClick={() => setSelectedIds(new Set())}>Clear</button>
              <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
                <PermissionGate resource="academics.academic-years" minLevel={5}>
                  <button className="bulk-danger" onClick={handleBulkDelete}>
                    <Trash2 size={12} /> Delete Selected
                  </button>
                </PermissionGate>
              </div>
            </div>
          )}
        </>
      )}

      {/* Year Wizard */}
      <AcademicYearWizard
        open={showWizard}
        onClose={() => setShowWizard(false)}
        onSave={handleWizardSave}
        saving={createYear.isPending}
      />

      {/* Edit Year Drawer */}
      <Drawer
        open={!!editYearDrawer}
        onClose={() => setEditYearDrawer(null)}
        title="Edit Academic Year"
        width={420}
        loading={updateYear.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditYearDrawer(null)}>Cancel</button>
            <button className="btn-primary" onClick={handleEditYearSave} disabled={updateYear.isPending}>Save</button>
          </>
        }
      >
        {editYearError && (
          <div role="alert" style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16 }}>
            {editYearError}
          </div>
        )}
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="form-group">
            <label htmlFor="ey-name">Academic Year Name *</label>
            <input id="ey-name" type="text" value={editYearForm.name}
              onChange={(e) => setEditYearForm((p) => ({ ...p, name: e.target.value }))}
              style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
          </div>
          <div style={{ display: "flex", gap: 12 }}>
            <div className="form-group" style={{ flex: 1 }}>
              <label htmlFor="ey-start">Start Date *</label>
              <input id="ey-start" type="date" value={editYearForm.startDate}
                onChange={(e) => setEditYearForm((p) => ({ ...p, startDate: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
            </div>
            <div className="form-group" style={{ flex: 1 }}>
              <label htmlFor="ey-end">End Date *</label>
              <input id="ey-end" type="date" value={editYearForm.endDate}
                min={editYearForm.startDate || undefined}
                onChange={(e) => setEditYearForm((p) => ({ ...p, endDate: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
            </div>
          </div>
          <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
            <input type="checkbox" checked={editYearForm.isCurrent}
              onChange={(e) => setEditYearForm((p) => ({ ...p, isCurrent: e.target.checked }))}
              style={{ accentColor: "#1a1f5e" }} />
            Mark as current academic year
          </label>
        </div>
      </Drawer>

      <CascadeImpactModal
        open={!!cascadeTarget}
        onClose={() => setCascadeTarget(null)}
        onConfirm={handleCloseYear}
        title="Close Academic Year"
        entityName={cascadeTarget?.name}
        impacts={[
          { count: semesters?.length || 0, label: "Semester(s)" },
          { count: "—", label: "Course offerings will be affected", detail: "via semesters" },
        ]}
        loading={closeYear.isPending}
      />

      <ConfirmDialog
        open={confirmAction?.type === "delete"}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleDeleteYear}
        title="Delete Academic Year"
        message={`Are you sure you want to delete ${confirmAction?.name}?`}
        detail="This action cannot be undone."
        confirmLabel="Delete"
        variant="danger"
        loading={deleteYear.isPending}
      />
    </div>
  );
}

export default AcademicYearsPage;
