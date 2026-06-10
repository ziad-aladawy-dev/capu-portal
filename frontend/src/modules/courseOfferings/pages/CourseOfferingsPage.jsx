import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  CalendarCheck, Plus, Edit2, AlertTriangle, Search, X,
  Send, Lock, Unlock, RefreshCw, Users, ArrowUp, ArrowDown, Calendar,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { OFFERING_STATUS_LABELS, OFFERING_STATUSES, REGISTRATION_STATE_LABELS } from "../../../core/services/courseOfferingService";
import { useToast } from "../../../core/components/Toast";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import EmptyState from "../../../core/components/EmptyState";
import { useActiveCourses } from "../../../core/query/useCourses";
import { useCourseOfferings, useCreateCourseOffering, useUpdateCourseOffering, useToggleOfferingLifecycle, useBulkPublishOfferings, useBulkCancelOfferings } from "../../../core/query/useCourseOfferings";
import "../styles/courseOfferings.css";

const PAGE_SIZE = 20;

const EMPTY_FORM = {
  courseId: "",
  sectionCode: "",
  capacity: 30,
  status: 0,
  registrationState: 0,
  semesterId: null,
  structureNodeId: null,
};

function CourseOfferingsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj, selectedSemester } = useAcademic();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [compact, setCompact] = useState(false);

  const queryParams = useMemo(() => ({
    page,
    pageSize: PAGE_SIZE,
    structureNodeId: scopeNode?.id,
    semesterId: selectedSemesterObj?.id,
    search,
    status: statusFilter !== "" ? Number(statusFilter) : undefined,
  }), [page, scopeNode?.id, selectedSemesterObj?.id, search, statusFilter]);

  const { data: offeringData, isLoading, error, refetch } = useCourseOfferings(queryParams);
  const { data: courses = [] } = useActiveCourses();

  const offerings = offeringData?.items || [];
  const totalCount = offeringData?.totalCount || 0;
  const totalPages = offeringData?.totalPages || 1;

  const createOffering = useCreateCourseOffering();
  const updateOffering = useUpdateCourseOffering();
  const { closeMut, openMut } = useToggleOfferingLifecycle();
  const bulkPublish = useBulkPublishOfferings();
  const bulkCancel = useBulkCancelOfferings();

  const [drawerMode, setDrawerMode] = useState(null);
  const [editOffering, setEditOffering] = useState(null);
  const [offeringFormData, setOfferingFormData] = useState(EMPTY_FORM);
  const [drawerError, setDrawerError] = useState("");
  const [confirmAction, setConfirmAction] = useState(null);
  const [bulkCancelOpen, setBulkCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [capacityAdjusting, setCapacityAdjusting] = useState(null);

  const getCourseInfo = (courseId) => courses.find((c) => c.id === courseId);

  const stats = useMemo(() => {
    const total = offerings.length;
    const atCapacity = offerings.filter((o) => o.capacity > 0 && o.registeredCount >= o.capacity).length;
    const nearCapacity = offerings.filter((o) => o.capacity > 0 && o.registeredCount / o.capacity >= 0.8 && o.registeredCount < o.capacity).length;
    const regOpen = offerings.filter((o) => o.registrationState === 1).length;
    const draft = offerings.filter((o) => o.status === 0).length;
    return { total, atCapacity, nearCapacity, regOpen, draft };
  }, [offerings]);

  // Form field error tracking
  const [fieldErrors, setFieldErrors] = useState({});

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); };

  const clearSearch = () => {
    setSearch("");
    setPage(1);
    setSelectedIds(new Set());
  };

  const openCreate = () => {
    setDrawerMode("create");
    setEditOffering(null);
    setOfferingFormData({
      courseId: "",
      sectionCode: "",
      capacity: 30,
      status: 0,
      registrationState: 0,
      semesterId: selectedSemesterObj?.id,
      structureNodeId: scopeNode?.id,
    });
    setDrawerError("");
    setFieldErrors({});
  };

  const openEdit = (offering) => {
    setDrawerMode("edit");
    setEditOffering(offering);
    setOfferingFormData({
      courseId: offering.courseId,
      sectionCode: offering.sectionCode,
      capacity: offering.capacity,
      status: offering.status,
      registrationState: offering.registrationState,
      semesterId: offering.semesterId,
      structureNodeId: offering.structureNodeId,
    });
    setDrawerError("");
    setFieldErrors({});
  };

  const closeForm = () => {
    setDrawerMode(null);
    setEditOffering(null);
    setDrawerError("");
    setFieldErrors({});
  };

  const validateForm = () => {
    const errors = {};
    if (!offeringFormData.courseId) errors.courseId = "Course is required";
    if (!offeringFormData.sectionCode.trim()) errors.sectionCode = "Section code is required";
    if (offeringFormData.capacity < 0) errors.capacity = "Capacity cannot be negative";
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setDrawerError(Object.values(errors)[0]);
      return false;
    }
    return true;
  };

  const handleSave = async () => {
    setDrawerError("");
    if (!validateForm()) return;
    const fd = offeringFormData;
    try {
      if (editOffering) {
        const body = {};
        if (fd.sectionCode !== editOffering.sectionCode) body.sectionCode = fd.sectionCode;
        if (fd.capacity !== editOffering.capacity) body.capacity = fd.capacity;
        if (fd.status !== editOffering.status) body.status = fd.status;
        if (fd.registrationState !== editOffering.registrationState) body.registrationState = fd.registrationState;
        await updateOffering.mutateAsync({ id: editOffering.id, ...body });
        addToast("Offering updated", "success");
      } else {
        await createOffering.mutateAsync(fd);
        addToast("Offering created", "success");
      }
      closeForm();
    } catch (err) {
      setDrawerError(err.message || "Failed to save offering");
    }
  };

  const handleClose = async () => {
    if (!confirmAction) return;
    try {
      await closeMut.mutateAsync(confirmAction.id);
      addToast("Offering closed", "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to close", "error");
      setConfirmAction(null);
    }
  };

  const handleOpen = async () => {
    if (!confirmAction) return;
    try {
      await openMut.mutateAsync(confirmAction.id);
      addToast("Offering reopened", "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to reopen", "error");
      setConfirmAction(null);
    }
  };

  const handleCapacityAdjust = async (offering, delta) => {
    setCapacityAdjusting(offering.id);
    try {
      const newCapacity = Math.max(0, offering.capacity + delta);
      await updateOffering.mutateAsync({ id: offering.id, capacity: newCapacity });
      addToast(`Capacity ${delta > 0 ? "increased" : "decreased"} to ${newCapacity}`, "success");
    } catch (err) {
      addToast(err.message || "Failed to adjust capacity", "error");
    } finally {
      setCapacityAdjusting(null);
    }
  };

  const handleBulkPublish = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await bulkPublish.mutateAsync(ids);
      addToast(`${ids.length} offering(s) published`, "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(`Publish failed: ${err.message}`, "error");
    }
  };

  const handleBulkCancel = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length || !cancelReason.trim()) return;
    try {
      await bulkCancel.mutateAsync({ ids, reason: cancelReason });
      addToast(`${ids.length} offering(s) cancelled`, "success");
      setSelectedIds(new Set());
      setCancelReason("");
      setBulkCancelOpen(false);
    } catch (err) {
      addToast(`Cancel failed: ${err.message}`, "error");
    }
  };

  const offeringStatusVariant = (status) => {
    switch (status) {
      case 0: return "draft";
      case 1: return "open";
      case 2: return "closed";
      case 3: return "cancelled";
      default: return "inactive";
    }
  };

  const regStatusVariant = (state) => {
    switch (state) {
      case 0: return "closed";
      case 1: return "open";
      case 2: return "warning";
      default: return "inactive";
    }
  };

  if (!selectedSemesterObj) {
    return (
      <div className="co-page">
        <div className="co-header">
          <div className="co-header-left">
            <CalendarCheck size={20} />
            <div>
              <h1>{t("course_offerings")}</h1>
              <p>{t("manage_course_offerings")}</p>
            </div>
          </div>
        </div>
        <EmptyState
          icon={Calendar}
          title="Select a Semester"
          message="Choose a semester from the context selector above to view course offerings."
        />
      </div>
    );
  }

  return (
    <div className="co-page">
      <div className="co-header">
        <div className="co-header-left">
          <CalendarCheck size={20} />
          <div>
            <h1>{t("course_offerings")}</h1>
            <p>{t("manage_course_offerings")} — {selectedSemester}</p>
          </div>
        </div>
        <PermissionGate resource="course-offerings.course-offerings" minLevel={2}>
          <button className="co-btn co-btn-primary" onClick={openCreate}>
            <Plus size={16} /> {t("new_offering")}
          </button>
        </PermissionGate>
      </div>

      {error && (
        <div className="co-alert co-alert-error" role="alert">
          <AlertTriangle size={16} /> {error.message || "Failed to load offerings"}
          <button className="co-btn co-btn-outline" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      <div className="co-toolbar">
        <div className="co-search">
          <Search size={14} />
          <input type="text" placeholder={t("search_course_offerings")} value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSearch()} aria-label="Search offerings" />
          {search && (
            <button className="co-search-clear" onClick={clearSearch}>
              <X size={14} />
            </button>
          )}
        </div>
        <select className="co-select" value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setPage(1); setSelectedIds(new Set()); }}
          aria-label="Filter by status">
          <option value="">{t("all_statuses")}</option>
          {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
            <option key={val} value={val}>{label}</option>
          ))}
        </select>
        <button className="co-btn co-btn-outline" onClick={handleSearch}>
          <Search size={13} /> {t("search")}
        </button>
        {(search || statusFilter) && (
          <button className="co-btn co-btn-ghost" onClick={clearSearch}>
            <X size={13} /> Clear
          </button>
        )}
      </div>

      {/* Search results info */}
      {!isLoading && !error && (
        <div className="co-result-info">
          {totalCount > 0
            ? `${totalCount} offering(s) found${search ? ` for "${search}"` : ""}`
            : "No offerings match your criteria"}
        </div>
      )}

      {offerings.length > 0 && (
        <div className="co-stats-bar">
          <div className="co-stat-item">
            <span className="co-stat-value">{stats.total}</span>
            <span className="co-stat-label">On Page</span>
          </div>
          <div className="co-stat-divider" />
          <div className="co-stat-item warn">
            <span className="co-stat-value">{stats.nearCapacity}</span>
            <span className="co-stat-label">Near Cap</span>
          </div>
          <div className="co-stat-item danger">
            <span className="co-stat-value">{stats.atCapacity}</span>
            <span className="co-stat-label">Full</span>
          </div>
          <div className="co-stat-divider" />
          <div className="co-stat-item success">
            <span className="co-stat-value">{stats.regOpen}</span>
            <span className="co-stat-label">Reg Open</span>
          </div>
          <div className="co-stat-item">
            <span className="co-stat-value">{stats.draft}</span>
            <span className="co-stat-label">Draft</span>
          </div>
        </div>
      )}

      {stats.atCapacity > 0 && (
        <div className="co-alert co-alert-warning" role="status">
          <Users size={14} />
          <strong>{stats.atCapacity} offering(s)</strong> at full capacity
          {stats.nearCapacity > 0 && (
            <span> &middot; <strong>{stats.nearCapacity} more</strong> near capacity</span>
          )}
        </div>
      )}

      <DataTable
        columns={[
          {
            key: "course", label: t("course"), render: (_, row) => {
              const course = getCourseInfo(row.courseId);
              return (
                <div className="co-course-info">
                  <span className="co-course-code">{course?.code || "—"}</span>
                  <span className="co-course-title">{course?.title || t("unknown_course")}</span>
                </div>
              );
            }, nowrap: false
          },
          { key: "sectionCode", label: t("section"), render: (v) => <span className="co-section-badge">{v}</span> },
          {
            key: "capacity", label: t("capacity"), render: (_, row) => (
              <div className="co-capacity-control">
                <span className="co-capacity-num">{row.capacity}</span>
                <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                  <div className="co-capacity-adjust">
                    <button className="co-capacity-btn" onClick={(e) => { e.stopPropagation(); handleCapacityAdjust(row, 1); }}
                      disabled={capacityAdjusting === row.id} title="Increase capacity" aria-label="Increase capacity">
                      <ArrowUp size={10} />
                    </button>
                    <button className="co-capacity-btn" onClick={(e) => { e.stopPropagation(); handleCapacityAdjust(row, -1); }}
                      disabled={capacityAdjusting === row.id || row.capacity <= 0} title="Decrease capacity" aria-label="Decrease capacity">
                      <ArrowDown size={10} />
                    </button>
                  </div>
                </PermissionGate>
              </div>
            )
          },
          {
            key: "registeredCount", label: t("enrolled"), render: (_, row) => {
              const filled = row.capacity > 0 ? Math.round((row.registeredCount / row.capacity) * 100) : 0;
              return (
                <div className="co-enrolled-cell">
                  <span className={`co-enrolled-num ${filled >= 100 ? "danger" : filled >= 80 ? "warn" : ""}`}>
                    {row.registeredCount}
                    <span className="co-enrolled-sep">/{row.capacity}</span>
                  </span>
                  <div className="co-capacity-bar">
                    <div className={`co-capacity-fill ${filled >= 100 ? "full" : filled >= 80 ? "warn" : "ok"}`}
                      style={{ width: `${Math.min(filled, 100)}%` }} />
                  </div>
                </div>
              );
            }
          },
          {
            key: "status", label: t("status"), render: (v) => (
              <StatusBadge status={offeringStatusVariant(v)} label={OFFERING_STATUS_LABELS[v] || t("unknown")} />
            )
          },
          {
            key: "registrationState", label: t("registration"), render: (v) => (
              <StatusBadge status={regStatusVariant(v)} label={REGISTRATION_STATE_LABELS[v] || t("unknown")} />
            )
          },
          { key: "isClosed", label: "Record", render: (v) => <StatusBadge status={v ? "closed" : "open"} label={v ? "Closed" : "Open"} /> },
          {
            key: "actions", label: t("actions"), render: (_, row) => (
              <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
                <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                  <button className="co-action-btn" onClick={() => openEdit(row)} title={t("edit")} aria-label={t("edit")}>
                    <Edit2 size={14} />
                  </button>
                </PermissionGate>
                {row.isClosed ? (
                  <PermissionGate resource="course-offerings.course-offerings" minLevel={4}>
                    <button className="co-action-btn" onClick={() => setConfirmAction({ type: "open", id: row.id })}
                      disabled={openMut.isPending} title="Reopen" aria-label="Reopen offering">
                      <Unlock size={14} />
                    </button>
                  </PermissionGate>
                ) : (
                  <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                    <button className="co-action-btn" onClick={() => setConfirmAction({ type: "close", id: row.id })}
                      disabled={closeMut.isPending} title="Close record" aria-label="Close offering record">
                      <Lock size={14} />
                    </button>
                  </PermissionGate>
                )}
              </div>
            ), nowrap: true
          },
        ]}
        data={offerings}
        loading={isLoading}
        error={error?.message}
        emptyIcon={CalendarCheck}
        emptyTitle={t("no_course_offerings")}
        emptyMessage={t("create_offering_to_start")}
        emptyActionLabel={t("new_offering")}
        emptyAction={openCreate}
        pagination={{ pageNumber: page, totalPages }}
        onPageChange={setPage}
        selectedIds={selectedIds}
        onSelectAll={() => {
          if (offerings.every((o) => selectedIds.has(o.id))) setSelectedIds(new Set());
          else setSelectedIds(new Set(offerings.map((o) => o.id)));
        }}
        onSelectOne={(id) => {
          setSelectedIds((prev) => { const n = new Set(prev); if (n.has(id)) n.delete(id); else n.add(id); return n; });
        }}
        getRowClass={(row) => {
          const filled = row.capacity > 0 ? (row.registeredCount / row.capacity) * 100 : 0;
          return filled >= 100 ? "danger" : filled >= 80 ? "warn" : "";
        }}
        compact={compact}
        onCompactToggle={() => setCompact((c) => !c)}
        tableLabel="Course offerings"
      />

      {/* Bulk actions */}
      {selectedIds.size > 0 && (
        <div className="bulk-bar">
          <span>{selectedIds.size} selected</span>
          <button onClick={() => setSelectedIds(new Set())}>Clear</button>
          <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
            <button className="bulk-success" onClick={handleBulkPublish} disabled={bulkPublish.isPending}>
              <Send size={13} /> Publish
            </button>
            <button className="bulk-warning" onClick={() => setBulkCancelOpen(true)} disabled={bulkCancel.isPending}>
              <Lock size={13} /> Cancel
            </button>
          </PermissionGate>
        </div>
      )}

      {/* Offering Drawer */}
      <Drawer
        open={!!drawerMode}
        onClose={closeForm}
        title={drawerMode === "create" ? t("new_offering") : t("edit_offering")}
        width={460}
        loading={createOffering.isPending || updateOffering.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={closeForm}>Cancel</button>
            <button className="btn-primary" onClick={handleSave} disabled={createOffering.isPending || updateOffering.isPending}>
              {drawerMode === "create" ? t("create") : t("save")}
            </button>
          </>
        }
      >
        {drawerError && (
          <div role="alert" className="co-drawer-error">
            {drawerError}
          </div>
        )}

        <div className="co-drawer-form">
          <div className={`co-drawer-field${fieldErrors.courseId ? " has-error" : ""}`}>
            <label htmlFor="of-course">{t("course")} *</label>
            <select id="of-course" value={offeringFormData.courseId}
              onChange={(e) => { setOfferingFormData((p) => ({ ...p, courseId: e.target.value })); setFieldErrors((p) => ({ ...p, courseId: "" })); }}
              disabled={drawerMode === "edit"}>
              <option value="">{t("select_course")}</option>
              {courses.map((c) => (
                <option key={c.id} value={c.id}>{c.code} — {c.title}</option>
              ))}
            </select>
            {fieldErrors.courseId && <span className="co-field-error-msg">{fieldErrors.courseId}</span>}
          </div>

          <div className={`co-drawer-field${fieldErrors.sectionCode ? " has-error" : ""}`}>
            <label htmlFor="of-section">{t("section")} *</label>
            <input id="of-section" type="text" value={offeringFormData.sectionCode}
              onChange={(e) => { setOfferingFormData((p) => ({ ...p, sectionCode: e.target.value })); setFieldErrors((p) => ({ ...p, sectionCode: "" })); }}
              placeholder="e.g. A" maxLength={20} />
            {fieldErrors.sectionCode && <span className="co-field-error-msg">{fieldErrors.sectionCode}</span>}
          </div>

          <div className={`co-drawer-field${fieldErrors.capacity ? " has-error" : ""}`}>
            <label htmlFor="of-capacity">{t("capacity")}</label>
            <input id="of-capacity" type="number" value={offeringFormData.capacity}
              min={0} max={9999}
              onChange={(e) => { setOfferingFormData((p) => ({ ...p, capacity: Number(e.target.value) })); setFieldErrors((p) => ({ ...p, capacity: "" })); }} />
            {fieldErrors.capacity && <span className="co-field-error-msg">{fieldErrors.capacity}</span>}
          </div>

          {drawerMode === "edit" && (
            <>
              <div className="co-drawer-field">
                <label htmlFor="of-status">{t("status")}</label>
                <select id="of-status" value={offeringFormData.status}
                  onChange={(e) => setOfferingFormData((p) => ({ ...p, status: Number(e.target.value) }))}>
                  {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
                    <option key={val} value={val}>{label}</option>
                  ))}
                </select>
              </div>
              <div className="co-drawer-field">
                <label htmlFor="of-reg">{t("registration")}</label>
                <select id="of-reg" value={offeringFormData.registrationState}
                  onChange={(e) => setOfferingFormData((p) => ({ ...p, registrationState: Number(e.target.value) }))}>
                  {Object.entries(REGISTRATION_STATE_LABELS).map(([val, label]) => (
                    <option key={val} value={val}>{label}</option>
                  ))}
                </select>
              </div>
            </>
          )}
        </div>
      </Drawer>

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={confirmAction?.type === "close" ? handleClose : handleOpen}
        title={confirmAction?.type === "close" ? "Close Offering Record" : "Reopen Offering Record"}
        message={confirmAction?.type === "close" ? "Are you sure you want to close this offering?" : "Reopen this offering?"}
        detail={confirmAction?.type === "close" ? "Closed offerings cannot be edited or accept new registrations." : "Reopened offerings will be editable and available for registration."}
        confirmLabel={confirmAction?.type === "close" ? "Yes, Close" : "Yes, Reopen"}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closeMut.isPending || openMut.isPending}
      />

      {/* Bulk Cancel Confirmation */}
      <ConfirmDialog
        open={bulkCancelOpen}
        onClose={() => { setBulkCancelOpen(false); setCancelReason(""); }}
        onConfirm={handleBulkCancel}
        title="Cancel Selected Offerings"
        message={`Cancel ${selectedIds.size} offering(s)?`}
        detail="Provide a cancellation reason below."
        confirmLabel="Confirm Cancel"
        variant="danger"
        loading={bulkCancel.isPending}
      >
        <div style={{ marginTop: 12 }}>
          <input type="text" placeholder="Enter cancellation reason…" value={cancelReason}
            onChange={(e) => setCancelReason(e.target.value)}
            style={{ width: "100%", padding: "8px 12px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box" }}
            autoFocus />
        </div>
      </ConfirmDialog>
    </div>
  );
}

export default CourseOfferingsPage;
