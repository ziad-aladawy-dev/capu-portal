import { useState, useMemo, useCallback, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router-dom";
import { Clock, Plus, Trash2, AlertTriangle, RefreshCw, Layers, X, Save, AlertCircle, CalendarDays } from "lucide-react";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { useToast } from "../../../../core/components/Toast";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import Drawer from "../../../../core/components/Drawer";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { getLocalized } from "../../../../core/utils/getLocalized";
import {
  useScheduleSlots, useCreateSlot, useUpdateSlot, useDeleteSlot,
  useCloseSlot, useOpenSlot, useBatchCreateSlots,
  useOfferingsForSchedule, useSlotsForOfferings,
} from "../../../../core/query/useScheduleSlots";
import { useActiveCourses } from "../../../../core/query/useCourses";
import { useStaffOptions } from "../CourseOfferings/useStaffOptions";
import { findOverlappingSlots, findAllOverlaps, findIntraBatchOverlaps } from "../../../../core/utils/scheduleOverlap";
import DraggableScheduleGrid from "./DraggableScheduleGrid";
import ScheduleSlotForm from "./ScheduleSlotForm";
import "./scheduleSlots.css";

const EMPTY_BATCH_ENTRY = { dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "" };

function ScheduleSlotsPage() {
  const { t, i18n } = useTranslation("academic");
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();
  const [searchParams, setSearchParams] = useSearchParams();

  const [selectedOfferingId, setSelectedOfferingId] = useState(searchParams.get("offeringId") || "");

  const {
    data: slots = [],
    isLoading: slotsLoading,
    isError: slotsError,
    refetch: refetchSlots,
  } = useScheduleSlots(selectedOfferingId);
  const { data: offerings = [], isLoading: offeringsLoading } =
    useOfferingsForSchedule(scopeNode?.id, selectedSemesterObj?.id);
  const { data: courses = [] } = useActiveCourses();

  // Slots of EVERY offering in scope — feeds the sidebar's per-offering slot
  // counts, the ghost blocks on the grid, and cross-offering room clashes.
  const offeringIds = useMemo(() => offerings.map((o) => o.id), [offerings]);
  const { slotsByOffering } = useSlotsForOfferings(offeringIds);

  // Deep link from the Offerings page (?offeringId=…) — honour once on load.
  useEffect(() => {
    const fromUrl = searchParams.get("offeringId");
    if (fromUrl && fromUrl !== selectedOfferingId) setSelectedOfferingId(fromUrl);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const pickOffering = (id) => {
    setSelectedOfferingId(id);
    setSearchParams(id ? { offeringId: id } : {}, { replace: true });
  };

  const courseById = useMemo(() => {
    const m = {};
    for (const c of courses) m[c.id] = c;
    return m;
  }, [courses]);

  // Instructor names for section rows — sections of one course often differ
  // only by who teaches them (Dr. Ahmed's 8:00 vs Dr. Sara's 9:45).
  const { data: staffOptions = [] } = useStaffOptions();
  const staffById = useMemo(() => {
    const m = {};
    for (const s of staffOptions) m[s.id] = s;
    return m;
  }, [staffOptions]);

  // Human-readable "Lecture Monday 10:00–12:00 • Lab …" list for conflict
  // feedback — every clash, not just the first.
  const describeConflicts = (conflicts) =>
    conflicts
      .map((c) =>
        `${t(`schedule.kinds.${c.kind}`)} ${t(`schedule.days.${c.dayOfWeek}`)} ${String(c.startTime).slice(0, 5)}–${String(c.endTime).slice(0, 5)}`)
      .join(" • ");

  const createSlot = useCreateSlot();
  const updateSlot = useUpdateSlot();
  const deleteSlot = useDeleteSlot();
  const closeSlotMut = useCloseSlot();
  const openSlotMut = useOpenSlot();
  const batchCreate = useBatchCreateSlots();

  const [drawerMode, setDrawerMode] = useState(null);
  const [editSlot, setEditSlot] = useState(null);
  const [drawerError, setDrawerError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [confirmTarget, setConfirmTarget] = useState(null);
  const submitRef = useRef(null);

  const [showBatchForm, setShowBatchForm] = useState(false);
  const [batchEntries, setBatchEntries] = useState([{ ...EMPTY_BATCH_ENTRY }]);
  const [batchOverlaps, setBatchOverlaps] = useState({});

  const selectedOffering = useMemo(
    () => offerings.find((o) => o.id === selectedOfferingId) || null,
    [offerings, selectedOfferingId]
  );

  // Sidebar payload: offerings carrying their course identity + live slot
  // count, so the panel reads "CS101 — Intro to CS (B) · 2 slots" instead of
  // a bare section letter.
  const enrichedOfferings = useMemo(
    () =>
      offerings.map((o) => {
        const course = courseById[o.courseId];
        return {
          ...o,
          courseCode: course?.code || "—",
          courseTitle: course?.title || "",
          instructorName: o.instructorId ? staffById[o.instructorId]?.name || "" : "",
          slotCount: (slotsByOffering[o.id] || []).filter((s) => !s.isClosed).length,
        };
      }),
    [offerings, courseById, slotsByOffering, staffById]
  );

  // Open slots of every OTHER offering — rendered as read-only ghost blocks
  // so the scheduler sees the week's real occupancy while editing one offering.
  const backgroundSlots = useMemo(() => {
    const out = [];
    for (const o of enrichedOfferings) {
      if (o.id === selectedOfferingId) continue;
      for (const s of slotsByOffering[o.id] || []) {
        if (!s.isClosed) out.push({ ...s, courseCode: o.courseCode, sectionCode: o.sectionCode });
      }
    }
    return out;
  }, [enrichedOfferings, slotsByOffering, selectedOfferingId]);

  const [showOthers, setShowOthers] = useState(true);

  // Dropdown mirror of the sidebar's course grouping: one <optgroup> per
  // course, sections (A/B/EVE-1 …) as its options in natural order.
  const dropdownGroups = useMemo(() => {
    const byCourse = new Map();
    for (const o of enrichedOfferings) {
      if (!byCourse.has(o.courseId)) {
        byCourse.set(o.courseId, {
          courseId: o.courseId, courseCode: o.courseCode, courseTitle: o.courseTitle, sections: [],
        });
      }
      byCourse.get(o.courseId).sections.push(o);
    }
    const groups = [...byCourse.values()];
    for (const g of groups) {
      g.sections.sort((a, b) =>
        String(a.sectionCode).localeCompare(String(b.sectionCode), undefined, { numeric: true }));
    }
    groups.sort((a, b) => String(a.courseCode).localeCompare(String(b.courseCode)));
    return groups;
  }, [enrichedOfferings]);

  // Cross-offering room guard: same room, intersecting window, any offering.
  // The backend stores location as free text and does not police rooms, so
  // this is the only line of defence against double-booking a room.
  const findRoomClashes = ({ dayOfWeek, startTime, endTime, location, excludeId }) => {
    if (!location) return [];
    const all = [...slots, ...backgroundSlots];
    return findOverlappingSlots({ dayOfWeek, startTime, endTime, excludeId }, all)
      .filter((s) => (s.location || "").trim().toLowerCase() === location.trim().toLowerCase());
  };

  const slotOverlaps = useMemo(() => {
    const conflictMap = {};
    for (let i = 0; i < slots.length; i++) {
      const conflicts = findOverlappingSlots(
        { ...slots[i], excludeId: slots[i].id },
        slots
      );
      if (conflicts.length > 0) {
        conflictMap[slots[i].id] = conflicts;
      }
    }
    return conflictMap;
  }, [slots]);

  // createPrefill: pseudo-slot used to pre-seed the create form when the user
  // clicks an empty timetable cell (drag-free scheduling path).
  const [createPrefill, setCreatePrefill] = useState(null);

  const openCreateDrawer = (prefill = null) => {
    setEditSlot(null);
    setCreatePrefill(prefill);
    setDrawerError("");
    setDrawerMode("create");
  };
  const openEditDrawer = (slot) => { setEditSlot(slot); setDrawerError(""); setDrawerMode("edit"); };
  const closeDrawer = () => { setDrawerMode(null); setEditSlot(null); setCreatePrefill(null); setDrawerError(""); };

  // Click an empty grid cell → create drawer pre-filled with that day/time.
  const handleCellClick = (dayOfWeek, startTime, endTime) => {
    if (!selectedOfferingId) {
      addToast(t("schedule.noOffering"), "info");
      return;
    }
    openCreateDrawer({ dayOfWeek, startTime, endTime });
  };

  const handleDrawerSubmit = async (formData) => {
    setDrawerError("");

    const overlapCheck = findOverlappingSlots(
      { dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime, excludeId: editSlot?.id },
      slots
    );
    if (overlapCheck.length > 0) {
      setDrawerError(
        `${t("schedule.conflicts", { count: overlapCheck.length })}: ${describeConflicts(overlapCheck)}`
      );
      return;
    }

    const roomClash = findRoomClashes({
      dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime,
      location: formData.location, excludeId: editSlot?.id,
    });
    if (roomClash.length > 0) {
      const c = roomClash[0];
      setDrawerError(t("schedule.roomClash", {
        room: formData.location,
        code: c.courseCode || t("schedule.anotherOffering"),
        start: String(c.startTime).slice(0, 5),
        end: String(c.endTime).slice(0, 5),
      }));
      return;
    }

    try {
      if (editSlot) {
        const body = {};
        if (formData.dayOfWeek !== editSlot.dayOfWeek) body.dayOfWeek = formData.dayOfWeek;
        if (formData.startTime !== String(editSlot.startTime).slice(0, 5)) body.startTime = formData.startTime;
        if (formData.endTime !== String(editSlot.endTime).slice(0, 5)) body.endTime = formData.endTime;
        if (formData.kind !== editSlot.kind) body.kind = formData.kind;
        if (formData.location !== (editSlot.location || "")) body.location = formData.location;
        if (formData.notes !== (editSlot.notes || "")) body.notes = formData.notes;
        await updateSlot.mutateAsync({ id: editSlot.id, ...body });
        addToast(t("schedule.slotUpdated"), "success");
      } else {
        await createSlot.mutateAsync({
          courseOfferingId: selectedOfferingId,
          ...formData,
        });
        addToast(t("schedule.slotCreated"), "success");
      }
      closeDrawer();
    } catch (err) {
      setDrawerError(err.message || t("courses.saveFailed"));
    }
  };

  const handleDragDropCreate = async (formData) => {
    const overlapCheck = findOverlappingSlots(
      { dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime },
      slots
    );
    if (overlapCheck.length > 0) {
      addToast(`${t("schedule.conflicts", { count: overlapCheck.length })} — ${describeConflicts(overlapCheck)}`, "warning");
      return;
    }
    try {
      await createSlot.mutateAsync(formData);
      addToast(t("schedule.slotCreated"), "success");
      // Dropping an offering that wasn't the selected one: select it so the
      // freshly created slot is immediately visible and editable.
      if (formData.courseOfferingId && formData.courseOfferingId !== selectedOfferingId) {
        pickOffering(formData.courseOfferingId);
      }
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    }
  };

  // Drag an existing slot to another grid cell — keeps the slot's duration,
  // pre-checks overlaps (every clash listed), then PATCHes day/start/end.
  const handleMoveSlot = async ({ slot, dayOfWeek, startTime, endTime }) => {
    const conflicts = findOverlappingSlots(
      { dayOfWeek, startTime, endTime, excludeId: slot.id },
      slots
    );
    if (conflicts.length > 0) {
      addToast(`${t("schedule.conflicts", { count: conflicts.length })} — ${describeConflicts(conflicts)}`, "warning");
      return;
    }
    const roomClash = findRoomClashes({
      dayOfWeek, startTime, endTime, location: slot.location, excludeId: slot.id,
    });
    if (roomClash.length > 0) {
      const c = roomClash[0];
      addToast(t("schedule.roomClash", {
        room: slot.location,
        code: c.courseCode || t("schedule.anotherOffering"),
        start: String(c.startTime).slice(0, 5),
        end: String(c.endTime).slice(0, 5),
      }), "warning");
      return;
    }
    try {
      await updateSlot.mutateAsync({ id: slot.id, dayOfWeek, startTime, endTime });
      addToast(t("schedule.slotMoved"), "success");
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteSlot.mutateAsync(deleteTarget.id);
      addToast(t("schedule.slotDeleted"), "success");
      setDeleteTarget(null);
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
      setDeleteTarget(null);
    }
  };

  const handleToggleLifecycle = (slot) => setConfirmTarget(slot);

  const handleLifecycleConfirm = async () => {
    if (!confirmTarget) return;
    const mut = confirmTarget.isClosed ? openSlotMut : closeSlotMut;
    try {
      await mut.mutateAsync(confirmTarget.id);
      addToast(confirmTarget.isClosed ? t("schedule.slotReopened") : t("schedule.slotClosed"), "success");
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    } finally {
      setConfirmTarget(null);
    }
  };

  const openBatchForm = () => {
    setBatchEntries([{ ...EMPTY_BATCH_ENTRY }]);
    setBatchOverlaps({});
    setShowBatchForm(true);
  };

  const addBatchEntry = () => setBatchEntries((prev) => [...prev, { ...EMPTY_BATCH_ENTRY }]);
  const removeBatchEntry = (idx) =>
    setBatchEntries((prev) => (prev.length > 1 ? prev.filter((_, i) => i !== idx) : prev));
  const updateBatchEntry = (idx, field, value) => {
    setBatchEntries((prev) => prev.map((e, i) => (i === idx ? { ...e, [field]: value } : e)));
    setBatchOverlaps((prev) => {
      const next = { ...prev };
      delete next[idx];
      return next;
    });
  };

  const validateBatchEntries = useCallback(() => {
    // 1) Every row needs a valid time range — flag offenders by ORIGINAL index
    //    so the red border lands on the right row.
    const invalid = {};
    batchEntries.forEach((e, idx) => {
      if (!e.startTime || !e.endTime || e.endTime <= e.startTime) invalid[idx] = ["time"];
    });
    if (Object.keys(invalid).length > 0) {
      setBatchOverlaps(invalid);
      addToast(t("schedule.validation.endAfterStart"), "warning");
      return null;
    }
    // 2) Conflicts against existing slots (index-aligned with batchEntries)…
    const existing = findAllOverlaps(batchEntries, slots);
    // 3) …and against EACH OTHER — the server applies rows one by one, so an
    //    internally clashing batch would otherwise half-succeed.
    const intra = findIntraBatchOverlaps(batchEntries);
    const merged = { ...existing };
    for (const [idx, others] of Object.entries(intra)) {
      merged[idx] = [...(merged[idx] || []), ...others];
    }
    setBatchOverlaps(merged);
    if (Object.keys(merged).length > 0) {
      addToast(t("schedule.conflicts", { count: Object.keys(merged).length }), "warning");
      return null;
    }
    return batchEntries;
  }, [batchEntries, slots, addToast, t]);

  const handleBatchSubmit = async () => {
    const valid = validateBatchEntries();
    if (!valid) return;

    try {
      await batchCreate.mutateAsync({
        offeringId: selectedOfferingId,
        slots: valid.map((e) => ({
          dayOfWeek: Number(e.dayOfWeek),
          startTime: e.startTime,
          endTime: e.endTime,
          kind: Number(e.kind),
          location: e.location.trim() || null,
        })),
      });
      addToast(t("schedule.slotCreated"), "success");
      setShowBatchForm(false);
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    }
  };

  return (
    <>
    <div className="sch-page" style={{ padding: 0 }}>
      <div className="sch-header">
        <div className="sch-header-left">
          <Clock size={20} />
          <div>
            <h1>{t("schedule.title")}</h1>
            <p>{t("schedule.subtitle")}</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <PermissionGate resource="schedule.schedule-slots" minLevel={2}>
            <button className="sch-btn sch-btn-primary" onClick={openBatchForm} disabled={!selectedOfferingId}>
              <Layers size={16} /> {t("offerings.batchCreate")}
            </button>
          </PermissionGate>
          <PermissionGate resource="schedule.schedule-slots" minLevel={2}>
            <button className="sch-btn sch-btn-primary" onClick={() => openCreateDrawer()} disabled={!selectedOfferingId}>
              <Plus size={16} /> {t("schedule.addSlot")}
            </button>
          </PermissionGate>
        </div>
      </div>

      <div className="sch-toolbar">
        {/* Active semester context — the offering list is scoped to it, so
            make the scope visible instead of implicit. */}
        <div className={`sch-context-chip${selectedSemesterObj ? "" : " is-missing"}`}
          title={t("common.semester")}>
          <CalendarDays size={13} />
          <span>
            {selectedSemesterObj
              ? getLocalized(selectedSemesterObj.name, i18n.language)
              : t("common.noSemester")}
          </span>
        </div>

        <label className="sch-select-label" htmlFor="sch-offering-select">
          {t("schedule.offering")}
        </label>
        <select id="sch-offering-select" className="sch-offering-select" value={selectedOfferingId}
          onChange={(e) => pickOffering(e.target.value)} aria-label={t("schedule.selectOffering")}
          disabled={!selectedSemesterObj}>
          <option value="">
            {t("schedule.selectOffering")}{offerings.length ? ` (${offerings.length})` : ""}
          </option>
          {dropdownGroups.map((g) => (
            <optgroup key={g.courseId} label={`${g.courseCode} — ${g.courseTitle}`}>
              {g.sections.map((o) => (
                <option key={o.id} value={o.id}>
                  {t("schedule.section")} {o.sectionCode}
                  {o.instructorName ? ` · ${o.instructorName}` : ""}
                  {o.slotCount > 0 ? ` — ${t("schedule.slotsCount", { count: o.slotCount })}` : ` — ${t("schedule.unscheduled")}`}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        {selectedOfferingId && selectedOffering && (
          <div className="sch-offering-badge">
            <strong>{t("offerings.capacity")}:</strong> {selectedOffering.registeredCount}/{selectedOffering.capacity}
          </div>
        )}
        {selectedOfferingId && (
          <button className="sch-btn sch-btn-primary" style={{ padding: "6px 12px", fontSize: 12 }}
            onClick={() => refetchSlots()} title={t("common.retry")} aria-label={t("common.retry")}>
            <RefreshCw size={12} />
          </button>
        )}
        <label className="sch-others-toggle">
          <input type="checkbox" checked={showOthers} onChange={(e) => setShowOthers(e.target.checked)} />
          {t("schedule.showOthers")}
        </label>
      </div>

      <p className="sch-howto">{t("schedule.howTo")}</p>

      <div aria-live="polite" className="sr-only">
        {slots.length > 0
          ? `${slots.length} — ${t("schedule.conflicts", { count: Object.keys(slotOverlaps).length })}`
          : t("schedule.noSlots")}
      </div>

      {/* Overlap warning banner */}
      {Object.keys(slotOverlaps).length > 0 && (
        <div className="sch-alert sch-alert-warning" role="alert" style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <AlertCircle size={16} />
          <span style={{ fontSize: 13 }}>{t("schedule.conflicts", { count: Object.keys(slotOverlaps).length })}</span>
        </div>
      )}

      {!selectedSemesterObj ? (
        <EmptyState icon={CalendarDays} title={t("common.semester")} message={t("common.noSemester")} />
      ) : offeringsLoading ? (
        <LoadingSpinner />
      ) : offerings.length === 0 ? (
        <EmptyState icon={Clock} title={t("schedule.offering")} message={t("schedule.noOfferingsInScope")} />
      ) : slotsError ? (
        <EmptyState
          icon={AlertTriangle}
          title={t("schedule.loadFailed")}
          message={t("schedule.loadFailedHint")}
          actionLabel={t("common.retry")}
          onAction={() => refetchSlots()}
        />
      ) : slotsLoading && selectedOfferingId ? (
        <LoadingSpinner />
      ) : (
        <DraggableScheduleGrid
          slots={selectedOfferingId ? slots : []}
          offerings={enrichedOfferings}
          backgroundSlots={showOthers ? backgroundSlots : []}
          selectedOfferingId={selectedOfferingId}
          onSelectOffering={pickOffering}
          onEditSlot={openEditDrawer}
          onDeleteSlot={setDeleteTarget}
          onToggleLifecycle={handleToggleLifecycle}
          onCreateSlot={handleDragDropCreate}
          onMoveSlot={handleMoveSlot}
          onCellClick={handleCellClick}
          slotOverlaps={slotOverlaps}
          openLoading={openSlotMut.isPending}
          closeLoading={closeSlotMut.isPending}
          createLoading={createSlot.isPending}
        />
      )}

      {/* Slot Drawer — shared ScheduleSlotForm */}
      <Drawer
        open={!!drawerMode}
        onClose={closeDrawer}
        title={drawerMode === "create" ? t("schedule.addSlot") : t("schedule.editSlot")}
        width={440}
        loading={createSlot.isPending || updateSlot.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={closeDrawer}>{t("common.cancel")}</button>
            <button className="btn-primary" onClick={() => submitRef.current?.()} disabled={createSlot.isPending || updateSlot.isPending}>
              <Save size={14} /> {createSlot.isPending || updateSlot.isPending ? t("common.saving") : drawerMode === "create" ? t("common.create") : t("common.save")}
            </button>
          </>
        }
      >
        {drawerMode && (
          <ScheduleSlotForm
            key={drawerMode === "edit"
              ? `edit-${editSlot?.id}`
              : `create-${createPrefill ? `${createPrefill.dayOfWeek}-${createPrefill.startTime}` : "blank"}`}
            slot={drawerMode === "edit" ? editSlot : createPrefill}
            onSubmit={handleDrawerSubmit}
            submitRef={submitRef}
            extraError={drawerError}
          />
        )}
      </Drawer>

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title={t("schedule.deleteSlot")}
        message={t("schedule.deleteSlotMsg", {
          kind: deleteTarget ? t(`schedule.kinds.${deleteTarget.kind}`) : "",
          day: deleteTarget ? t(`schedule.days.${deleteTarget.dayOfWeek}`) : "",
        })}
        confirmLabel={t("common.delete")}
        variant="danger"
        loading={deleteSlot.isPending}
      />

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmTarget}
        onClose={() => setConfirmTarget(null)}
        onConfirm={handleLifecycleConfirm}
        title={confirmTarget?.isClosed ? t("courses.reopenRecord") : t("courses.closeRecord")}
        message={`${confirmTarget?.isClosed ? t("common.reopen") : t("common.close")} ${confirmTarget ? t(`schedule.days.${confirmTarget.dayOfWeek}`) : ""} ${confirmTarget?.startTime?.slice(0, 5)}-${confirmTarget?.endTime?.slice(0, 5)}?`}
        confirmLabel={confirmTarget?.isClosed ? t("common.reopen") : t("common.close")}
        variant={confirmTarget?.isClosed ? "default" : "warning"}
        loading={closeSlotMut.isPending || openSlotMut.isPending}
      />

      {/* Batch Create Form */}
      {showBatchForm && (
        <div className="sch-confirm-overlay" role="dialog" aria-modal="true" aria-label={t("offerings.batchCreate")} onClick={() => !batchCreate.isPending && setShowBatchForm(false)}>
          <div className="sch-confirm-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 600, maxHeight: "80vh", overflowY: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <h3 style={{ margin: 0 }}>{t("offerings.batchCreate")}</h3>
              <button className="btn-cancel" style={{ padding: "4px 8px" }} onClick={() => setShowBatchForm(false)}><X size={16} /></button>
            </div>

            {batchEntries.map((entry, idx) => {
              const hasConflict = batchOverlaps[idx]?.length > 0;
              return (
                <div key={idx} style={{
                  display: "flex", gap: 8, alignItems: "flex-end", marginBottom: 10,
                  padding: 10, background: "#f9fafb", borderRadius: 8,
                  border: `1px solid ${hasConflict ? "#fecaca" : "#e5e7eb"}`,
                  boxShadow: hasConflict ? "0 0 0 1px #ef4444" : undefined,
                }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>{t("schedule.day")}</label>
                    <select value={entry.dayOfWeek} onChange={(e) => updateBatchEntry(idx, "dayOfWeek", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }}>
                      {[0, 1, 2, 3, 4, 5, 6].map((d) => (
                        <option key={d} value={d}>{t(`schedule.days.${d}`)}</option>
                      ))}
                    </select>
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>{t("schedule.startTime")}</label>
                    <input type="time" value={entry.startTime} onChange={(e) => updateBatchEntry(idx, "startTime", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>{t("schedule.endTime")}</label>
                    <input type="time" value={entry.endTime} onChange={(e) => updateBatchEntry(idx, "endTime", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>{t("schedule.kind")}</label>
                    <select value={entry.kind} onChange={(e) => updateBatchEntry(idx, "kind", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }}>
                      {[0, 1, 2, 3, 4, 5].map((k) => (
                        <option key={k} value={k}>{t(`schedule.kinds.${k}`)}</option>
                      ))}
                    </select>
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>{t("schedule.location")}</label>
                    <input type="text" value={entry.location} onChange={(e) => updateBatchEntry(idx, "location", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  {hasConflict && (
                    <div style={{ flexShrink: 0, color: "#dc2626", fontSize: 10, fontWeight: 600, textAlign: "center" }}>
                      <AlertCircle size={14} />
                    </div>
                  )}
                  <button className="sch-slot-action-btn danger" onClick={() => removeBatchEntry(idx)}
                    style={{ width: 28, height: 28, flexShrink: 0, marginBottom: 0 }}>
                    <Trash2 size={12} />
                  </button>
                </div>
              );
            })}

            <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
              <button className="btn-cancel" style={{ fontSize: 12 }} onClick={addBatchEntry}>
                <Plus size={12} /> {t("common.create")}
              </button>
            </div>

            {Object.keys(batchOverlaps).length > 0 && (
              <div style={{ marginTop: 12, padding: "8px 12px", background: "#fef2f2", borderRadius: 8, border: "1px solid #fecaca", fontSize: 12, color: "#b91c1c" }}>
                <AlertTriangle size={12} style={{ marginRight: 4 }} />
                {t("schedule.conflicts", { count: Object.keys(batchOverlaps).length })}
              </div>
            )}

            <div className="sch-confirm-actions" style={{ marginTop: 16 }}>
              <button className="btn-cancel" onClick={() => setShowBatchForm(false)} disabled={batchCreate.isPending}>{t("common.cancel")}</button>
              <button className="btn-primary" onClick={handleBatchSubmit} disabled={batchCreate.isPending}>
                <Save size={14} /> {batchCreate.isPending ? t("common.saving") : t("common.create")}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
    </>
  );
}

export default ScheduleSlotsPage;
