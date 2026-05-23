import React from "react";

function FacultyPageHeader({
  title,
  icon: Icon,
  onAdd,
  onExport,
  onImport,
  showActions = true,
  exportButtonRef,
}) {
  return (
    <section className="users-page-header">
      <div className="users-page-title">
        {Icon && (
          <div className="users-page-icon">
            <Icon size={16} />
          </div>
        )}
        <div>
          <span className="users-page-kicker">Users Module</span>
          <h1>{title}</h1>
          <p>Manage users, roles and permissions.</p>
        </div>
      </div>
      {showActions && (
        <div className="users-page-actions">
          <button type="button" className="users-secondary-btn" onClick={onImport}>
            Import
          </button>
          <button type="button" className="users-secondary-btn" onClick={onExport} ref={exportButtonRef}>
            Export
          </button>
          <button type="button" className="users-primary-btn" onClick={onAdd}>
            Add User
          </button>
        </div>
      )}
    </section>
  );
}

export default FacultyPageHeader;