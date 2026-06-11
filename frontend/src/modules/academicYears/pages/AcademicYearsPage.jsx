import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Plus, Edit2, Trash2, AlertTriangle, Calendar, Clock, Search, X,
  ChevronLeft, ChevronRight, ArrowLeft, CalendarRange,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import PageHeader from "../../../core/components/PageHeader";
import EmptyState from "../../../core/components/EmptyState";
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
import { toLocalizedJson } from "../../../core/utils/getLocalized";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import "../styles/academicYears.css";

const PAGE_SIZE = 10;

function AcademicYearsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { refreshAcademicYears } = useAcademic();

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
  const [editYearForm, setEditYearForm] = useState({ name: "", nameAr: "", startDate: "", endDate: "", isCurrent: false });
  const [editYearError, setEditYearError] = useState("");

  const [cascadeTarget, setCascadeTarget] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);

  const [semesterYear, setSemesterYear] = useState(null);

  const [semDrawer, setSemDrawer] = useState(null);
  const [editSem, setEditSem] = useState(null);
  const [semForm, setSemForm] = useState({ name: "", nameAr: "", startDate: "", endDate: "" });
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
        name: toLocalizedJson(data.nameAr, data.name),
        startDate: data.startDate,
        endDate: data.endDate,
        isCurrent: data.isCurrent,
      });
      if (data.semesters?.length > 0) {
        for (const sem of data.semesters) {
          try {
            await createSem.mutateAsync({
              ...sem,
              name: toLocalizedJson(sem.nameAr, sem.name),
              academicYearId: null,
            });
          } catch {}
        }
      }
      addToast(t("ay_created_with_semesters"), "success");
      setShowWizard(false);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || t("ay_create_failed"), "error");
    }
  };

  const openEditYearDrawer = (year) => {
    setEditYearDrawer(year);
    setEditYearForm({
      name: year.name,
      nameAr: "",
      startDate: year.startDate?.split("T")[0] || "",
      endDate: year.endDate?.split("T")[0] || "",
      isCurrent: year.isCurrent,
    });
    setEditYearError("");
  };

  const handleEditYearSave = async () => {
    if (!editYearForm.name.trim()) { setEditYearError(t("ay_name_required")); return; }
    if (!editYearForm.startDate) { setEditYearError(t("ay_start_date_required")); return; }
    if (!editYearForm.endDate) { setEditYearError(t("ay_end_date_required")); return; }
    if (new Date(editYearForm.startDate) >= new Date(editYearForm.endDate)) {
      setEditYearError(t("ay_end_after_start")); return;
    }
    try {
      // The API stores names as {"ar","en"} JSON but returns them localized, so
      // only resend the name when it was actually edited (or an Arabic name was
      // provided) to avoid overwriting the other language.
      const { name, nameAr, ...rest } = editYearForm;
      const payload = { id: editYearDrawer.id, ...rest };
      if (name.trim() !== (editYearDrawer.name || "") || nameAr.trim()) {
        payload.name = toLocalizedJson(nameAr, name);
      }
      await updateYear.mutateAsync(payload);
      addToast(t("ay_updated"), "success");
      setEditYearDrawer(null);
      refreshAcademicYears();
    } catch (err) {
      setEditYearError(err.message || t("ay_update_failed"));
    }
  };

  const handleDeleteYear = async () => {
    if (!confirmAction) return;
    try {
      await deleteYear.mutateAsync(confirmAction.id);
      addToast(t("ay_deleted"), "success");
      setSelectedIds((prev) => { const n = new Set(prev); n.delete(confirmAction.id); return n; });
      setConfirmAction(null);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || t("ay_delete_failed"), "error");
      setConfirmAction(null);
    }
  };

  const handleSetCurrent = async (year) => {
    try {
      await setCurrentYear.mutateAsync();
      addToast(t("ay_set_current", { name: year.name }), "success");
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || t("ay_set_current_failed"), "error");
    }
  };

  const handleCloseYear = async () => {
    if (!cascadeTarget) return;
    try {
      await closeYear.mutateAsync(cascadeTarget.id);
      addToast(t("ay_closed", { name: cascadeTarget.name }), "success");
      setCascadeTarget(null);
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || t("ay_close_failed"), "error");
    }
  };

  const handleReopenYear = async (year) => {
    try {
      await reopenYear.mutateAsync(year.id);
      addToast(t("ay_reopened", { name: year.name }), "success");
      refreshAcademicYears();
    } catch (err) {
      addToast(err.message || t("ay_reopen_failed"), "error");
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      for (const id of ids) {
        await deleteYear.mutateAsync(id);
      }
      addToast(t("ay_bulk_deleted", { count: ids.length }), "success");
      setSelectedIds(new Set());
      refreshAcademicYears();
    } catch (err) {
      addToast(t("ay_bulk_delete_failed", { message: err.message }), "error");
    }
  };

  const openCreateSemDrawer = () => {
    setSemDrawer("create");
    setEditSem(null);
    setSemForm({ name: "", nameAr: "", startDate: "", endDate: "" });
    setSemFormError("");
  };

  const openEditSemDrawer = (sem) => {
    setSemDrawer("edit");
    setEditSem(sem);
    setSemForm({
      name: sem.name,
      nameAr: "",
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
    if (!semForm.name.trim()) { setSemFormError(t("ay_semester_name_required")); return false; }
    if (!semForm.startDate) { setSemFormError(t("ay_start_date_required")); return false; }
    if (!semForm.endDate) { setSemFormError(t("ay_end_date_required")); return false; }
    if (new Date(semForm.startDate) >= new Date(semForm.endDate)) {
      setSemFormError(t("ay_end_after_start")); return false;
    }
    if (semesterDateBounds.minDate && semForm.startDate < semesterDateBounds.minDate) {
      setSemFormError(t("ay_start_within_year")); return false;
    }
    if (semesterDateBounds.maxDate && semForm.endDate > semesterDateBounds.maxDate) {
      setSemFormError(t("ay_end_within_year")); return false;
    }
    setSemFormError("");
    return true;
  };

  const handleSemSave = async (e) => {
    e.preventDefault();
    if (!validateSemForm()) return;
    try {
      const { name, nameAr, ...dates } = semForm;
      if (semDrawer === "create") {
        await createSem.mutateAsync({
          name: toLocalizedJson(nameAr, name),
          ...dates,
          academicYearId: semesterYear.id,
        });
        addToast(t("ay_semester_created"), "success");
      } else if (semDrawer === "edit" && editSem) {
        // Only resend the name when edited (or an Arabic name was provided) —
        // the API returns names localized, so blind resends clobber the other language.
        const payload = { id: editSem.id, ...dates };
        if (name.trim() !== (editSem.name || "") || nameAr.trim()) {
          payload.name = toLocalizedJson(nameAr, name);
        }
        await updateSem.mutateAsync(payload);
        addToast(t("ay_semester_updated"), "success");
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
      addToast(err.message || t("ay_semester_delete_failed"), "error");
    }
  };

  // Semester sub-view
  if (semesterYear) {
    return (
      <div className="academic-years-container">
        <PageHeader
          leading={
            <button className="btn-back" onClick={() => setSemesterYear(null)}>
              <ArrowLeft size={20} />
            </button>
          }
          title={`${t("manage_semesters")} — ${semesterYear.name}`}
          subtitle={t("manage_semesters_for")}
          actions={
            <PermissionGate resource="academics.academic-years" minLevel={2}>
              <button className="btn-create" onClick={openCreateSemDrawer}>
                <Plus size={18} /> {t("new_semester")}
              </button>
            </PermissionGate>
          }
        />

        {semsLoading ? (
          <div className="loading-container"><div className="spinner" /><p>{t("loading_semesters")}</p></div>
        ) : semesters.length === 0 ? (
          <div style={{ textAlign: "center" }}>
            <EmptyState icon={Clock} title={t("no_semesters_yet")} />
            <PermissionGate resource="academics.academic-years" minLevel={2}>
              <button className="btn-primary" onClick={openCreateSemDrawer}>{t("create_first_semester")}</button>
            </PermissionGate>
          </div>
        ) : (
          <DataTable
            columns={[
              { key: "name", label: t("name"), render: (v) => <span className="year-name">{v}</span> },
              { key: "startDate", label: t("start_date"), render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "endDate", label: t("end_date"), render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "startDate", label: t("duration"), render: (_, row) => {
                const start = new Date(row.startDate);
                const end = new Date(row.endDate);
                const days = Math.floor((end - start) / (1000 * 60 * 60 * 24));
                return <span style={{ fontFamily: "Space Mono, monospace" }}>{t("days_count", { count: days })}</span>;
              }},
              { key: "actions", label: t("actions"), render: (_, row) => (
                <div style={{ display: "flex", gap: 4 }}>
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button className="ay-icon-btn edit" onClick={() => openEditSemDrawer(row)} title={t("edit")}><Edit2 size={16} /></button>
                  </PermissionGate>
                  <PermissionGate resource="academics.academic-years" minLevel={5}>
                    <button className="ay-icon-btn delete" onClick={() => setSemDelete(row)} title={t("delete")}><Trash2 size={16} /></button>
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
          title={semDrawer === "create" ? t("create_semester") : t("edit_semester")}
          width={420}
          loading={createSem.isPending || updateSem.isPending}
          footer={
            <>
              <button className="btn-cancel" onClick={closeSemDrawer}>{t("cancel")}</button>
              <button className="btn-primary" onClick={handleSemSave}>
                {createSem.isPending || updateSem.isPending ? t("saving") : semDrawer === "create" ? t("create") : t("update")}
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
              <label htmlFor="sem-name">{t("semester_name")} *</label>
              <input id="sem-name" type="text" value={semForm.name}
                onChange={(e) => setSemForm((p) => ({ ...p, name: e.target.value }))}
                placeholder={t("semester_name_placeholder")}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
              <small style={{ color: "#6b7280", fontSize: 10 }}>
                {t("year_bounds", { min: semesterDateBounds.minDate, max: semesterDateBounds.maxDate })}
              </small>
            </div>
            <div className="form-group">
              <label htmlFor="sem-name-ar">{t("name_arabic")}</label>
              <input id="sem-name-ar" type="text" dir="rtl" value={semForm.nameAr}
                onChange={(e) => setSemForm((p) => ({ ...p, nameAr: e.target.value }))}
                placeholder="مثال: الفصل الأول"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
            </div>
            <div style={{ display: "flex", gap: 12 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="sem-start">{t("start_date")} *</label>
                <input id="sem-start" type="date" value={semForm.startDate}
                  min={semesterDateBounds.minDate || undefined}
                  max={semForm.endDate || semesterDateBounds.maxDate || undefined}
                  onChange={(e) => setSemForm((p) => ({ ...p, startDate: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }} />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="sem-end">{t("end_date")} *</label>
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
          title={t("delete_semester")}
          message={t("confirm_delete_semester", { name: semDelete?.name })}
          detail={t("cannot_undo")}
          confirmLabel={t("delete")}
          variant="danger"
        />
      </div>
    );
  }

  return (
    <div className="academic-years-container">
      <PageHeader
        icon={CalendarRange}
        title={t("academic_years_management")}
        subtitle={t("manage_academic_years")}
        actions={
          <>
            <div className="ay-view-toggle" role="group" aria-label="View mode">
              <button
                className={`ay-toggle-btn ${viewMode === "table" ? "active" : ""}`}
                onClick={() => { setViewMode("table"); setSelectedIds(new Set()); }}
                title={t("table_view")}
                aria-pressed={viewMode === "table"}
              ><Clock size={16} /></button>
              <button
                className={`ay-toggle-btn ${viewMode === "timeline" ? "active" : ""}`}
                onClick={() => { setViewMode("timeline"); setSelectedIds(new Set()); }}
                title={t("timeline_view")}
                aria-pressed={viewMode === "timeline"}
              ><ChevronLeft size={16} /></button>
            </div>
            <PermissionGate resource="academics.academic-years" minLevel={2}>
              <button className="btn-create" onClick={() => setShowWizard(true)}>
                <Plus size={18} /> {t("new_academic_year")}
              </button>
            </PermissionGate>
          </>
        }
      />

      {queryError && (
        <div className="alert alert-error" role="alert">
          <AlertTriangle size={18} />
          <span>{queryError.message || t("failed_to_load_data")}</span>
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }} onClick={() => refetch()}>
            {t("retry")}
          </button>
        </div>
      )}

      {viewMode === "timeline" ? (
        allYears.length === 0 ? (
          <EmptyState icon={Calendar} title={t("no_academic_years_yet")} />
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
                type="text" placeholder={t("search_by_name")} value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handleSearch()}
                style={{ border: "none", outline: "none", flex: 1, fontSize: 13, fontFamily: "inherit", color: "#1a1f5e" }}
                aria-label={t("search_by_name")}
              />
              {search && (
                <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
                  onClick={() => { setSearch(""); setPage(1); setSelectedIds(new Set()); }}>
                  <X size={13} />
                </button>
              )}
            </div>
            <button className="btn-cancel" style={{ padding: "5px 12px", fontSize: 12 }} onClick={handleSearch}>
              <Search size={12} /> {t("search")}
            </button>
            {filtered.length !== allYears.length && (
              <span style={{ fontSize: 12, color: "#6b7280" }}>{t("filtered_of_total", { filtered: filtered.length, total: allYears.length })}</span>
            )}
          </div>

          <DataTable
            columns={[
              { key: "name", label: t("name"), render: (_, row) => (
                <span className="name-cell">
                  <span className="year-name">{row.name}</span>
                  {row.isCurrent && <StatusBadge status="active" label={t("current_badge")} style={{ marginLeft: 8 }} />}
                  {row.isClosed && <StatusBadge status="closed" label={t("closed_badge")} style={{ marginLeft: 8 }} />}
                </span>
              )},
              { key: "startDate", label: t("start_date"), render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "endDate", label: t("end_date"), render: (v) => v ? new Date(v).toLocaleDateString() : "—" },
              { key: "isCurrent", label: t("status"), render: (v) => <StatusBadge status={v ? "active" : "inactive"} label={v ? t("active") : t("inactive")} /> },
              { key: "isClosed", label: t("record_status"), render: (v) => <StatusBadge status={v ? "closed" : "open"} label={v ? t("closed_badge") : t("open_status")} /> },
              { key: "semesters", label: t("semesters_tab"), render: (_, row) => (
                <PermissionGate resource="academics.academic-years" minLevel={3}>
                  <button className="btn-semester" onClick={(e) => { e.stopPropagation(); setSemesterYear(row); }} title={t("manage_semesters")}>
                    <Clock size={16} /> {t("manage_semesters")}
                  </button>
                </PermissionGate>
              )},
              { key: "actions", label: t("actions"), render: (_, row) => (
                <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
                  {!row.isCurrent && !row.isClosed && (
                    <PermissionGate resource="academics.academic-years" minLevel={3}>
                      <button className="ay-icon-btn set-current" onClick={() => handleSetCurrent(row)}
                        disabled={setCurrentYear.isPending} title={t("set_as_current_year")}>
                        <Clock size={16} />
                      </button>
                    </PermissionGate>
                  )}
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button className="ay-icon-btn edit" onClick={() => openEditYearDrawer(row)} title={t("edit")}><Edit2 size={16} /></button>
                  </PermissionGate>
                  {row.isClosed ? (
                    <PermissionGate resource="academics.academic-years" minLevel={4}>
                      <button className="ay-icon-btn reopen" onClick={() => handleReopenYear(row)}
                        disabled={reopenYear.isPending} title={t("reopen_year")}><Clock size={16} /></button>
                    </PermissionGate>
                  ) : (
                    <PermissionGate resource="academics.academic-years" minLevel={3}>
                      <button className="ay-icon-btn close" onClick={() => setCascadeTarget(row)}
                        disabled={closeYear.isPending} title={t("close_year")}><Clock size={16} /></button>
                    </PermissionGate>
                  )}
                  <PermissionGate resource="academics.academic-years" minLevel={5}>
                    <button className="ay-icon-btn delete" onClick={() => setConfirmAction({ type: "delete", id: row.id, name: row.name })}
                      title={t("delete")}><Trash2 size={16} /></button>
                  </PermissionGate>
                </div>
              ), nowrap: true },
            ]}
            data={displayYears}
            loading={isLoading}
            error={queryError?.message}
            emptyIcon={Calendar}
            emptyTitle={t("ay_empty_title")}
            emptyMessage={t("ay_empty_message")}
            emptyActionLabel={t("create_academic_year")}
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
              <span>{t("ay_selected_count", { count: selectedIds.size })}</span>
              <button onClick={() => setSelectedIds(new Set())}>{t("clear")}</button>
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
        title={t("ay_edit_year")}
        width={420}
        loading={updateYear.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditYearDrawer(null)}>{t("cancel")}</button>
            <button className="btn-primary" onClick={handleEditYearSave} disabled={updateYear.isPending}>{t("save")}</button>
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
          <div className="form-group">
            <label htmlFor="ey-name-ar">Academic Year Name (Arabic)</label>
            <input id="ey-name-ar" type="text" dir="rtl" value={editYearForm.nameAr}
              onChange={(e) => setEditYearForm((p) => ({ ...p, nameAr: e.target.value }))}
              placeholder="مثال: ٢٠٢٤-٢٠٢٥"
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
        title={t("ay_close_year")}
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
        title={t("ay_delete_year")}
        message={`Are you sure you want to delete ${confirmAction?.name}?`}
        detail="This action cannot be undone."
        confirmLabel={t("delete")}
        variant="danger"
        loading={deleteYear.isPending}
      />
    </div>
  );
}

export default AcademicYearsPage;
