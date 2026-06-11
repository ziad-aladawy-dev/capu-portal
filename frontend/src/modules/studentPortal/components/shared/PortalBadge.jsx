import styles from "./PortalBadge.module.css";

/**
 * Status badge with the portal colour mapping.
 * tone: primary | accent | success | warning | danger | info | neutral
 */
function PortalBadge({ tone = "neutral", icon: Icon, children }) {
  return (
    <span className={`${styles.badge} ${styles[tone] || styles.neutral}`}>
      {Icon && <Icon size={11} />}
      {children}
    </span>
  );
}

export default PortalBadge;
