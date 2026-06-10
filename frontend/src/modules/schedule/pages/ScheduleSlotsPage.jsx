import { useState, useMemo, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { Clock, Plus, Edit2, Trash2, AlertTriangle, RefreshCw, Lock, Unlock, Layers, X, Save, AlertCircle } from "lucide-react";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { DAY_LABELS, SLOT_KIND_LABELS, SLOT_KINDS } from "../../../core/services/scheduleService";
import { useToast } from "../../../core/components/Toast";
import EmptyState from "../../../core/components/EmptyState";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import Drawer from "../../../core/components/Drawer";
import PermissionGate from "../../../core/auth/PermissionGate";
import {
  useScheduleSlots, useCreateSlot, useUpdateSlot, useDeleteSlot,
  useCloseSlot, useOpenSlot, useBatchCreateSlots,
  useOfferingsForSchedule,
} from "../../../core/query/useScheduleSlots";
import {
  findOverlappingSlots, findAllOverlaps, hasOverlap,
  getSlotKindClass, generateTimeIntervals, timeToGridPosition, timeToRowSpan,
} from "../../../core/utils/scheduleOverlap";
import DraggableScheduleGrid from "../components/DraggableScheduleGrid";
import "../styles/scheduleSlots.css";

const START_HOUR = 7;
const END_HOUR = 22;

const EMPTY_BATCH_ENTRY = { dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "" };

function ScheduleSlotsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();

  const [selectedOfferingId, setSelectedOfferingId] = useState("");

  const { data: slots = [], isLoading: slotsLoading } = useScheduleSlots(selectedOfferingId);
  const { data: offerings = [] } = useOfferingsForSchedule(scopeNode?.id, selectedSemesterObj?.id);

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

  const [showBatchForm, setShowBatchForm] = useState(false);
  const [batchEntries, setBatchEntries] = useState([{ ...EMPTY_BATCH_ENTRY }]);
  const [batchOverlaps, setBatchOverlaps] = useState({});

  const intervals = useMemo(() => generateTimeIntervals(START_HOUR, END_HOUR), []);
  const days = useMemo(() => [0, 1, 2, 3, 4, 5, 6], []);

  const selectedOffering = useMemo(
    () => offerings.find((o) => o.id === selectedOfferingId) || null,
    [offerings, selectedOfferingId]
  );

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

  const openCreateDrawer = () => {
    setEditSlot(null);
    setDrawerError("");
    setDrawerMode("create");
  };

  const openEditDrawer = (slot) => {
    setEditSlot(slot);
    setDrawerError("");
    setDrawerMode("edit");
  };

  const closeDrawer = () => {
    setDrawerMode(null);
    setEditSlot(null);
    setDrawerError("");
  };

  const [slotForm, setSlotForm] = useState({
    dayOfWeek: "",
    startTime: "",
    endTime: "",
    kind: "0",
    location: "",
    notes: "",
  });

  const handleDrawerSave = async () => {
    setDrawerError("");
    const formData = {
      dayOfWeek: Number(slotForm.dayOfWeek),
      startTime: slotForm.startTime,
      endTime: slotForm.endTime,
      kind: Number(slotForm.kind),
      location: slotForm.location,
      notes: slotForm.notes,
    };

    if (!formData.dayOfWeek && formData.dayOfWeek !== 0) { setDrawerError("Day is required"); return; }
    if (!formData.startTime) { setDrawerError("Start time is required"); return; }
    if (!formData.endTime) { setDrawerError("End time is required"); return; }
    if (formData.endTime <= formData.startTime) { setDrawerError("End time must be after start time"); return; }

    const overlapCheck = findOverlappingSlots(
      { dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime, excludeId: editSlot?.id },
      slots
    );
    if (overlapCheck.length > 0) {
      setDrawerError(`Time conflict with ${overlapCheck.length} existing slot(s) on ${DAY_LABELS[formData.dayOfWeek]}.`);
      return;
    }

    try {
      if (editSlot) {
        const body = {};
        if (formData.dayOfWeek !== editSlot.dayOfWeek) body.dayOfWeek = formData.dayOfWeek;
        if (formData.startTime !== editSlot.startTime) body.startTime = formData.startTime;
        if (formData.endTime !== editSlot.endTime) body.endTime = formData.endTime;
        if (formData.kind !== editSlot.kind) body.kind = formData.kind;
        if (formData.location !== editSlot.location) body.location = formData.location;
        if (formData.notes !== editSlot.notes) body.notes = formData.notes;
        await updateSlot.mutateAsync({ id: editSlot.id, ...body });
        addToast("Slot updated", "success");
      } else {
        await createSlot.mutateAsync({
          courseOfferingId: selectedOfferingId,
          ...formData,
        });
        addToast("Slot created", "success");
      }
      closeDrawer();
    } catch (err) {
      setDrawerError(err.message || "Failed to save slot");
    }
  };

  const handleDragDropCreate = async (formData) => {
    const overlapCheck = findOverlappingSlots(
      { dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime },
      slots
    );
    if (overlapCheck.length > 0) {
      addToast(`Time conflict with ${overlapCheck.length} existing slot(s)`, "warning");
      return;
    }
    try {
      await createSlot.mutateAsync(formData);
      addToast("Slot created via drag-and-drop", "success");
    } catch (err) {
      addToast(err.message || "Failed to create slot", "error");
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteSlot.mutateAsync(deleteTarget.id);
      addToast("Slot deleted", "success");
      setDeleteTarget(null);
    } catch (err) {
      addToast(err.message || "Failed to delete slot", "error");
      setDeleteTarget(null);
    }
  };

  const handleToggleLifecycle = (slot) => {
    setConfirmTarget(slot);
  };

  const handleClose = async () => {
    if (!confirmTarget) return;
    try {
      await closeSlotMut.mutateAsync(confirmTarget.id);
      addToast("Slot closed", "success");
      setConfirmTarget(null);
    } catch (err) {
      addToast(err.message || "Failed to close slot", "error");
      setConfirmTarget(null);
    }
  };

  const handleOpen = async () => {
    if (!confirmTarget) return;
    try {
      await openSlotMut.mutateAsync(confirmTarget.id);
      addToast("Slot reopened", "success");
      setConfirmTarget(null);
    } catch (err) {
      addToast(err.message || "Failed to reopen slot", "error");
      setConfirmTarget(null);
    }
  };

  const openBatchForm = () => {
    setBatchEntries([{ ...EMPTY_BATCH_ENTRY }]);
    setBatchOverlaps({});
    setShowBatchForm(true);
  };

  const addBatchEntry = () => {
    setBatchEntries((prev) => [...prev, { ...EMPTY_BATCH_ENTRY }]);
  };

  const removeBatchEntry = (idx) => {
    setBatchEntries((prev) => (prev.length > 1 ? prev.filter((_, i) => i !== idx) : prev));
  };

  const updateBatchEntry = (idx, field, value) => {
    setBatchEntries((prev) => prev.map((e, i) => (i === idx ? { ...e, [field]: value } : e)));
    setBatchOverlaps((prev) => {
      const next = { ...prev };
      delete next[idx];
      return next;
    });
  };

  const validateBatchEntries = useCallback(() => {
    const valid = batchEntries.filter((e) => e.startTime && e.endTime && e.endTime > e.startTime);
    if (valid.length === 0) {
      addToast("No valid entries to create", "warning");
      return null;
    }
    const allExisting = [...slots];
    const overlaps = findAllOverlaps(valid, allExisting);
    setBatchOverlaps(overlaps);
    if (Object.keys(overlaps).length > 0) {
      addToast(`${Object.keys(overlaps).length} entry(s) have time conflicts. Review highlights.`, "warning");
      return null;
    }
    return valid;
  }, [batchEntries, slots, addToast]);

  const handleBatchSubmit = async () => {
    const valid = validateBatchEntries();
    if (!valid) return;

    try {
      const result = await batchCreate.mutateAsync({
        offeringId: selectedOfferingId,
        slots: valid.map((e) => ({
          dayOfWeek: Number(e.dayOfWeek),
          startTime: e.startTime,
          endTime: e.endTime,
          kind: Number(e.kind),
          location: e.location.trim() || null,
        })),
      });
      const count = result?.succeeded?.length || valid.length;
      addToast(`${count} slot(s) created`, "success");
      setShowBatchForm(false);
    } catch (err) {
      addToast(err.message || "Batch create failed", "error");
    }
  };

  const renderSlotForm = () => {
    const formData = slotForm;
    const setForm = setSlotForm;

    if (!drawerMode) return null;

    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        {drawerMode === "edit" && editSlot ? (
          <>
            <div className="form-group">
              <label>Day</label>
              <select value={formData.dayOfWeek} onChange={(e) => setForm((p) => ({ ...p, dayOfWeek: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }}>
                {Object.entries(DAY_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div style={{ display: "flex", gap: 12 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Start Time</label>
                <input type="time" value={formData.startTime} onChange={(e) => setForm((p) => ({ ...p, startTime: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label>End Time</label>
                <input type="time" value={formData.endTime} onChange={(e) => setForm((p) => ({ ...p, endTime: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
              </div>
            </div>
            <div className="form-group">
              <label>Kind</label>
              <select value={formData.kind} onChange={(e) => setForm((p) => ({ ...p, kind: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }}>
                {Object.entries(SLOT_KIND_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Room</label>
              <input type="text" value={formData.location} onChange={(e) => setForm((p) => ({ ...p, location: e.target.value }))}
                placeholder="e.g. A201"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
            </div>
            <div className="form-group">
              <label>Notes</label>
              <textarea value={formData.notes} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))}
                rows={2} style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit", resize: "vertical" }} />
            </div>
          </>
        ) : (
          <>
            <div className="form-group">
              <label>Day *</label>
              <select value={formData.dayOfWeek} onChange={(e) => setForm((p) => ({ ...p, dayOfWeek: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }}>
                <option value="">Select day...</option>
                {Object.entries(DAY_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div style={{ display: "flex", gap: 12 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Start Time *</label>
                <input type="time" value={formData.startTime} onChange={(e) => setForm((p) => ({ ...p, startTime: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label>End Time *</label>
                <input type="time" value={formData.endTime} onChange={(e) => setForm((p) => ({ ...p, endTime: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
              </div>
            </div>
            <div className="form-group">
              <label>Kind</label>
              <select value={formData.kind} onChange={(e) => setForm((p) => ({ ...p, kind: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }}>
                {Object.entries(SLOT_KIND_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Room</label>
              <input type="text" value={formData.location} onChange={(e) => setForm((p) => ({ ...p, location: e.target.value }))}
                placeholder="e.g. A201"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
            </div>
          </>
        )}
      </div>
    );
  };

  return (
    <div className="sch-page">
      <div className="sch-header">
        <div className="sch-header-left">
          <Clock size={20} />
          <div>
            <h1>{t("schedule_builder")}</h1>
            <p>{t("manage_slots")}</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <PermissionGate resource="schedule.schedule-slots" minLevel={2}>
            <button className="sch-btn sch-btn-primary" onClick={openBatchForm} disabled={!selectedOfferingId}>
              <Layers size={16} /> Batch
            </button>
          </PermissionGate>
          <PermissionGate resource="schedule.schedule-slots" minLevel={2}>
            <button className="sch-btn sch-btn-primary" onClick={openCreateDrawer} disabled={!selectedOfferingId}>
              <Plus size={16} /> {t("add_slot")}
            </button>
          </PermissionGate>
        </div>
      </div>

      <div className="sch-toolbar">
        <select className="sch-offering-select" value={selectedOfferingId}
          onChange={(e) => setSelectedOfferingId(e.target.value)} aria-label="Select course offering">
          <option value="">{t("select_course_offering")}</option>
          {offerings.map((o) => (
            <option key={o.id} value={o.id}>
              {o.courseCode || "—"} — {o.courseTitle || t("unknown")} ({o.sectionCode})
            </option>
          ))}
        </select>
        {selectedOfferingId && selectedOffering && (
          <div className="sch-offering-badge">
            <strong>{t("capacity")}:</strong> {selectedOffering.registeredCount}/{selectedOffering.capacity}
          </div>
        )}
        {selectedOfferingId && (
          <button className="sch-btn sch-btn-primary" style={{ padding: "6px 12px", fontSize: 12 }}
            onClick={() => { }} title="Refresh">
            <RefreshCw size={12} />
          </button>
        )}
      </div>

      <div aria-live="polite" className="sr-only">
        {slots.length > 0
          ? `${slots.length} slot(s) scheduled. ${Object.keys(slotOverlaps).length} overlap(s) detected.`
          : "No slots scheduled"}
      </div>

      {/* Overlap warning banner */}
      {Object.keys(slotOverlaps).length > 0 && (
        <div className="sch-alert sch-alert-warning" role="alert" style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <AlertCircle size={16} />
          <span style={{ fontSize: 13 }}><strong>{Object.keys(slotOverlaps).length}</strong> overlapping slot(s) detected in the timetable.</span>
        </div>
      )}

      {!selectedOfferingId ? (
        <EmptyState icon={Clock} title="Select an Offering" message={t("select_offering_first")} />
      ) : slotsLoading ? (
        <div className="sch-empty" style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 12 }}>
          <RefreshCw size={24} style={{ animation: "spin 1s linear infinite" }} />
          <p style={{ margin: 0, color: "#6b7280", fontSize: 13 }}>{t("loading_timetable")}</p>
        </div>
      ) : slots.length === 0 ? (
        <EmptyState icon={Clock} title="No Slots Yet" message={t("no_slots_yet")}
          actionLabel={t("add_slot")} onAction={openCreateDrawer} />
      ) : (
        <DraggableScheduleGrid
          slots={slots}
          offerings={offerings}
          selectedOfferingId={selectedOfferingId}
          onSelectOffering={setSelectedOfferingId}
          onEditSlot={openEditDrawer}
          onDeleteSlot={setDeleteTarget}
          onToggleLifecycle={handleToggleLifecycle}
          onCreateSlot={handleDragDropCreate}
          slotOverlaps={slotOverlaps}
          openLoading={openSlotMut.isPending}
          closeLoading={closeSlotMut.isPending}
          createLoading={createSlot.isPending}
        />
      )}

      {/* Slot Drawer */}
      <Drawer
        open={!!drawerMode}
        onClose={closeDrawer}
        title={drawerMode === "create" ? "Create Slot" : "Edit Slot"}
        width={440}
        loading={createSlot.isPending || updateSlot.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={closeDrawer}>Cancel</button>
            <button className="btn-primary" onClick={handleDrawerSave} disabled={createSlot.isPending || updateSlot.isPending}>
              <Save size={14} /> {createSlot.isPending || updateSlot.isPending ? "Saving..." : drawerMode === "create" ? "Create" : "Update"}
            </button>
          </>
        }
      >
        {drawerError && (
          <div role="alert" style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16 }}>
            {drawerError}
          </div>
        )}
        {renderSlotForm()}
      </Drawer>

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title="Delete Slot"
        message={`Delete ${DAY_LABELS[deleteTarget?.dayOfWeek]} ${deleteTarget?.startTime?.slice(0, 5)}-${deleteTarget?.endTime?.slice(0, 5)}?`}
        confirmLabel="Delete"
        variant="danger"
        loading={deleteSlot.isPending}
      />

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmTarget}
        onClose={() => setConfirmTarget(null)}
        onConfirm={confirmTarget?.isClosed ? handleOpen : handleClose}
        title={confirmTarget?.isClosed ? "Reopen Slot" : "Close Slot"}
        message={`${confirmTarget?.isClosed ? "Reopen" : "Close"} ${DAY_LABELS[confirmTarget?.dayOfWeek]} ${confirmTarget?.startTime?.slice(0, 5)}-${confirmTarget?.endTime?.slice(0, 5)}?`}
        confirmLabel={confirmTarget?.isClosed ? "Reopen" : "Close"}
        variant={confirmTarget?.isClosed ? "default" : "warning"}
        loading={closeSlotMut.isPending || openSlotMut.isPending}
      />

      {/* Batch Create Form */}
      {showBatchForm && (
        <div className="sch-confirm-overlay" role="dialog" aria-modal="true" aria-label="Batch create slots" onClick={() => !batchCreate.isPending && setShowBatchForm(false)}>
          <div className="sch-confirm-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 600, maxHeight: "80vh", overflowY: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <h3 style={{ margin: 0 }}>Batch Create Slots</h3>
              <button className="btn-cancel" style={{ padding: "4px 8px" }} onClick={() => setShowBatchForm(false)}><X size={16} /></button>
            </div>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 16px" }}>
              Add multiple schedule slots for the selected offering. Red-bordered entries have time conflicts.
            </p>

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
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>Day</label>
                    <select value={entry.dayOfWeek} onChange={(e) => updateBatchEntry(idx, "dayOfWeek", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }}>
                      {Object.entries(DAY_LABELS).map(([val, label]) => (
                        <option key={val} value={val}>{label}</option>
                      ))}
                    </select>
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>Start</label>
                    <input type="time" value={entry.startTime} onChange={(e) => updateBatchEntry(idx, "startTime", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>End</label>
                    <input type="time" value={entry.endTime} onChange={(e) => updateBatchEntry(idx, "endTime", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>Kind</label>
                    <select value={entry.kind} onChange={(e) => updateBatchEntry(idx, "kind", e.target.value)}
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }}>
                      {Object.entries(SLOT_KIND_LABELS).map(([val, label]) => (
                        <option key={val} value={val}>{label}</option>
                      ))}
                    </select>
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <label style={{ fontSize: 10, fontWeight: 600, color: "#6b7280", display: "block", marginBottom: 2 }}>Room</label>
                    <input type="text" value={entry.location} onChange={(e) => updateBatchEntry(idx, "location", e.target.value)}
                      placeholder="e.g. A201"
                      style={{ width: "100%", padding: "6px 8px", border: "1px solid #d1d5db", borderRadius: 6, fontSize: 12, fontFamily: "inherit" }} />
                  </div>
                  {hasConflict && (
                    <div style={{ flexShrink: 0, color: "#dc2626", fontSize: 10, fontWeight: 600, textAlign: "center" }}>
                      <AlertCircle size={14} />
                      <span>Conflict</span>
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
                <Plus size={12} /> Add Row
              </button>
            </div>

            {Object.keys(batchOverlaps).length > 0 && (
              <div style={{ marginTop: 12, padding: "8px 12px", background: "#fef2f2", borderRadius: 8, border: "1px solid #fecaca", fontSize: 12, color: "#b91c1c" }}>
                <AlertTriangle size={12} style={{ marginRight: 4 }} />
                {Object.keys(batchOverlaps).length} entry(s) have time conflicts with existing slots.
              </div>
            )}

            <div className="sch-confirm-actions" style={{ marginTop: 16 }}>
              <button className="btn-cancel" onClick={() => setShowBatchForm(false)} disabled={batchCreate.isPending}>Cancel</button>
              <button className="btn-primary" onClick={handleBatchSubmit} disabled={batchCreate.isPending}>
                <Save size={14} /> {batchCreate.isPending ? "Creating..." : `Create ${batchEntries.length} Slot(s)`}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ScheduleSlotsPage;
