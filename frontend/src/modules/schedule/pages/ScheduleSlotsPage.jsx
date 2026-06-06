import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Clock, Plus, Edit2, Trash2, AlertTriangle, RefreshCw } from "lucide-react";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { DAY_LABELS, SLOT_KIND_LABELS } from "../../../core/services/scheduleService";
import * as courseService from "../../../core/services/courseService";
import { useScheduleSlots } from "../hooks/useScheduleSlots";
import SlotForm from "../components/SlotForm";
import "../styles/scheduleSlots.css";

const START_HOUR = 7;
const END_HOUR = 22;

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
      setDeleteTarget(null);
      if (selectedOfferingId) loadSlots(selectedOfferingId);
    } catch (err) {
      setError(err.message || "Failed to delete slot");
      setDeleteTarget(null);
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
        <button
          className="sch-btn sch-btn-primary"
          onClick={openCreate}
          disabled={!selectedOfferingId}
        >
          <><Plus size={16} /> {t("add_slot")}</>
        </button>
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
      </div>

      {error && (
        <div className="sch-alert sch-alert-error">
          <AlertTriangle size={16} /> {error}
        </div>
      )}

      {!selectedOfferingId ? (
        <div className="sch-empty">
          {t("select_offering_first")}
        </div>
      ) : loading ? (
        <div className="sch-empty">
          <RefreshCw size={20} style={{ animation: "spin 1s linear infinite", marginRight: 8, verticalAlign: "middle" }} />
          {t("loading_timetable")}
        </div>
      ) : slots.length === 0 ? (
        <div className="sch-empty">
          {t("no_slots_yet")}
        </div>
      ) : (
        <div className="sch-timetable-wrap">
          <div
            className="sch-timetable"
            style={{
              gridTemplateColumns: `60px repeat(7, 1fr)`,
              gridTemplateRows: `36px repeat(${intervals.length}, 28px)`,
            }}
          >
            {/* Header */}
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

            {/* Time labels + empty cells */}
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

            {/* Slot blocks */}
            {slots.map((slot) => {
              const startMin = toMinSince7(slot.startTime?.slice(0, 5));
              const endMin = toMinSince7(slot.endTime?.slice(0, 5));
              const startRow = minToGridRow(startMin);
              const span = Math.max(1, minToRowSpan(endMin - startMin));
              const kindLabel = SLOT_KIND_LABELS[slot.kind] || "Other";

              return (
                <div
                  key={slot.id}
                  className={`sch-slot-block ${getKindClass(slot.kind)}`}
                  style={{
                    gridRow: `${startRow} / span ${span}`,
                    gridColumn: slot.dayOfWeek + 2,
                  }}
                  onClick={() => openEdit(slot)}
                >
                  <div className="sch-slot-time">
                    {slot.startTime?.slice(0, 5)}–{slot.endTime?.slice(0, 5)}
                  </div>
                  <div className="sch-slot-location">
                    {kindLabel}{slot.location ? ` · ${slot.location}` : ""}
                  </div>
                  <div className="sch-slot-actions">
                    <button
                      className="sch-slot-action-btn"
                      title={t("edit")}
                      onClick={(e) => { e.stopPropagation(); openEdit(slot); }}
                    >
                      <Edit2 size={9} />
                    </button>
                    <button
                      className="sch-slot-action-btn danger"
                      title={t("delete")}
                      onClick={(e) => { e.stopPropagation(); setDeleteTarget(slot); }}
                    >
                      <Trash2 size={9} />
                    </button>
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
    </div>
  );
}

export default ScheduleSlotsPage;
