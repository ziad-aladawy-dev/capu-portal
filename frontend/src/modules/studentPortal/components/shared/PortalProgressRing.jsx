import styles from "./PortalProgressRing.module.css";

/**
 * Circular progress (GPA, completeness…). `value` 0..max maps to ring fill.
 * Children render in the center; default shows the value.
 */
function PortalProgressRing({ value = 0, max = 100, size = 84, stroke = 8, tone = "primary", label, children }) {
  const radius = (size - stroke) / 2;
  const circumference = 2 * Math.PI * radius;
  const ratio = max > 0 ? Math.min(1, Math.max(0, value / max)) : 0;
  const dash = circumference * ratio;

  return (
    <div className={styles.wrap} style={{ width: size, height: size }} role="img" aria-label={label}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle
          className={styles.track}
          cx={size / 2} cy={size / 2} r={radius}
          strokeWidth={stroke} fill="none"
        />
        <circle
          className={`${styles.fill} ${styles[tone] || ""}`}
          cx={size / 2} cy={size / 2} r={radius}
          strokeWidth={stroke} fill="none"
          strokeDasharray={`${dash} ${circumference - dash}`}
          strokeDashoffset={circumference / 4}
          strokeLinecap="round"
        />
      </svg>
      <div className={styles.center}>{children}</div>
    </div>
  );
}

export default PortalProgressRing;
