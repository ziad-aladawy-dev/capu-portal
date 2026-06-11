import { useState, useMemo, useCallback, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router-dom";
import { Clock, Plus, Trash2, AlertTriangle, RefreshCw, Layers, X, Save, AlertCircle } from "lucide-react";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { useToast } from "../../../../core/components/Toast";
import EmptyState from "../../../../core/components/EmptyState";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import Drawer from "../../../../core/components/Drawer";
import PermissionGate from "../../../../core/auth/PermissionGate";
import {
  useScheduleSlots, useCreateSlot, useUpdateSlot, useDeleteSlot,
  useCloseSlot, useOpenSlot, useBatchCreateSlots,
  useOfferingsForSchedule,
} from "../../../../core/query/useScheduleSlots";
import { useActiveCourses } from "../../../../core/query/useCourses";
import { findOverlappingSlots, findAllOverlaps } from "../../../../core/utils/scheduleOverlap";
import AcademicShell from "../../layout/AcademicShell";
import DraggableScheduleGrid from "./DraggableScheduleGrid";
import ScheduleSlotForm from "./ScheduleSlotForm";
import "./scheduleSlots.css";

const EMPTY_BATCH_ENTRY = { dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "" };

function ScheduleSlotsPage() {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();
  const [searchParams, setSearchParams] = useSearchParams();

  const [selectedOfferingId, setSelectedOfferingId] = useState(searchParams.get("offeringId") || "");

  const { data: slots = [], isLoading: slotsLoading, refetch: refetchSlots } = useScheduleSlots(selectedOfferingId);
  const { data: offerings = [] } = useOfferingsForSchedule(scopeNode?.id, selectedSemesterObj?.id);
  const { data: courses = [] } = useActiveCourses();

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

  const openCreateDrawer = () => { setEditSlot(null); setDrawerError(""); setDrawerMode("create"); };
  const openEditDrawer = (slot) => { setEditSlot(slot); setDrawerError(""); setDrawerMode("edit"); };
  const closeDrawer = () => { setDrawerMode(null); setEditSlot(null); setDrawerError(""); };

  const handleDrawerSubmit = async (formData) => {
    setDrawerError("");

    const overlapCheck = findOverlappingSlots(
      { dayOfWeek: formData.dayOfWeek, startTime: formData.startTime, endTime: formData.endTime, excludeId: editSlot?.id },
      slots
    );
    if (overlapCheck.length > 0) {
      const c = overlapCheck[0];
      setDrawerError(t("schedule.overlapWarning", {
        code: t(`schedule.kinds.${c.kind}`),
        day: t(`schedule.days.${formData.dayOfWeek}`),
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
      addToast(t("schedule.conflicts", { count: overlapCheck.length }), "warning");
      return;
    }
    try {
      await createSlot.mutateAsync(formData);
      addToast(t("schedule.slotCreated"), "success");
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
    const valid = batchEntries.filter((e) => e.startTime && e.endTime && e.endTime > e.startTime);
    if (valid.length === 0) {
      addToast(t("schedule.validation.endAfterStart"), "warning");
      return null;
    }
    const overlaps = findAllOverlaps(valid, [...slots]);
    setBatchOverlaps(overlaps);
    if (Object.keys(overlaps).length > 0) {
      addToast(t("schedule.conflicts", { count: Object.keys(overlaps).length }), "warning");
      return null;
    }
    return valid;
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
    <AcademicShell>
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
            <button className="sch-btn sch-btn-primary" onClick={openCreateDrawer} disabled={!selectedOfferingId}>
              <Plus size={16} /> {t("schedule.addSlot")}
            </button>
          </PermissionGate>
        </div>
      </div>

      <div className="sch-toolbar">
        <select className="sch-offering-select" value={selectedOfferingId}
          onChange={(e) => pickOffering(e.target.value)} aria-label={t("schedule.selectOffering")}>
          <option value="">{t("schedule.selectOffering")}</option>
          {offerings.map((o) => {
            const course = courseById[o.courseId];
            return (
              <option key={o.id} value={o.id}>
                {course?.code || "—"} — {course?.title || ""} ({o.sectionCode})
              </option>
            );
          })}
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
      </div>

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

      {!selectedOfferingId ? (
        <EmptyState icon={Clock} title={t("schedule.offering")} message={t("schedule.noOffering")} />
      ) : slotsLoading ? (
        <div className="sch-empty" style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 12 }}>
          <RefreshCw size={24} style={{ animation: "spin 1s linear infinite" }} />
          <p style={{ margin: 0, color: "#6b7280", fontSize: 13 }}>{t("common.loading")}</p>
        </div>
      ) : slots.length === 0 ? (
        <EmptyState icon={Clock} title={t("schedule.noSlots")} message={t("schedule.noSlots")}
          actionLabel={t("schedule.addSlot")} onAction={openCreateDrawer} />
      ) : (
        <DraggableScheduleGrid
          slots={slots}
          offerings={offerings}
          selectedOfferingId={selectedOfferingId}
          onSelectOffering={pickOffering}
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
            slot={drawerMode === "edit" ? editSlot : null}
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
    </AcademicShell>
  );
}

export default ScheduleSlotsPage;
