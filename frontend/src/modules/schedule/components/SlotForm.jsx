import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { X, Save } from "lucide-react";
import { SLOT_KINDS, SLOT_KIND_LABELS, DAY_LABELS } from "../../../core/services/scheduleService";

function SlotForm({ editSlot, courseOfferingId, saving, formError, onSave, onClose }) {
  const { t } = useTranslation();
  const [form, setForm] = useState({
    courseOfferingId: courseOfferingId || "",
    dayOfWeek: 0,
    startTime: "08:00",
    endTime: "09:00",
    kind: SLOT_KINDS.Lecture,
    location: "",
    notes: "",
  });

  useEffect(() => {
    if (editSlot) {
      setForm({
        courseOfferingId: editSlot.courseOfferingId || courseOfferingId || "",
        dayOfWeek: editSlot.dayOfWeek ?? 0,
        startTime: editSlot.startTime?.slice(0, 5) || "08:00",
        endTime: editSlot.endTime?.slice(0, 5) || "09:00",
        kind: editSlot.kind ?? SLOT_KINDS.Lecture,
        location: editSlot.location || "",
        notes: editSlot.notes || "",
      });
    } else {
      setForm((prev) => ({
        ...prev,
        courseOfferingId: courseOfferingId || prev.courseOfferingId,
      }));
    }
  }, [editSlot, courseOfferingId]);

  const handleChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!form.startTime || !form.endTime) return;
    if (form.endTime <= form.startTime) return;
    onSave({
      courseOfferingId: form.courseOfferingId,
      dayOfWeek: Number(form.dayOfWeek),
      startTime: form.startTime,
      endTime: form.endTime,
      kind: Number(form.kind),
      location: form.location.trim() || null,
      notes: form.notes.trim() || null,
    });
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content sch-form-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{editSlot ? t("edit_slot") : t("new_schedule_slot")}</h3>
          <button className="modal-close-btn" onClick={onClose}><X size={20} /></button>
        </div>
        <form onSubmit={handleSubmit} className="sch-form">
          <div className="sch-form-grid">
            <div className="sch-form-group">
              <label>{t("day")} *</label>
              <select value={form.dayOfWeek} onChange={(e) => handleChange("dayOfWeek", e.target.value)}>
                {Object.entries(DAY_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>

            <div className="sch-form-group">
              <label>{t("kind")}</label>
              <select value={form.kind} onChange={(e) => handleChange("kind", e.target.value)}>
                {Object.entries(SLOT_KIND_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>

            <div className="sch-form-group">
              <label>{t("start_time")} *</label>
              <input
                type="time"
                value={form.startTime}
                onChange={(e) => handleChange("startTime", e.target.value)}
                required
              />
            </div>

            <div className="sch-form-group">
              <label>{t("end_time")} *</label>
              <input
                type="time"
                value={form.endTime}
                onChange={(e) => handleChange("endTime", e.target.value)}
                required
              />
            </div>

            <div className="sch-form-group">
              <label>{t("location")}</label>
              <input
                type="text"
                value={form.location}
                onChange={(e) => handleChange("location", e.target.value)}
                placeholder={t("location_placeholder")}
              />
            </div>

            <div className="sch-form-group full-width">
              <label>{t("notes")}</label>
              <textarea
                value={form.notes}
                onChange={(e) => handleChange("notes", e.target.value)}
                placeholder={t("notes_placeholder")}
              />
            </div>
          </div>

          {formError && <div className="sch-form-error">{formError}</div>}

          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose} disabled={saving}>{t("cancel")}</button>
            <button
              type="submit"
              className="btn-primary"
              disabled={saving || !form.startTime || !form.endTime || form.endTime <= form.startTime}
            >
              <Save size={14} /> {saving ? t("saving") : editSlot ? t("update") : t("create")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default SlotForm;
