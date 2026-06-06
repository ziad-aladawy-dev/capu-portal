import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { X, Save } from "lucide-react";
import { OFFERING_STATUSES, REGISTRATION_STATES } from "../../../core/services/courseOfferingService";
import * as structureService from "../../../core/services/structureService";

function OfferingForm({ editOffering, courses, faculties, semesterId, structureNodeId, saving, formError, onSave, onClose }) {
  const { t } = useTranslation();
  const [form, setForm] = useState({
    courseId: "",
    semesterId: semesterId || "",
    structureNodeId: structureNodeId || "",
    sectionCode: "",
    capacity: 30,
    status: OFFERING_STATUSES.Draft,
    registrationState: REGISTRATION_STATES.Closed,
  });
  const [facultyId, setFacultyId] = useState("");
  const [programs, setPrograms] = useState([]);

  useEffect(() => {
    if (editOffering) {
      setForm({
        courseId: editOffering.courseId || "",
        semesterId: editOffering.semesterId || semesterId || "",
        structureNodeId: editOffering.structureNodeId || structureNodeId || "",
        sectionCode: editOffering.sectionCode || "",
        capacity: editOffering.capacity ?? 30,
        status: editOffering.status ?? OFFERING_STATUSES.Draft,
        registrationState: editOffering.registrationState ?? REGISTRATION_STATES.Closed,
      });
    } else {
      setForm((prev) => ({
        ...prev,
        semesterId: semesterId || prev.semesterId,
        structureNodeId: structureNodeId || prev.structureNodeId,
      }));
    }
  }, [editOffering, semesterId, structureNodeId]);

  useEffect(() => {
    if (facultyId) {
      structureService.fetchChildNodesByType(facultyId, "Program")
        .then(setPrograms)
        .catch(() => setPrograms([]));
    } else {
      setPrograms([]);
    }
  }, [facultyId]);

  const handleChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!form.courseId || !form.sectionCode.trim()) return;
    onSave({
      ...form,
      sectionCode: form.sectionCode.trim(),
    });
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content co-form-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{editOffering ? t("edit_offering") : t("new_course_offering")}</h3>
          <button className="modal-close-btn" onClick={onClose}><X size={20} /></button>
        </div>
        <form onSubmit={handleSubmit} className="co-form">
          <div className="co-form-grid">
            <div className="co-form-group">
              <label>{t("course")} *</label>
              {editOffering ? (
                <div className="co-form-static">
                  {courses.find((c) => c.id === editOffering.courseId)?.code || "—"} — {courses.find((c) => c.id === editOffering.courseId)?.title || ""}
                </div>
              ) : (
                <select value={form.courseId} onChange={(e) => handleChange("courseId", e.target.value)} required>
                  <option value="">{t("select_course_placeholder")}</option>
                  {courses.map((c) => (
                    <option key={c.id} value={c.id}>{c.code} — {c.title}</option>
                  ))}
                </select>
              )}
            </div>

            <div className="co-form-group">
              <label>{t("section_code")} *</label>
              <input
                type="text"
                value={form.sectionCode}
                onChange={(e) => handleChange("sectionCode", e.target.value)}
                placeholder={t("section_code_placeholder")}
                required
              />
            </div>

            <div className="co-form-group">
              <label>{t("capacity")}</label>
              <input
                type="number"
                min={0}
                value={form.capacity}
                onChange={(e) => handleChange("capacity", parseInt(e.target.value) || 0)}
              />
            </div>

            <div className="co-form-group">
              <label>{t("status")}</label>
              <select value={form.status} onChange={(e) => handleChange("status", parseInt(e.target.value))}>
                <option value={OFFERING_STATUSES.Draft}>{t("draft")}</option>
                <option value={OFFERING_STATUSES.Open}>{t("open")}</option>
                <option value={OFFERING_STATUSES.Closed}>{t("closed")}</option>
                <option value={OFFERING_STATUSES.Cancelled}>{t("cancelled")}</option>
              </select>
            </div>

            <div className="co-form-group">
              <label>{t("registration")}</label>
              <select value={form.registrationState} onChange={(e) => handleChange("registrationState", parseInt(e.target.value))}>
                <option value={REGISTRATION_STATES.Closed}>{t("closed")}</option>
                <option value={REGISTRATION_STATES.Open}>{t("open")}</option>
                <option value={REGISTRATION_STATES.Waitlist}>{t("waitlist")}</option>
              </select>
            </div>

            {!editOffering && (
              <>
                <div className="co-form-group">
                  <label>{t("faculty")}</label>
                  <select value={facultyId} onChange={(e) => setFacultyId(e.target.value)}>
                    <option value="">{t("select_faculty_placeholder")}</option>
                    {faculties.map((f) => (
                      <option key={f.id} value={f.id}>{f.name}</option>
                    ))}
                  </select>
                </div>

                <div className="co-form-group">
                  <label>{t("program_structure_node")}</label>
                  <select value={form.structureNodeId} onChange={(e) => handleChange("structureNodeId", e.target.value)} required>
                    <option value="">{t("select_program_placeholder")}</option>
                    {programs.map((p) => (
                      <option key={p.id} value={p.id}>{p.name}</option>
                    ))}
                  </select>
                </div>
              </>
            )}
          </div>

          {formError && <div className="co-form-error">{formError}</div>}

          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose} disabled={saving}>{t("cancel")}</button>
            <button type="submit" className="btn-primary" disabled={saving || !form.courseId || !form.sectionCode.trim()}>
              <Save size={14} /> {saving ? t("saving") : editOffering ? t("update") : t("create")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default OfferingForm;
