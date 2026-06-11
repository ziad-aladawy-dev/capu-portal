import { useState, useMemo } from "react";
import { ChevronLeft, ChevronRight, Calendar, Check } from "lucide-react";
import Drawer from "../../../core/components/Drawer";
import { autoGenerateSemesters } from "../../../core/utils/dateConstraints";

const STEPS = ["Basic Info", "Semesters", "Review"];

export default function AcademicYearWizard({
  open,
  onClose,
  onSave,
  saving,
}) {
  const [step, setStep] = useState(0);
  const [form, setForm] = useState({
    name: "",
    nameAr: "",
    startDate: "",
    endDate: "",
    isCurrent: false,
  });
  const [semesters, setSemesters] = useState([]);
  const [formError, setFormError] = useState("");

  const handleGenerate = () => {
    if (!form.startDate || !form.endDate) {
      setFormError("Set start and end dates first");
      return;
    }
    setFormError("");
    const auto = autoGenerateSemesters(form.startDate, form.endDate);
    setSemesters(auto);
  };

  const updateSemester = (idx, field, value) => {
    setSemesters((prev) => prev.map((s, i) => (i === idx ? { ...s, [field]: value } : s)));
  };

  const addSemester = () => {
    setSemesters((prev) => [...prev, { name: "", nameAr: "", startDate: "", endDate: "" }]);
  };

  const removeSemester = (idx) => {
    setSemesters((prev) => prev.filter((_, i) => i !== idx));
  };

  const canProceed = useMemo(() => {
    if (step === 0) return form.name.trim() && form.startDate && form.endDate;
    if (step === 1) return semesters.length > 0 && semesters.every((s) => s.name && s.startDate && s.endDate);
    return true;
  }, [step, form, semesters]);

  const handleNext = () => {
    if (step === 0) {
      if (!form.name.trim()) { setFormError("Academic year name is required"); return; }
      if (!form.startDate) { setFormError("Start date is required"); return; }
      if (!form.endDate) { setFormError("End date is required"); return; }
      if (new Date(form.startDate) >= new Date(form.endDate)) {
        setFormError("End date must be after start date"); return;
      }
      setFormError("");
      if (semesters.length === 0) handleGenerate();
    }
    if (step === 1) {
      const invalid = semesters.find((s) => !s.name || !s.startDate || !s.endDate);
      if (invalid) { setFormError("All semesters must have a name, start date, and end date"); return; }
      const outOfBounds = semesters.find(
        (s) => s.startDate < form.startDate || s.endDate > form.endDate
      );
      if (outOfBounds) { setFormError("Semester dates must fall within the academic year bounds"); return; }
      setFormError("");
    }
    setStep((s) => Math.min(s + 1, STEPS.length - 1));
  };

  const handleBack = () => {
    setFormError("");
    setStep((s) => Math.max(s - 1, 0));
  };

  const handleSave = () => {
    const payload = {
      ...form,
      semesters: semesters.map((s, i) => ({ ...s, sortOrder: i })),
    };
    onSave(payload);
  };

  const handleClose = () => {
    setStep(0);
    setForm({ name: "", nameAr: "", startDate: "", endDate: "", isCurrent: false });
    setSemesters([]);
    setFormError("");
    onClose();
  };

  const renderStep = () => {
    switch (step) {
      case 0:
        return (
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <p style={{ fontSize: 13, color: "#6b7280", margin: 0 }}>
              Define the academic year boundaries.
            </p>
            <div className="form-group">
              <label htmlFor="wiz-name">Academic Year Name *</label>
              <input
                id="wiz-name"
                type="text"
                value={form.name}
                onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))}
                placeholder="e.g. 2024-2025"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
                autoFocus
              />
            </div>
            <div className="form-group">
              <label htmlFor="wiz-name-ar">Academic Year Name (Arabic)</label>
              <input
                id="wiz-name-ar"
                type="text"
                dir="rtl"
                value={form.nameAr}
                onChange={(e) => setForm((p) => ({ ...p, nameAr: e.target.value }))}
                placeholder="مثال: ٢٠٢٤-٢٠٢٥"
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
              />
            </div>
            <div style={{ display: "flex", gap: 12 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="wiz-start">Start Date *</label>
                <input
                  id="wiz-start"
                  type="date"
                  value={form.startDate}
                  onChange={(e) => setForm((p) => ({ ...p, startDate: e.target.value }))}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
                />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label htmlFor="wiz-end">End Date *</label>
                <input
                  id="wiz-end"
                  type="date"
                  value={form.endDate}
                  onChange={(e) => setForm((p) => ({ ...p, endDate: e.target.value }))}
                  min={form.startDate || undefined}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
                />
              </div>
            </div>
            <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
              <input
                type="checkbox"
                checked={form.isCurrent}
                onChange={(e) => setForm((p) => ({ ...p, isCurrent: e.target.checked }))}
                style={{ accentColor: "#1a1f5e" }}
              />
              Mark as current academic year
            </label>
          </div>
        );

      case 1:
        return (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <p style={{ fontSize: 13, color: "#6b7280", margin: 0 }}>
                Configure semesters. Dates are constrained to the year bounds.
              </p>
              <button
                style={{
                  padding: "4px 10px", fontSize: 11, background: "#eef2ff",
                  color: "#4338ca", border: "1px solid #c7d2fe", borderRadius: 6,
                  cursor: "pointer", fontWeight: 600,
                }}
                onClick={handleGenerate}
              >
                Auto-generate
              </button>
            </div>

            {semesters.map((sem, idx) => (
              <div
                key={idx}
                style={{
                  padding: 12,
                  background: "#f9fafb",
                  borderRadius: 8,
                  border: "1px solid #e5e7eb",
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                  <span style={{ fontSize: 12, fontWeight: 600, color: "#1a1f5e" }}>
                    Semester {idx + 1}
                  </span>
                  {semesters.length > 1 && (
                    <button
                      className="sch-slot-action-btn danger"
                      onClick={() => removeSemester(idx)}
                      style={{ width: 24, height: 24 }}
                    >
                      ✕
                    </button>
                  )}
                </div>
                <div style={{ display: "flex", gap: 8, marginBottom: 8 }}>
                  <div style={{ flex: 1 }}>
                    <input
                      type="text"
                      placeholder="Name (e.g. Fall)"
                      value={sem.name}
                      onChange={(e) => updateSemester(idx, "name", e.target.value)}
                      style={{ width: "100%", padding: "6px 10px", borderRadius: 6, border: "1px solid #d1d5db", fontSize: 12 }}
                    />
                  </div>
                  <div style={{ flex: 1 }}>
                    <input
                      type="text"
                      dir="rtl"
                      placeholder="الاسم (عربي)"
                      value={sem.nameAr || ""}
                      onChange={(e) => updateSemester(idx, "nameAr", e.target.value)}
                      style={{ width: "100%", padding: "6px 10px", borderRadius: 6, border: "1px solid #d1d5db", fontSize: 12 }}
                    />
                  </div>
                </div>
                <div style={{ display: "flex", gap: 8 }}>
                  <div style={{ flex: 1 }}>
                    <label style={{ fontSize: 10, color: "#6b7280", display: "block", marginBottom: 2 }}>Start</label>
                    <input
                      type="date"
                      value={sem.startDate}
                      min={form.startDate}
                      max={sem.endDate || form.endDate}
                      onChange={(e) => updateSemester(idx, "startDate", e.target.value)}
                      style={{ width: "100%", padding: "6px 10px", borderRadius: 6, border: "1px solid #d1d5db", fontSize: 12 }}
                    />
                  </div>
                  <div style={{ flex: 1 }}>
                    <label style={{ fontSize: 10, color: "#6b7280", display: "block", marginBottom: 2 }}>End</label>
                    <input
                      type="date"
                      value={sem.endDate}
                      min={sem.startDate || form.startDate}
                      max={form.endDate}
                      onChange={(e) => updateSemester(idx, "endDate", e.target.value)}
                      style={{ width: "100%", padding: "6px 10px", borderRadius: 6, border: "1px solid #d1d5db", fontSize: 12 }}
                    />
                  </div>
                </div>
              </div>
            ))}

            <button
              style={{
                padding: "6px 12px", fontSize: 12, background: "transparent",
                color: "#4338ca", border: "1px dashed #c7d2fe", borderRadius: 6,
                cursor: "pointer",
              }}
              onClick={addSemester}
            >
              + Add Semester
            </button>
          </div>
        );

      case 2:
        return (
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <p style={{ fontSize: 13, color: "#6b7280", margin: 0 }}>
              Review the academic year and its semesters before creating.
            </p>

            <div style={{ padding: 12, background: "#f8f9fc", borderRadius: 8, border: "1px solid #e5e7eb" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                <Calendar size={14} style={{ color: "#4338ca" }} />
                <strong style={{ fontSize: 14, color: "#1a1f5e" }}>
                  {form.name}{form.nameAr ? ` — ${form.nameAr}` : ""}
                </strong>
              </div>
              <div style={{ fontSize: 12, color: "#6b7280", marginLeft: 22 }}>
                {form.startDate} → {form.endDate}
                {form.isCurrent && <span style={{ marginLeft: 8, color: "#166534", fontWeight: 600 }}>● Current</span>}
              </div>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <span style={{ fontSize: 12, fontWeight: 600, color: "#374151" }}>
                Semesters ({semesters.length})
              </span>
              {semesters.map((sem, idx) => (
                <div
                  key={idx}
                  style={{
                    display: "flex", alignItems: "center", gap: 8,
                    padding: "8px 12px", background: "#f9fafb",
                    borderRadius: 6, border: "1px solid #e5e7eb", fontSize: 12,
                  }}
                >
                  <Check size={12} style={{ color: "#16a34a", flexShrink: 0 }} />
                  <strong>{sem.name}{sem.nameAr ? ` — ${sem.nameAr}` : ""}</strong>
                  <span style={{ color: "#6b7280" }}>
                    {sem.startDate} → {sem.endDate}
                  </span>
                </div>
              ))}
            </div>
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <Drawer
      open={open}
      onClose={handleClose}
      title={`New Academic Year — Step ${step + 1} of ${STEPS.length}`}
      width={520}
      loading={saving}
      footer={
        <div style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <div>
            {step > 0 && (
              <button className="btn-cancel" onClick={handleBack} disabled={saving}>
                <ChevronLeft size={14} /> Back
              </button>
            )}
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn-cancel" onClick={handleClose} disabled={saving}>
              Cancel
            </button>
            {step < STEPS.length - 1 ? (
              <button
                className="btn-primary"
                onClick={handleNext}
                disabled={!canProceed || saving}
              >
                Next <ChevronRight size={14} />
              </button>
            ) : (
              <button
                className="btn-primary"
                onClick={handleSave}
                disabled={saving}
              >
                {saving ? "Saving..." : "Create Year & Semesters"}
              </button>
            )}
          </div>
        </div>
      }
    >
      {/* Step indicators */}
      <div
        style={{
          display: "flex",
          gap: 4,
          marginBottom: 20,
          justifyContent: "center",
        }}
      >
        {STEPS.map((label, idx) => (
          <div
            key={idx}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              padding: "4px 10px",
              borderRadius: 20,
              fontSize: 11,
              fontWeight: 600,
              background: idx === step ? "#1a1f5e" : idx < step ? "#dcfce7" : "#f3f4f6",
              color: idx === step ? "white" : idx < step ? "#166534" : "#9ca3af",
              transition: "all 0.2s",
            }}
          >
            {idx < step ? <Check size={10} /> : <span>{idx + 1}</span>}
            {label}
          </div>
        ))}
      </div>

      {formError && (
        <div
          role="alert"
          style={{
            padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca",
            borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16,
          }}
        >
          {formError}
        </div>
      )}

      {renderStep()}
    </Drawer>
  );
}
