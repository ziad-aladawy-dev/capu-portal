import { useTranslation } from "react-i18next";
import { CheckCircle2, AlertCircle } from "lucide-react";
import styles from "./ProfileCompleteness.module.css";

/**
 * "Your profile is N% complete" bar. Clicking a missing item scrolls to (and
 * pulses open) the section that owns it.
 */
function ProfileCompleteness({ percentage, missingFields, onJump }) {
  const { t } = useTranslation();
  const complete = percentage >= 100;

  return (
    <div className={`${styles.bar} ${complete ? styles.complete : ""}`}>
      <div className={styles.head}>
        {complete ? <CheckCircle2 size={16} /> : <AlertCircle size={16} />}
        <span className={styles.text}>
          {complete
            ? t("portal_profile.complete", { defaultValue: "Your profile is complete" })
            : t("portal_profile.percent_complete", { defaultValue: "Your profile is {{percent}}% complete", percent: percentage })}
        </span>
      </div>
      <div className={styles.track}>
        <div className={styles.fill} style={{ width: `${percentage}%` }} />
      </div>
      {!complete && missingFields.length > 0 && (
        <div className={styles.missing}>
          {missingFields.map((f) => (
            <button key={f.key} type="button" className={styles.chip} onClick={() => onJump?.(f.section)}>
              {t(`portal_profile.field_${f.key}`, { defaultValue: f.label })}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default ProfileCompleteness;
