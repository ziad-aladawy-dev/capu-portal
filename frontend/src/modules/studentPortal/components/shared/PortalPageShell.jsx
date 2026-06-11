import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ArrowLeft } from "lucide-react";
import styles from "./PortalPageShell.module.css";

/**
 * Consistent page chrome for portal pages: optional back button, title,
 * subtitle, and a trailing action slot. Content renders below.
 *
 *   <PortalPageShell title="My Grades" subtitle="Fall 2025" actions={<Button/>}>
 *     …page content…
 *   </PortalPageShell>
 */
function PortalPageShell({ title, subtitle, backTo, actions, children }) {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.headerMain}>
          {backTo && (
            <button
              type="button"
              className={styles.backBtn}
              onClick={() => (backTo === true ? navigate(-1) : navigate(backTo))}
              aria-label={t("back", { defaultValue: "Back" })}
            >
              <ArrowLeft size={18} className={styles.backIcon} />
            </button>
          )}
          <div>
            <h1 className={styles.title}>{title}</h1>
            {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
          </div>
        </div>
        {actions && <div className={styles.actions}>{actions}</div>}
      </header>
      <div className={styles.content}>{children}</div>
    </div>
  );
}

export default PortalPageShell;
