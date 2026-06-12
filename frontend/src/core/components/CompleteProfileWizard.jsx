import { useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { motion } from "framer-motion";
import {
  Camera, Phone, MapPin, ShieldAlert, ArrowRight, ArrowLeft, Check, PartyPopper, Loader2,
} from "lucide-react";
import { useAuth } from "../auth/useAuth";
import { getLocalized } from "../utils/getLocalized";
import * as studentService from "../services/studentService";
import {
  upsertProfileRecord,
  STUDENT_PROFILE_CATEGORY,
} from "../services/studentProfileService";
import { BLOCKER_QUERY_KEY, CONTACT_RECORD_KEY } from "../hooks/useBlockerState";
import { PhotoValidationOverlay } from "./PhotoValidationOverlay";
import { usePhotoValidator } from "../hooks/usePhotoValidator";
import styles from "./CompleteProfileWizard.module.css";

const STEPS = ["welcome", "contact", "emergency", "done"];
const PHONE_RE = /^01[0-9]{9}$/;

const CONFETTI_COLORS = ["#2563eb", "#8b5cf6", "#16a34a", "#d97706", "#dc2626"];

function ConfettiBurst() {
  const pieces = useMemo(
    () =>
      Array.from({ length: 24 }, (_, i) => ({
        x: (i % 12) * 30 - 165 + ((i * 7) % 13),
        delay: (i % 6) * 0.06,
        color: CONFETTI_COLORS[i % CONFETTI_COLORS.length],
        rotate: (i * 137) % 360,
      })),
    []
  );
  return (
    <div className={styles.confetti} aria-hidden="true">
      {pieces.map((p, i) => (
        <motion.span
          key={i}
          className={styles.confettiPiece}
          style={{ background: p.color }}
          initial={{ x: 0, y: 0, opacity: 1, rotate: 0 }}
          animate={{ x: p.x, y: 220, opacity: 0, rotate: p.rotate }}
          transition={{ duration: 1.4, delay: p.delay, ease: "easeOut" }}
        />
      ))}
    </div>
  );
}

/**
 * First-run onboarding for students whose required profile data is incomplete.
 * Four friendly steps (welcome+photo → contact → emergency → done) instead of
 * the old two-step interrupt. Each data step persists on "Continue" so closing
 * the browser mid-way loses at most one step.
 */
function CompleteProfileWizard({ student, emergencyData, contactData }) {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const validator = usePhotoValidator();
  const fileInputRef = useRef(null);

  const [step, setStep] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [photoUploaded, setPhotoUploaded] = useState(Boolean(student?.photoUrl));
  const [pendingPhoto, setPendingPhoto] = useState(null);
  const [photoPreview, setPhotoPreview] = useState(null);
  const [showValidation, setShowValidation] = useState(false);

  const [form, setForm] = useState({
    phoneNumber: student?.phoneNumber || "",
    address: contactData?.address || "",
    city: contactData?.city || "",
    country: contactData?.country || "",
    emergencyName: emergencyData?.name || "",
    relationship: emergencyData?.relationship || "",
    emergencyPhone: emergencyData?.phone || "",
  });

  const set = (name) => (e) => setForm((p) => ({ ...p, [name]: e.target.value }));

  const pickPhoto = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setPendingPhoto(file);
    setPhotoPreview(URL.createObjectURL(file));
    setShowValidation(true);
    validator.validate(file);
    e.target.value = "";
  };

  const acceptPhoto = async () => {
    setShowValidation(false);
    try {
      await studentService.uploadStudentPhoto(user.id, pendingPhoto);
      setPhotoUploaded(true);
    } catch {
      setError(t("onboarding.photo_failed", { defaultValue: "Photo upload failed — you can retry from your profile later." }));
    } finally {
      setPendingPhoto(null);
      validator.reset();
    }
  };

  const saveContact = async () => {
    if (!form.phoneNumber.trim() || !PHONE_RE.test(form.phoneNumber.trim())) {
      setError(t("onboarding.phone_invalid", { defaultValue: "Enter a valid Egyptian mobile number (01XXXXXXXXX)." }));
      return;
    }
    if (!form.address.trim()) {
      setError(t("onboarding.address_required", { defaultValue: "Permanent address is required." }));
      return;
    }
    setSaving(true);
    setError("");
    try {
      await studentService.updateStudent(user.id, { phoneNumber: form.phoneNumber.trim() });
      await upsertProfileRecord(user.id, {
        category: STUDENT_PROFILE_CATEGORY.Custom,
        customCategoryKey: CONTACT_RECORD_KEY,
        schemaVersion: 1,
        isSensitive: false,
        dataJson: JSON.stringify({
          address: form.address.trim(),
          city: form.city.trim(),
          country: form.country.trim(),
        }),
      });
      setStep(2);
    } catch (err) {
      setError(err.response?.data?.message || err.message || t("onboarding.save_failed", { defaultValue: "Could not save. Please try again." }));
    } finally {
      setSaving(false);
    }
  };

  const saveEmergency = async () => {
    if (!form.emergencyName.trim() || !form.emergencyPhone.trim()) {
      setError(t("onboarding.emergency_required", { defaultValue: "Emergency contact name and phone are required." }));
      return;
    }
    setSaving(true);
    setError("");
    try {
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
      setStep(3);
    } catch (err) {
      setError(err.response?.data?.message || err.message || t("onboarding.save_failed", { defaultValue: "Could not save. Please try again." }));
    } finally {
      setSaving(false);
    }
  };

  const finish = async () => {
    setSaving(true);
    await queryClient.invalidateQueries({ queryKey: BLOCKER_QUERY_KEY(user.id) });
  };

  const firstName = (getLocalized(student?.name, i18n.language) || "").split(" ")[0];

  return (
    <div className={styles.overlay}>
      <motion.div
        className={styles.card}
        initial={{ opacity: 0, y: 18 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3, ease: "easeOut" }}
      >
        <div className={styles.progress}>
          {STEPS.map((s, i) => (
            <div key={s} className={`${styles.dot} ${i <= step ? styles.dotActive : ""}`}>
              {i < step ? <Check size={11} /> : i + 1}
            </div>
          ))}
          <div className={styles.progressTrack}>
            <div className={styles.progressFill} style={{ width: `${(step / (STEPS.length - 1)) * 100}%` }} />
          </div>
        </div>

        {error && <div className={styles.error}>{error}</div>}

        {step === 0 && (
          <div className={styles.body}>
            <h1 className={styles.title}>
              {t("onboarding.welcome_title", { defaultValue: "Welcome{{name}}! Let's set you up", name: firstName ? `, ${firstName}` : "" })} 👋
            </h1>
            <p className={styles.subtitle}>
              {t("onboarding.welcome_text", { defaultValue: "Two quick steps and you're in. First — want to add a profile photo?" })}
            </p>

            <button type="button" className={styles.photoDrop} onClick={() => fileInputRef.current?.click()}>
              <Camera size={26} />
              <span>
                {photoUploaded
                  ? t("onboarding.photo_done", { defaultValue: "Photo added — looking good!" })
                  : t("onboarding.photo_cta", { defaultValue: "Upload a photo (optional)" })}
              </span>
            </button>
            <input ref={fileInputRef} type="file" accept="image/*" hidden onChange={pickPhoto} />

            <div className={styles.actions}>
              <button type="button" className={styles.primaryBtn} onClick={() => { setError(""); setStep(1); }}>
                {t("onboarding.lets_go", { defaultValue: "Let's go" })} <ArrowRight size={15} className={styles.arrow} />
              </button>
            </div>
          </div>
        )}

        {step === 1 && (
          <div className={styles.body}>
            <h2 className={styles.stepTitle}>
              <Phone size={17} /> {t("onboarding.contact_title", { defaultValue: "How can we reach you?" })}
            </h2>
            <label className={styles.field}>
              <span>{t("onboarding.phone", { defaultValue: "Phone Number" })} *</span>
              <input type="tel" value={form.phoneNumber} onChange={set("phoneNumber")} placeholder="01012345678" autoFocus />
            </label>
            <label className={styles.field}>
              <span><MapPin size={12} /> {t("onboarding.address", { defaultValue: "Permanent Address" })} *</span>
              <input type="text" value={form.address} onChange={set("address")} placeholder={t("onboarding.address_ph", { defaultValue: "Street, building…" })} />
            </label>
            <div className={styles.row}>
              <label className={styles.field}>
                <span>{t("onboarding.city", { defaultValue: "City" })}</span>
                <input type="text" value={form.city} onChange={set("city")} />
              </label>
              <label className={styles.field}>
                <span>{t("onboarding.country", { defaultValue: "Country" })}</span>
                <input type="text" value={form.country} onChange={set("country")} />
              </label>
            </div>
            <div className={styles.actions}>
              <button type="button" className={styles.ghostBtn} onClick={() => { setError(""); setStep(0); }} disabled={saving}>
                <ArrowLeft size={15} className={styles.arrow} /> {t("onboarding.back", { defaultValue: "Back" })}
              </button>
              <button type="button" className={styles.primaryBtn} onClick={saveContact} disabled={saving}>
                {saving ? <Loader2 size={15} className={styles.spin} /> : null}
                {t("onboarding.continue", { defaultValue: "Continue" })} <ArrowRight size={15} className={styles.arrow} />
              </button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className={styles.body}>
            <h2 className={styles.stepTitle}>
              <ShieldAlert size={17} /> {t("onboarding.emergency_title", { defaultValue: "Who should we call in an emergency?" })}
            </h2>
            <label className={styles.field}>
              <span>{t("onboarding.contact_name", { defaultValue: "Contact Name" })} *</span>
              <input type="text" value={form.emergencyName} onChange={set("emergencyName")} autoFocus />
            </label>
            <div className={styles.row}>
              <label className={styles.field}>
                <span>{t("onboarding.relationship", { defaultValue: "Relationship" })}</span>
                <input type="text" value={form.relationship} onChange={set("relationship")} placeholder={t("onboarding.relationship_ph", { defaultValue: "Parent, sibling…" })} />
              </label>
              <label className={styles.field}>
                <span>{t("onboarding.phone", { defaultValue: "Phone Number" })} *</span>
                <input type="tel" value={form.emergencyPhone} onChange={set("emergencyPhone")} placeholder="01012345678" />
              </label>
            </div>
            <div className={styles.actions}>
              <button type="button" className={styles.ghostBtn} onClick={() => { setError(""); setStep(1); }} disabled={saving}>
                <ArrowLeft size={15} className={styles.arrow} /> {t("onboarding.back", { defaultValue: "Back" })}
              </button>
              <button type="button" className={styles.primaryBtn} onClick={saveEmergency} disabled={saving}>
                {saving ? <Loader2 size={15} className={styles.spin} /> : null}
                {t("onboarding.continue", { defaultValue: "Continue" })} <ArrowRight size={15} className={styles.arrow} />
              </button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className={`${styles.body} ${styles.doneBody}`}>
            <ConfettiBurst />
            <span className={styles.doneIcon}><PartyPopper size={30} /></span>
            <h1 className={styles.title}>{t("onboarding.done_title", { defaultValue: "You're all set!" })} 🎉</h1>
            <p className={styles.subtitle}>
              {t("onboarding.done_text", { defaultValue: "Your profile is complete. Welcome to your student portal." })}
            </p>
            <div className={styles.actions}>
              <button type="button" className={styles.primaryBtn} onClick={finish} disabled={saving}>
                {saving ? <Loader2 size={15} className={styles.spin} /> : null}
                {t("onboarding.go_dashboard", { defaultValue: "Go to my dashboard" })} <ArrowRight size={15} className={styles.arrow} />
              </button>
            </div>
          </div>
        )}
      </motion.div>

      {showValidation && (
        <PhotoValidationOverlay
          results={validator.results}
          previewUrl={photoPreview}
          isProcessing={validator.isProcessing}
          error={validator.error}
          onAccept={acceptPhoto}
          onReject={() => { setShowValidation(false); setPendingPhoto(null); validator.reset(); }}
          onRetry={() => validator.validate(pendingPhoto)}
        />
      )}
    </div>
  );
}

export default CompleteProfileWizard;
