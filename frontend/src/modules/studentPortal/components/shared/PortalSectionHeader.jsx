import { Link } from "react-router-dom";
import { ArrowRight } from "lucide-react";
import styles from "./PortalSectionHeader.module.css";

/** Section heading with optional icon and "View all" link. */
function PortalSectionHeader({ icon: Icon, title, to, toLabel = "View all", children }) {
  return (
    <header className={styles.header}>
      <h3 className={styles.title}>
        {Icon && <Icon size={16} className={styles.icon} />}
        {title}
      </h3>
      <div className={styles.side}>
        {children}
        {to && (
          <Link to={to} className={styles.link}>
            {toLabel} <ArrowRight size={13} className={styles.arrow} />
          </Link>
        )}
      </div>
    </header>
  );
}

export default PortalSectionHeader;
