import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Phone, MapPin, ShieldAlert, ArrowRight, ArrowLeft, Check } from "lucide-react";
import { useAuth } from "../auth/useAuth";
import * as studentService from "../services/studentService";
import {
  upsertProfileRecord,
  STUDENT_PROFILE_CATEGORY,
} from "../services/studentProfileService";
import { BLOCKER_QUERY_KEY } from "../hooks/useBlockerState";
import "./studentBlocker.css";

// Fields PUT /students/{id} accepts (mirrors the working StudentProfile page),
// so the update never wipes data the wizard doesn't touch.
function buildStudentUpdate(student, patch) {
  return {
    firstName: student?.firstName || "",
    lastName: student?.lastName || "",
    email: student?.email || "",
    dateOfBirth: student?.dateOfBirth || null,
    phoneNumber: student?.phoneNumber || "",
    address: student?.address || "",
    city: student?.city || "",
    country: student?.country || "",
    ...patch,
  };
}

function CompleteProfileWizard({ student, emergencyData }) {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [step, setStep] = useState(1);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    phoneNumber: student?.phoneNumber || "",
    address: student?.address || "",
    city: student?.city || "",
    country: student?.country || "",
    emergencyName: emergencyData?.name || "",
    relationship: emergencyData?.relationship || "",
    emergencyPhone: emergencyData?.phone || "",
  });

  const set = (name) => (e) => setForm((p) => ({ ...p, [name]: e.target.value }));

  const goNext = () => {
    if (!form.phoneNumber.trim() || !form.address.trim()) {
      setError("Phone number and permanent address are required.");
      return;
    }
    setError("");
    setStep(2);
  };

  const handleFinish = async () => {
    if (!form.emergencyName.trim() || !form.emergencyPhone.trim()) {
      setError("Emergency contact name and phone are required.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      await studentService.updateStudent(
        user.id,
        buildStudentUpdate(student, {
          phoneNumber: form.phoneNumber.trim(),
          address: form.address.trim(),
          city: form.city.trim(),
          country: form.country.trim(),
        })
      );

      await upsertProfileRecord(user.id, {
        category: STUDENT_PROFILE_CATEGORY.EmergencyContact,
        schemaVersion: 1,
        isSensitive: false,
        dataJson: JSON.stringify({
          name: form.emergencyName.trim(),
          relationship: form.relationship.trim(),
          phone: form.emergencyPhone.trim(),
        }),
      });

      // Re-evaluate the gate; on success it will let the student through.
      await queryClient.invalidateQueries({ queryKey: BLOCKER_QUERY_KEY(user.id) });
    } catch (err) {
      setError(
        err.response?.data?.message || err.message || "Could not save your profile. Please try again."
      );
      setSaving(false);
    }
  };

  return (
    <div className="blocker-overlay">
      <div className="blocker-card">
        <div className="blocker-header">
          <h1>Complete your profile</h1>
          <p>We need a few details before you can continue to your dashboard.</p>
        </div>

        <div className="wizard-progress">
          <div className={`wizard-step ${step >= 1 ? "active" : ""}`}>
            <span>{step > 1 ? <Check size={14} /> : 1}</span> Contact
          </div>
          <div className="wizard-progress-line" />
          <div className={`wizard-step ${step >= 2 ? "active" : ""}`}>
            <span>2</span> Emergency
          </div>
        </div>

        {error && <div className="blocker-error">{error}</div>}

        {step === 1 ? (
          <div className="wizard-body">
            <div className="wizard-field">
              <label><Phone size={14} /> Phone Number *</label>
              <input type="tel" value={form.phoneNumber} onChange={set("phoneNumber")} placeholder="e.g. 01012345678" autoFocus />
            </div>
            <div className="wizard-field">
              <label><MapPin size={14} /> Permanent Address *</label>
              <input type="text" value={form.address} onChange={set("address")} placeholder="Street, building, etc." />
            </div>
            <div className="wizard-row">
              <div className="wizard-field">
                <label>City</label>
                <input type="text" value={form.city} onChange={set("city")} />
              </div>
              <div className="wizard-field">
                <label>Country</label>
                <input type="text" value={form.country} onChange={set("country")} />
              </div>
            </div>

            <div className="wizard-actions">
              <button type="button" className="wizard-btn primary" onClick={goNext}>
                Continue <ArrowRight size={16} />
              </button>
            </div>
          </div>
        ) : (
          <div className="wizard-body">
            <div className="wizard-note">
              <ShieldAlert size={16} /> Who should we contact in an emergency?
            </div>
            <div className="wizard-field">
              <label>Full Name *</label>
              <input type="text" value={form.emergencyName} onChange={set("emergencyName")} autoFocus />
            </div>
            <div className="wizard-row">
              <div className="wizard-field">
                <label>Relationship</label>
                <input type="text" value={form.relationship} onChange={set("relationship")} placeholder="e.g. Parent" />
              </div>
              <div className="wizard-field">
                <label>Phone *</label>
                <input type="tel" value={form.emergencyPhone} onChange={set("emergencyPhone")} />
              </div>
            </div>

            <div className="wizard-actions">
              <button type="button" className="wizard-btn ghost" onClick={() => { setError(""); setStep(1); }} disabled={saving}>
                <ArrowLeft size={16} /> Back
              </button>
              <button type="button" className="wizard-btn primary" onClick={handleFinish} disabled={saving}>
                {saving ? "Saving…" : "Finish & Continue"}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default CompleteProfileWizard;
