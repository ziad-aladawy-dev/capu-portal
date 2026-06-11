import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import {
  CalendarCheck, Plus, Edit2, AlertTriangle, Search, X, Send, Lock, Unlock,
  RefreshCw, Calendar, Layers, Clock,
} from "lucide-react";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import {
  OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS,
} from "../../../../core/services/courseOfferingService";
import { useToast } from "../../../../core/components/Toast";
import StatusBadge from "../../../../core/components/StatusBadge";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import DataTable from "../../../../core/components/DataTable";
import EmptyState from "../../../../core/components/EmptyState";
import { useActiveCourses } from "../../../../core/query/useCourses";
import {
  useCourseOfferings, useCreateCourseOffering, useUpdateCourseOffering,
  useToggleOfferingLifecycle, useBulkPublishOfferings, useBulkCancelOfferings,
  useOfferingStats,
} from "../../../../core/query/useCourseOfferings";
import OfferingFormDrawer from "./OfferingFormDrawer";
import BatchSectionsWizard from "./BatchSectionsWizard";
import { useStaffOptions } from "./useStaffOptions";
import shared from "../../styles/academic.module.css";
import styles from "./CourseOfferings.module.css";

const PAGE_SIZE = 20;
const CLEAR_INSTRUCTOR = "00000000-0000-0000-0000-000000000000";

const offeringStatusVariant = (s) => ({ 0: "draft", 1: "open", 2: "closed", 3: "cancelled" }[s] || "inactive");
const regStatusVariant = (s) => ({ 0: "closed", 1: "open", 2: "warning" }[s] || "inactive");

/** Click-to-edit capacity cell. Enter saves, Escape cancels, blur saves. */
function CapacityCell({ row, onSave, disabled }) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(row.capacity);

  if (!editing || disabled) {
    return (
      <button
        type="button"
        className={styles.capacityValue}
        onClick={(e) => { e.stopPropagation(); if (!disabled) { setValue(row.capacity); setEditing(true); } }}
        disabled={disabled}
        title={disabled ? undefined : "Click to edit"}
      >
        {row.capacity}
      </button>
    );
  }

  const commit = () => {
    setEditing(false);
    const n = Math.max(0, Math.min(9999, Number(value)));
    if (Number.isFinite(n) && n !== row.capacity) onSave(row, n);
  };

  return (
    <input
      type="number"
      className={styles.capacityInput}
      value={value}
      min={0}
      max={9999}
      autoFocus
      onClick={(e) => e.stopPropagation()}
      onChange={(e) => setValue(e.target.value)}
      onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === "Enter") commit();
        else if (e.key === "Escape") setEditing(false);
      }}
    />
  );
}

/** Instructor cell: name display; click swaps to an inline assign select. */
function InstructorCell({ row, staff, onAssign, disabled }) {
  const { t } = useTranslation("academic");
  const [editing, setEditing] = useState(false);
  const name = useMemo(
    () => staff.find((s) => s.id === row.instructorId)?.name,
    [staff, row.instructorId]
  );

  if (!editing) {
    return (
      <button
        type="button"
        className={row.instructorId ? styles.instructorAssigned : styles.instructorEmpty}
        onClick={(e) => { e.stopPropagation(); if (!disabled) setEditing(true); }}
        disabled={disabled}
        title={disabled ? undefined : t("offerings.assignInstructor")}
      >
        {name || (row.instructorId ? `${String(row.instructorId).slice(0, 8)}…` : t("offerings.unassigned"))}
      </button>
    );
  }

  return (
    <select
      className={styles.instructorSelect}
      autoFocus
      defaultValue={row.instructorId || ""}
      onClick={(e) => e.stopPropagation()}
      onBlur={() => setEditing(false)}
      onKeyDown={(e) => e.key === "Escape" && setEditing(false)}
      onChange={(e) => {
        setEditing(false);
        const v = e.target.value;
        if (v !== (row.instructorId || "")) onAssign(row, v || CLEAR_INSTRUCTOR);
      }}
    >
      <option value="">{t("offerings.unassigned")}</option>
      {staff.map((s) => (
        <option key={s.id} value={s.id}>{s.name}</option>
      ))}
    </select>
  );
}

export default function CourseOfferingsPage() {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const navigate = useNavigate();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj, selectedSemester, semesters, selectSemester } = useAcademic();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());

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
  const { data: staff = [] } = useStaffOptions();
  const { data: termStats } = useOfferingStats(selectedSemesterObj?.id, scopeNode?.id);

  const offerings = offeringData?.items || [];
  const totalCount = offeringData?.totalCount || 0;
  const totalPages = offeringData?.totalPages || 1;

  const createOffering = useCreateCourseOffering();
  const updateOffering = useUpdateCourseOffering();
  const { closeMut, openMut } = useToggleOfferingLifecycle();
  const bulkPublish = useBulkPublishOfferings();
  const bulkCancel = useBulkCancelOfferings();

  const [drawerMode, setDrawerMode] = useState(null);
  const [editTarget, setEditTarget] = useState(null);
  const [serverError, setServerError] = useState("");
  const [batchOpen, setBatchOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState(null);
  const [bulkCancelOpen, setBulkCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");

  const courseById = useMemo(() => {
    const m = new Map();
    for (const c of courses) m.set(c.id, c);
    return m;
  }, [courses]);

  const handleSubmit = async (payload) => {
    setServerError("");
    try {
      if (drawerMode === "create") {
        await createOffering.mutateAsync(payload);
        addToast(t("offerings.offeringCreated"), "success");
      } else if (editTarget) {
        await updateOffering.mutateAsync({ id: editTarget.id, ...payload });
        addToast(t("offerings.offeringUpdated"), "success");
      }
      setDrawerMode(null);
      setEditTarget(null);
    } catch (err) {
      setServerError(
        err.status === 409 ? t("offerings.sectionInUse") : err.message || t("courses.saveFailed")
      );
    }
  };

  const handleCapacitySave = async (row, newCapacity) => {
    try {
      await updateOffering.mutateAsync({ id: row.id, capacity: newCapacity });
      addToast(t("offerings.offeringUpdated"), "success");
    } catch (err) {
      addToast(err.status === 409 ? t("offerings.capacityBelowCount") : err.message, "error");
    }
  };

  const handleInstructorAssign = async (row, instructorId) => {
    try {
      await updateOffering.mutateAsync({ id: row.id, instructorId });
      addToast(t("offerings.offeringUpdated"), "success");
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    }
  };

  const handleLifecycle = async () => {
    if (!confirmAction) return;
    const mut = confirmAction.type === "close" ? closeMut : openMut;
    try {
      await mut.mutateAsync(confirmAction.id);
      addToast(confirmAction.type === "close" ? t("offerings.offeringClosed") : t("offerings.offeringReopened"), "success");
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    } finally {
      setConfirmAction(null);
    }
  };

  const handleBulkPublish = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await bulkPublish.mutateAsync(ids);
      addToast(t("offerings.published", { count: ids.length }), "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  const handleBulkCancel = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length || !cancelReason.trim()) return;
    try {
      await bulkCancel.mutateAsync({ ids, reason: cancelReason });
      addToast(t("offerings.cancelledToast", { count: ids.length }), "success");
      setSelectedIds(new Set());
      setCancelReason("");
      setBulkCancelOpen(false);
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  if (!selectedSemesterObj) {
    return (
      <>
        <div className={shared.header}>
          <div className={shared.headerLeft}>
            <CalendarCheck size={20} />
            <div>
              <h1>{t("offerings.title")}</h1>
              <p>{t("offerings.subtitle")}</p>
            </div>
          </div>
        </div>
        <EmptyState
          icon={Calendar}
          title={t("common.semester")}
          message={t("common.noSemester")}
        />
        {semesters.length > 0 && (
          <div style={{ display: "flex", justifyContent: "center", marginTop: 12 }}>
            <select
              className={shared.select}
              defaultValue=""
              onChange={(e) => {
                const sem = semesters.find((s) => s.id === e.target.value);
                if (sem) selectSemester(sem);
              }}
              aria-label={t("common.semester")}
            >
              <option value="" disabled>{t("common.semester")}…</option>
              {semesters.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </div>
        )}
      </>
    );
  }

  const canEdit3 = "course-offerings.course-offerings";

  return (
    <>
      <div className={shared.header}>
        <div className={shared.headerLeft}>
          <CalendarCheck size={20} />
          <div>
            <h1>{t("offerings.title")}</h1>
            <p>{t("offerings.subtitle")} — {selectedSemester}</p>
          </div>
        </div>
        <div className={shared.headerActions}>
          <PermissionGate resource={canEdit3} minLevel={2}>
            <button className="btn-cancel" onClick={() => setBatchOpen(true)}>
              <Layers size={14} /> {t("offerings.batchCreate")}
            </button>
            <button className="btn-primary" onClick={() => { setDrawerMode("create"); setEditTarget(null); setServerError(""); }}>
              <Plus size={14} /> {t("offerings.createOffering")}
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className={shared.errorBanner} role="alert">
          <AlertTriangle size={16} /> {error.message}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> {t("common.retry")}
          </button>
        </div>
      )}

      {termStats && (
        <div className={shared.statsBar}>
          <div className={shared.stat}><span className={shared.statValue}>{termStats.total}</span><span className={shared.statLabel}>{t("common.total")}</span></div>
          <div className={shared.statDivider} />
          <div className={shared.stat}><span className={shared.statValue} style={{ color: "#166534" }}>{termStats.openCount}</span><span className={shared.statLabel}>{t("offerings.statsOpen")}</span></div>
          <div className={shared.stat}><span className={shared.statValue} style={{ color: "#92400e" }}>{termStats.draftCount}</span><span className={shared.statLabel}>{t("offerings.statsDraft")}</span></div>
          <div className={shared.stat}><span className={shared.statValue} style={{ color: termStats.fullCount ? "#b91c1c" : undefined }}>{termStats.fullCount}</span><span className={shared.statLabel}>{t("offerings.statsFull")}</span></div>
          <div className={shared.statDivider} />
          <div className={shared.stat}>
            <span className={shared.statValue}>{termStats.totalRegistered}/{termStats.totalCapacity}</span>
            <span className={shared.statLabel}>{t("offerings.statsSeats")}</span>
          </div>
        </div>
      )}

      <div className={shared.toolbar}>
        <div className={shared.searchBox}>
          <Search size={14} />
          <input
            type="text"
            placeholder={t("offerings.searchPlaceholder")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && setPage(1)}
            aria-label={t("offerings.searchPlaceholder")}
          />
          {search && (
            <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }} onClick={() => { setSearch(""); setPage(1); }}>
              <X size={14} />
            </button>
          )}
        </div>
        <select className={shared.select} value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); setSelectedIds(new Set()); }} aria-label={t("common.status")}>
          <option value="">{t("common.allStatuses")}</option>
          {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
            <option key={val} value={val}>{label}</option>
          ))}
        </select>
      </div>

      <DataTable
        columns={[
          {
            key: "course", label: t("offerings.course"), nowrap: false,
            render: (_, row) => {
              const course = courseById.get(row.courseId);
              return (
                <div className={styles.courseInfo}>
                  <span className={shared.codePill}>{course?.code || "—"}</span>
                  <span className={styles.courseTitle}>{course?.title || row.courseId}</span>
                </div>
              );
            },
          },
          {
            key: "sectionCode", label: t("offerings.section"), width: 90,
            render: (v) => <span className={styles.sectionBadge}>{v}</span>,
          },
          {
            key: "capacity", label: t("offerings.capacity"), align: "center", width: 90,
            render: (_, row) => (
              <PermissionGate resource={canEdit3} minLevel={3} fallback={<span>{row.capacity}</span>}>
                <CapacityCell row={row} onSave={handleCapacitySave} disabled={row.isClosed} />
              </PermissionGate>
            ),
          },
          {
            key: "registeredCount", label: t("offerings.registered"), width: 130,
            render: (_, row) => {
              const filled = row.capacity > 0 ? Math.round((row.registeredCount / row.capacity) * 100) : 0;
              return (
                <div className={styles.enrolledCell}>
                  <span className={filled >= 100 ? styles.enrolledDanger : filled >= 80 ? styles.enrolledWarn : ""}>
                    {row.registeredCount}<span className={styles.enrolledSep}>/{row.capacity}</span>
                  </span>
                  <div className={styles.capacityTrack}>
                    <div
                      className={`${styles.capacityFill} ${filled >= 100 ? styles.fillDanger : filled >= 80 ? styles.fillWarn : styles.fillOk}`}
                      style={{ width: `${Math.min(filled, 100)}%` }}
                    />
                  </div>
                </div>
              );
            },
          },
          {
            key: "instructorId", label: t("offerings.instructor"), width: 160,
            render: (_, row) => (
              <PermissionGate
                resource={canEdit3}
                minLevel={3}
                fallback={<span style={{ fontSize: 12 }}>{staff.find((s) => s.id === row.instructorId)?.name || t("offerings.unassigned")}</span>}
              >
                <InstructorCell row={row} staff={staff} onAssign={handleInstructorAssign} disabled={row.isClosed} />
              </PermissionGate>
            ),
          },
          {
            key: "status", label: t("common.status"), width: 95,
            render: (v) => <StatusBadge status={offeringStatusVariant(v)} label={OFFERING_STATUS_LABELS[v]} />,
          },
          {
            key: "registrationState", label: t("offerings.registration"), width: 95,
            render: (v) => <StatusBadge status={regStatusVariant(v)} label={REGISTRATION_STATE_LABELS[v]} />,
          },
          {
            key: "actions", label: t("common.actions"), nowrap: true, width: 130,
            render: (_, row) => (
              <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
                <button
                  className={styles.actionBtn}
                  onClick={() => navigate(`/admin/academic/schedule?offeringId=${row.id}`)}
                  title={t("offerings.scheduleHint")}
                  aria-label={t("offerings.scheduleHint")}
                >
                  <Clock size={13} />
                </button>
                <PermissionGate resource={canEdit3} minLevel={3}>
                  <button className={styles.actionBtn} onClick={() => { setEditTarget(row); setDrawerMode("edit"); setServerError(""); }} title={t("common.edit")}>
                    <Edit2 size={13} />
                  </button>
                </PermissionGate>
                {row.isClosed ? (
                  <PermissionGate resource={canEdit3} minLevel={4}>
                    <button className={styles.actionBtn} onClick={() => setConfirmAction({ type: "open", id: row.id })} title={t("common.reopen")}>
                      <Unlock size={13} />
                    </button>
                  </PermissionGate>
                ) : (
                  <PermissionGate resource={canEdit3} minLevel={3}>
                    <button className={styles.actionBtn} onClick={() => setConfirmAction({ type: "close", id: row.id })} title={t("common.close")}>
                      <Lock size={13} />
                    </button>
                  </PermissionGate>
                )}
              </div>
            ),
          },
        ]}
        data={offerings}
        loading={isLoading}
        error={error?.message}
        emptyIcon={CalendarCheck}
        emptyTitle={t("offerings.noOfferings")}
        emptyMessage={t("offerings.noOfferingsHint")}
        emptyActionLabel={t("offerings.createOffering")}
        emptyAction={() => { setDrawerMode("create"); setEditTarget(null); }}
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
        compact
        tableLabel={t("offerings.title")}
      />

      {!isLoading && !error && (
        <div className={styles.resultInfo}>{totalCount} {t("offerings.title").toLowerCase()}</div>
      )}

      {selectedIds.size > 0 && (
        <div className={shared.bulkBar}>
          <span>{t("common.selected", { count: selectedIds.size })}</span>
          <button onClick={() => setSelectedIds(new Set())}>{t("common.clear")}</button>
          <PermissionGate resource={canEdit3} minLevel={3}>
            <button onClick={handleBulkPublish} disabled={bulkPublish.isPending}>
              <Send size={13} /> {t("offerings.publish")}
            </button>
            <button className={shared.bulkDanger} onClick={() => setBulkCancelOpen(true)} disabled={bulkCancel.isPending}>
              <X size={13} /> {t("common.cancel")}
            </button>
          </PermissionGate>
        </div>
      )}

      <OfferingFormDrawer
        open={!!drawerMode}
        mode={drawerMode}
        offering={editTarget}
        semester={selectedSemesterObj}
        scopeNode={scopeNode}
        onClose={() => { setDrawerMode(null); setEditTarget(null); setServerError(""); }}
        onSubmit={handleSubmit}
        saving={createOffering.isPending || updateOffering.isPending}
        serverError={serverError}
      />

      <BatchSectionsWizard
        open={batchOpen}
        onClose={() => setBatchOpen(false)}
        semester={selectedSemesterObj}
        scopeNode={scopeNode}
      />

      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleLifecycle}
        title={confirmAction?.type === "close" ? t("courses.closeRecord") : t("courses.reopenRecord")}
        message={confirmAction?.type === "close" ? t("courses.closeRecordDetail") : t("courses.reopenRecordDetail")}
        confirmLabel={confirmAction?.type === "close" ? t("common.close") : t("common.reopen")}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closeMut.isPending || openMut.isPending}
      />

      <ConfirmDialog
        open={bulkCancelOpen}
        onClose={() => { setBulkCancelOpen(false); setCancelReason(""); }}
        onConfirm={handleBulkCancel}
        title={t("offerings.cancelOfferings")}
        message={t("offerings.cancelMsg", { count: selectedIds.size })}
        detail={t("offerings.cancelDetail")}
        confirmLabel={t("common.confirm")}
        variant="danger"
        loading={bulkCancel.isPending}
      >
        <div style={{ marginTop: 12 }}>
          <input
            type="text"
            className={shared.formInput}
            style={{ width: "100%", boxSizing: "border-box" }}
            placeholder={t("offerings.cancelReasonPlaceholder")}
            value={cancelReason}
            onChange={(e) => setCancelReason(e.target.value)}
            autoFocus
          />
        </div>
      </ConfirmDialog>
    </>
  );
}
