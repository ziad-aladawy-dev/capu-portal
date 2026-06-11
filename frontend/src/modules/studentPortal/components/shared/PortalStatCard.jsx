import styles from "./PortalStatCard.module.css";

/**
 * Metric display. variant "column" (default) stacks value over label;
 * "row" puts an icon chip beside the numbers.
 */
function PortalStatCard({ icon: Icon, value, label, hint, tone = "primary", variant = "column" }) {
  return (
    <div className={`${styles.card} ${variant === "row" ? styles.row : ""}`}>
      {Icon && (
        <span className={`${styles.iconChip} ${styles[tone] || ""}`}>
          <Icon size={18} />
        </span>
      )}
      <div className={styles.body}>
        <span className={styles.value}>{value}</span>
        <span className={styles.label}>{label}</span>
        {hint && <span className={styles.hint}>{hint}</span>}
      </div>
    </div>
  );
}

export default PortalStatCard;
