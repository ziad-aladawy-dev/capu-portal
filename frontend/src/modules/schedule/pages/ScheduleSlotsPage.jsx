import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Clock, Plus, Edit2, Trash2, AlertTriangle, RefreshCw, Lock, Unlock, Layers, X, Save } from "lucide-react";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { DAY_LABELS, SLOT_KIND_LABELS, SLOT_KINDS, closeSlot, openSlot, batchCreateSlots } from "../../../core/services/scheduleService";
import * as courseService from "../../../core/services/courseService";
import { useToast } from "../../../core/components/Toast";
import EmptyState from "../../../core/components/EmptyState";
import { useScheduleSlots } from "../hooks/useScheduleSlots";
import PermissionGate from "../../../core/auth/PermissionGate";
import SlotForm from "../components/SlotForm";
import "../styles/scheduleSlots.css";

const START_HOUR = 7;
const END_HOUR = 22;

const EMPTY_BATCH_ENTRY = { dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "" };

function toMinSince7(timeStr) {
  const [h, m] = timeStr.split(":").map(Number);
  return (h - START_HOUR) * 60 + m;
}

function minToGridRow(totalMin) {
  return 2 + Math.floor(totalMin / 30);
}

function minToRowSpan(totalMin) {
  return Math.ceil(totalMin / 30);
}

function ScheduleSlotsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();

  const {
    slots, offerings, loading, error,
    fetchOfferings, loadSlots, createSlot, updateSlot, removeSlot, setError,
  } = useScheduleSlots();

  const [selectedOfferingId, setSelectedOfferingId] = useState("");
  const [courses, setCourses] = useState([]);
  const [showForm, setShowForm] = useState(false);
  const [editSlot, setEditSlot] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [lifecycleLoading, setLifecycleLoading] = useState(null);
  const [showBatchForm, setShowBatchForm] = useState(false);
  const [batchEntries, setBatchEntries] = useState([{ ...EMPTY_BATCH_ENTRY }]);
  const [batchSaving, setBatchSaving] = useState(false);

  const courseMap = useRef({});

  useEffect(() => {
    if (scopeNode?.id && selectedSemesterObj?.id) {
      fetchOfferings(scopeNode.id, selectedSemesterObj.id);
    }
  }, [scopeNode, selectedSemesterObj, fetchOfferings]);

  useEffect(() => {
    courseService.fetchActiveCourses().then((data) => {
      const list = Array.isArray(data) ? data : [];
      setCourses(list);
      courseMap.current = {};
      list.forEach((c) => { courseMap.current[c.id] = c; });
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (selectedOfferingId) {
      loadSlots(selectedOfferingId);
    } else {
      loadSlots(null);
    }
  }, [selectedOfferingId, loadSlots]);

  const selectedOffering = useMemo(
    () => offerings.find((o) => o.id === selectedOfferingId) || null,
    [offerings, selectedOfferingId]
  );

  const intervals = useMemo(() => {
    const result = [];
    for (let t = START_HOUR * 60; t < END_HOUR * 60; t += 30) {
      const h = Math.floor(t / 60);
      const m = t % 60;
      result.push(`${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`);
    }
    return result;
  }, []);

  const days = useMemo(() => [0, 1, 2, 3, 4, 5, 6], []);

  const openCreate = () => {
    setEditSlot(null);
    setFormError("");
    setShowForm(true);
  };

  const openEdit = (slot) => {
    setEditSlot(slot);
    setFormError("");
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditSlot(null);
    setFormError("");
  };

  const handleSave = async (formData) => {
    setSaving(true);
    setFormError("");
    try {
      if (editSlot) {
        const body = {};
        if (formData.dayOfWeek !== editSlot.dayOfWeek) body.dayOfWeek = formData.dayOfWeek;
        if (formData.startTime !== editSlot.startTime) body.startTime = formData.startTime;
        if (formData.endTime !== editSlot.endTime) body.endTime = formData.endTime;
        if (formData.kind !== editSlot.kind) body.kind = formData.kind;
        if (formData.location !== editSlot.location) body.location = formData.location;
        if (formData.notes !== editSlot.notes) body.notes = formData.notes;
        await updateSlot(editSlot.id, body);
        addToast("Slot updated", "success");
      } else {
        await createSlot({
          courseOfferingId: formData.courseOfferingId,
          dayOfWeek: formData.dayOfWeek,
          startTime: formData.startTime,
          endTime: formData.endTime,
          kind: formData.kind,
          location: formData.location,
          notes: formData.notes,
        });
        addToast("Slot created", "success");
      }
      closeForm();
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      setFormError(err.message || "Failed to save slot");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await removeSlot(deleteTarget.id);
      addToast("Slot deleted", "success");
      setDeleteTarget(null);
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      addToast(err.message || "Failed to delete slot", "error");
      setDeleteTarget(null);
    }
  };

  const handleClose = async (slot) => {
    setLifecycleLoading(slot.id);
    try {
      await closeSlot(slot.id);
      addToast("Slot closed", "success");
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      addToast(err.message || "Failed to close slot", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleOpen = async (slot) => {
    setLifecycleLoading(slot.id);
    try {
      await openSlot(slot.id);
      addToast("Slot reopened", "success");
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      addToast(err.message || "Failed to reopen slot", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const openBatchForm = () => {
    setBatchEntries([{ ...EMPTY_BATCH_ENTRY }]);
    setShowBatchForm(true);
  };

  const addBatchEntry = () => {
    setBatchEntries(prev => [...prev, { ...EMPTY_BATCH_ENTRY }]);
  };

  const removeBatchEntry = (idx) => {
    setBatchEntries(prev => prev.length > 1 ? prev.filter((_, i) => i !== idx) : prev);
  };

  const updateBatchEntry = (idx, field, value) => {
    setBatchEntries(prev => prev.map((e, i) => i === idx ? { ...e, [field]: value } : e));
  };

  const handleBatchSubmit = async () => {
    const valid = batchEntries.filter(e => e.startTime && e.endTime && e.endTime > e.startTime);
    if (valid.length === 0) {
      addToast("No valid entries to create", "warning");
      return;
    }
    setBatchSaving(true);
    try {
      const result = await batchCreateSlots(selectedOfferingId, valid.map(e => ({
        dayOfWeek: Number(e.dayOfWeek),
        startTime: e.startTime,
        endTime: e.endTime,
        kind: Number(e.kind),
        location: e.location.trim() || null,
      })));
      const count = result?.succeeded?.length || valid.length;
      addToast(`${count} slot(s) created`, "success");
      setShowBatchForm(false);
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      addToast(err.message || "Batch create failed", "error");
    } finally {
      setBatchSaving(false);
    }
  };

  const getKindClass = (kind) => {
    const labels = ["lecture", "lab", "tutorial", "seminar", "exam", "other"];
    return `sch-kind-${labels[kind] || "other"}`;
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
            <button
              className="sch-btn sch-btn-primary"
              onClick={openBatchForm}
              disabled={!selectedOfferingId}
            >
              <Layers size={16} /> Batch
            </button>
          </PermissionGate>
          <PermissionGate resource="schedule.schedule-slots" minLevel={2}>
            <button
              className="sch-btn sch-btn-primary"
              onClick={openCreate}
              disabled={!selectedOfferingId}
            >
              <Plus size={16} /> {t("add_slot")}
            </button>
          </PermissionGate>
        </div>
      </div>

      <div className="sch-toolbar">
        <select
          className="sch-offering-select"
          value={selectedOfferingId}
          onChange={(e) => setSelectedOfferingId(e.target.value)}
        >
          <option value="">{t("select_course_offering")}</option>
          {offerings.map((o) => {
            const course = courseMap.current[o.courseId];
            return (
              <option key={o.id} value={o.id}>
                {course?.code || "—"} — {course?.title || t("unknown")} ({o.sectionCode})
              </option>
            );
          })}
        </select>
        {selectedOfferingId && selectedOffering && (
          <div className="sch-offering-badge">
            <strong>{t("capacity")}:</strong> {selectedOffering.registeredCount}/{selectedOffering.capacity}
          </div>
        )}
        {selectedOfferingId && (
          <button className="sch-btn sch-btn-primary" style={{ padding: "6px 12px", fontSize: 12 }}
            onClick={() => loadSlots(selectedOfferingId)}>
            <RefreshCw size={12} />
          </button>
        )}
      </div>

      {error && (
        <div className="sch-alert sch-alert-error" style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <AlertTriangle size={16} /> {error}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }}
            onClick={() => { setError(null); if (selectedOfferingId) loadSlots(selectedOfferingId); }}>
            Dismiss
          </button>
        </div>
      )}

      {!selectedOfferingId ? (
        <EmptyState icon={Clock} title="Select an Offering"
          message={t("select_offering_first")} />
      ) : loading ? (
        <div className="sch-empty" style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 12 }}>
          <RefreshCw size={24} style={{ animation: "spin 1s linear infinite" }} />
          <p style={{ margin: 0, color: "#6b7280", fontSize: 13 }}>{t("loading_timetable")}</p>
        </div>
      ) : slots.length === 0 ? (
        <EmptyState icon={Clock} title="No Slots Yet"
          message={t("no_slots_yet")}
          actionLabel={t("add_slot")} onAction={openCreate} />
      ) : (
        <div className="sch-timetable-wrap">
          <div
            className="sch-timetable"
            style={{
              gridTemplateColumns: `60px repeat(7, 1fr)`,
              gridTemplateRows: `36px repeat(${intervals.length}, 28px)`,
            }}
          >
            <div className="sch-timetable-corner" style={{ gridRow: 1, gridColumn: 1 }}>
              {t("time")}
            </div>
            {days.map((d, idx) => (
              <div
                key={d}
                className="sch-timetable-day-header"
                style={{ gridRow: 1, gridColumn: idx + 2 }}
              >
                {DAY_LABELS[d]}
              </div>
            ))}

            {intervals.map((time, rowIdx) => (
              <div key={`row-${time}`} style={{ display: "contents" }}>
                <div
                  className="sch-time-label"
                  style={{ gridRow: rowIdx + 2, gridColumn: 1 }}
                >
                  {time}
                </div>
                {days.map((d, colIdx) => (
                  <div
                    key={`cell-${d}-${time}`}
                    className="sch-time-cell"
                    style={{ gridRow: rowIdx + 2, gridColumn: colIdx + 2 }}
                  />
                ))}
              </div>
            ))}

            {slots.map((slot) => {
              const startMin = toMinSince7(slot.startTime?.slice(0, 5));
              const endMin = toMinSince7(slot.endTime?.slice(0, 5));
              const startRow = minToGridRow(startMin);
              const span = Math.max(1, minToRowSpan(endMin - startMin));
              const kindLabel = SLOT_KIND_LABELS[slot.kind] || "Other";
              const closed = slot.isClosed;

              return (
                <div
                  key={slot.id}
                  className={`sch-slot-block ${getKindClass(slot.kind)}`}
                  style={{
                    gridRow: `${startRow} / span ${span}`,
                    gridColumn: slot.dayOfWeek + 2,
                    opacity: closed ? 0.55 : 1,
                    filter: closed ? "grayscale(0.6)" : "none",
                    cursor: closed ? "default" : "pointer",
                  }}
                  onClick={() => !closed && openEdit(slot)}
                >
                  <div className="sch-slot-time">
                    {slot.startTime?.slice(0, 5)}–{slot.endTime?.slice(0, 5)}
                    {closed && <span style={{ marginLeft: 3, fontSize: 7, fontWeight: 700, textTransform: "uppercase", color: "#6b7280" }}>CLOSED</span>}
                  </div>
                  <div className="sch-slot-location">
                    {kindLabel}{slot.location ? ` · ${slot.location}` : ""}
                  </div>
                  <div className="sch-slot-actions">
                    {closed ? (
                      <PermissionGate resource="schedule.schedule-slots" minLevel={4}>
                        <button className="sch-slot-action-btn" title="Reopen"
                          onClick={(e) => { e.stopPropagation(); handleOpen(slot); }}
                          disabled={lifecycleLoading === slot.id}
                          style={{ color: "#2563eb" }}>
                          <Unlock size={9} />
                        </button>
                      </PermissionGate>
                    ) : (
                      <PermissionGate resource="schedule.schedule-slots" minLevel={3}>
                        <button className="sch-slot-action-btn" title="Close"
                          onClick={(e) => { e.stopPropagation(); handleClose(slot); }}
                          disabled={lifecycleLoading === slot.id}
                          style={{ color: "#d97706" }}>
                          <Lock size={9} />
                        </button>
                      </PermissionGate>
                    )}
                    <PermissionGate resource="schedule.schedule-slots" minLevel={3}>
                      <button className="sch-slot-action-btn" title={t("edit")}
                        onClick={(e) => { e.stopPropagation(); openEdit(slot); }}
                        disabled={closed}>
                        <Edit2 size={9} />
                      </button>
                    </PermissionGate>
                    <PermissionGate resource="schedule.schedule-slots" minLevel={5}>
                      <button className="sch-slot-action-btn danger" title={t("delete")}
                        onClick={(e) => { e.stopPropagation(); setDeleteTarget(slot); }}
                        disabled={closed}>
                        <Trash2 size={9} />
                      </button>
                    </PermissionGate>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {showForm && (
        <SlotForm
          editSlot={editSlot}
          courseOfferingId={selectedOfferingId}
          saving={saving}
          formError={formError}
          onSave={handleSave}
          onClose={closeForm}
        />
      )}

      {deleteTarget && (
        <div className="sch-confirm-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="sch-confirm-box" onClick={(e) => e.stopPropagation()}>
            <h3>{t("delete_slot")}</h3>
            <p>{t("delete_slot_confirm", { day: DAY_LABELS[deleteTarget.dayOfWeek], start: deleteTarget.startTime?.slice(0, 5), end: deleteTarget.endTime?.slice(0, 5) })}</p>
            <div className="sch-confirm-actions">
              <button className="btn-cancel" onClick={() => setDeleteTarget(null)}>{t("cancel")}</button>
              <button className="btn-primary" style={{ background: "#dc2626", boxShadow: "none" }} onClick={handleDelete}>
                {t("delete")}
              </button>
            </div>
          </div>
        </div>
      )}

      {showBatchForm && (
        <div className="sch-confirm-overlay" onClick={() => !batchSaving && setShowBatchForm(false)}>
          <div className="sch-confirm-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 520, maxHeight: "80vh", overflowY: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <h3 style={{ margin: 0 }}>Batch Create Slots</h3>
              <button className="btn-cancel" style={{ padding: "4px 8px" }} onClick={() => setShowBatchForm(false)}>
                <X size={16} />
              </button>
            </div>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 16px" }}>
              Add multiple schedule slots for the selected offering.
            </p>
            {batchEntries.map((entry, idx) => (
              <div key={idx} style={{ display: "flex", gap: 8, alignItems: "flex-end", marginBottom: 10, padding: 10, background: "#f9fafb", borderRadius: 8, border: "1px solid #e5e7eb" }}>
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
                <button className="sch-slot-action-btn danger" onClick={() => removeBatchEntry(idx)}
                  style={{ width: 28, height: 28, flexShrink: 0, marginBottom: 0 }}>
                  <Trash2 size={12} />
                </button>
              </div>
            ))}
            <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
              <button className="btn-cancel" style={{ fontSize: 12 }} onClick={addBatchEntry}>
                <Plus size={12} /> Add Row
              </button>
            </div>
            <div className="sch-confirm-actions" style={{ marginTop: 16 }}>
              <button className="btn-cancel" onClick={() => setShowBatchForm(false)} disabled={batchSaving}>Cancel</button>
              <button className="btn-primary" onClick={handleBatchSubmit} disabled={batchSaving}>
                <Save size={14} /> {batchSaving ? "Creating..." : `Create ${batchEntries.length} Slot(s)`}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ScheduleSlotsPage;
