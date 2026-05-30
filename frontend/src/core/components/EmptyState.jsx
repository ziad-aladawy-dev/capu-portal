/**
 * Reusable empty state component used across all list pages.
 * 
 * Props:
 *   icon      — Lucide icon component
 *   title     — Heading (e.g. "No students yet")
 *   message   — Description text
 *   actionLabel — CTA button label (optional)
 *   onAction  — CTA button click handler (optional)
 */
function EmptyState({ icon: Icon, title, message, actionLabel, onAction }) {
  return (
    <div className="empty-state">
      {Icon && (
        <div className="empty-state-icon">
          <Icon size={44} />
        </div>
      )}
      <h3 className="empty-state-title">{title || "Nothing here yet"}</h3>
      {message && <p className="empty-state-message">{message}</p>}
      {actionLabel && onAction && (
        <button className="empty-state-action" onClick={onAction}>
          {actionLabel}
        </button>
      )}
    </div>
  );
}

export default EmptyState;
