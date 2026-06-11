import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ChevronDown, Pencil, Check, X, Loader2 } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";
import PortalCard from "../shared/PortalCard";
import styles from "./ProfileSection.module.css";

/**
 * Collapsible profile section with optional per-section inline editing.
 *
 * fields: [{ key, label, value, type?, editable?, required?, placeholder?,
 *            validate?(value) -> error string | null, render?(value) }]
 * onSave(values) -> promise. Read-only sections omit onSave.
 */
function ProfileSection({ id, icon: Icon, title, badge, fields, onSave, defaultOpen = true, highlight = false }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [draft, setDraft] = useState({});
  const [errors, setErrors] = useState({});

  const startEdit = () => {
    const initial = {};
    for (const f of fields) if (f.editable) initial[f.key] = f.value ?? "";
    setDraft(initial);
    setErrors({});
    setEditing(true);
    setOpen(true);
  };

  // Pulse open when the completeness bar links here — render-time state
  // adjustment (not an effect) per react.dev/you-might-not-need-an-effect.
  const [prevHighlight, setPrevHighlight] = useState(false);
  if (highlight !== prevHighlight) {
    setPrevHighlight(highlight);
    if (highlight) {
      if (onSave) startEdit();
      else setOpen(true);
    }
  }

  const cancelEdit = () => {
    setEditing(false);
    setErrors({});
  };

  const handleSave = async () => {
    const nextErrors = {};
    for (const f of fields) {
      if (!f.editable) continue;
      const value = (draft[f.key] ?? "").trim();
      if (f.required && !value) nextErrors[f.key] = t("portal_profile.required", { defaultValue: "Required" });
      else if (f.validate) {
        const err = f.validate(value);
        if (err) nextErrors[f.key] = err;
      }
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setSaving(true);
    try {
      await onSave(draft);
      setEditing(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <PortalCard padding="none" className={styles.section} data-section={id}>
      <button type="button" className={styles.header} onClick={() => setOpen((p) => !p)} aria-expanded={open}>
        {Icon && <span className={styles.icon}><Icon size={16} /></span>}
        <span className={styles.title}>{title}</span>
        {badge}
        <ChevronDown size={16} className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`} />
      </button>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2, ease: "easeOut" }}
            className={styles.bodyWrap}
          >
            <div className={styles.body}>
              <dl className={styles.grid}>
                {fields.map((f) => (
                  <div key={f.key} className={styles.field}>
                    <dt className={styles.label}>
                      {f.label}
                      {editing && f.editable && f.required && <span className={styles.req}>*</span>}
                    </dt>
                    <dd className={styles.value}>
                      {editing && f.editable ? (
                        <>
                          <input
                            type={f.type || "text"}
                            className={`${styles.input} ${errors[f.key] ? styles.inputError : ""}`}
                            value={draft[f.key] ?? ""}
                            placeholder={f.placeholder}
                            onChange={(e) => setDraft((p) => ({ ...p, [f.key]: e.target.value }))}
                          />
                          {errors[f.key] && <span className={styles.error}>{errors[f.key]}</span>}
                        </>
                      ) : f.render ? (
                        f.render(f.value)
                      ) : (
                        f.value || <span className={styles.empty}>—</span>
                      )}
                    </dd>
                  </div>
                ))}
              </dl>

              {onSave && (
                <div className={styles.actions}>
                  {editing ? (
                    <>
                      <button type="button" className={styles.saveBtn} onClick={handleSave} disabled={saving}>
                        {saving ? <Loader2 size={14} className={styles.spin} /> : <Check size={14} />}
                        {t("portal_profile.save", { defaultValue: "Save" })}
                      </button>
                      <button type="button" className={styles.cancelBtn} onClick={cancelEdit} disabled={saving}>
                        <X size={14} /> {t("portal_profile.cancel", { defaultValue: "Cancel" })}
                      </button>
                    </>
                  ) : (
                    <button type="button" className={styles.editBtn} onClick={startEdit}>
                      <Pencil size={13} /> {t("portal_profile.edit", { defaultValue: "Edit" })}
                    </button>
                  )}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </PortalCard>
  );
}

export default ProfileSection;
