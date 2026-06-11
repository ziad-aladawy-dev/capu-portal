/**
 * Standard admin page header: icon chip + title + subtitle + actions.
 * Replaces the per-module .ty-header / .ay-header / .perm-header /
 * .sync-header / .audit-header / ... variants.
 *
 * Props:
 *   icon     — Lucide icon component (not an element)
 *   kicker   — optional small uppercase label above the title
 *   title    — page title (string)
 *   subtitle — optional description line
 *   actions  — optional right-side node (buttons, toggles)
 *   leading  — optional node rendered before the icon (e.g. back button)
 */
function PageHeader({ icon: Icon, kicker, title, subtitle, actions, leading }) {
  return (
    <div className="page-header">
      <div className="page-header-left">
        {leading}
        {Icon && (
          <span className="page-header-icon" aria-hidden="true">
            <Icon size={20} />
          </span>
        )}
        <div>
          {kicker && <span className="page-header-kicker">{kicker}</span>}
          <h1 className="page-header-title">{title}</h1>
          {subtitle && <p className="page-header-sub">{subtitle}</p>}
        </div>
      </div>
      {actions && <div className="page-header-actions">{actions}</div>}
    </div>
  );
}

export default PageHeader;
