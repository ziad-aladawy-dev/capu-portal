import React from "react";
import { useTranslation } from "react-i18next";

function FacultyPageHeader({
  title,
  icon: Icon,
  onAdd,
  onExport,
  showActions = true,
  exportButtonRef,
}) {
  const { t } = useTranslation();

  return (
    <section className="users-page-header">
      <div className="users-page-title">
        {Icon && (
          <div className="users-page-icon">
            <Icon size={16} />
          </div>
        )}
        <div>
          <span className="users-page-kicker">{t("users_module")}</span>
          <h1>{title}</h1>
          <p>{t("manage_users_roles_permissions")}</p>
        </div>
      </div>
      {showActions && (
        <div className="users-page-actions">
          <button type="button" className="users-secondary-btn" onClick={onExport} ref={exportButtonRef}>
            {t("export")}
          </button>
          <button type="button" className="users-primary-btn" onClick={onAdd}>
            {t("add_user")}
          </button>
        </div>
      )}
    </section>
  );
}

export default FacultyPageHeader;