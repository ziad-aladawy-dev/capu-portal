import { Link } from "react-router-dom";
import styles from "./PortalEmptyState.module.css";

/**
 * Friendly empty state: icon, headline, optional explanation + action.
 * `action`: { to, label } renders a link-button; `onAction` + `actionLabel`
 * renders a plain button instead.
 */
function PortalEmptyState({ icon: Icon, title, text, action, onAction, actionLabel, compact = false }) {
  return (
    <div className={`${styles.wrap} ${compact ? styles.compact : ""}`}>
      {Icon && (
        <div className={styles.iconRing}>
          <Icon size={compact ? 18 : 24} />
        </div>
      )}
      {title && <strong className={styles.title}>{title}</strong>}
      {text && <p className={styles.text}>{text}</p>}
      {action?.to && (
        <Link to={action.to} className={styles.action}>
          {action.label}
        </Link>
      )}
      {onAction && (
        <button type="button" className={styles.action} onClick={onAction}>
          {actionLabel}
        </button>
      )}
    </div>
  );
}

export default PortalEmptyState;
