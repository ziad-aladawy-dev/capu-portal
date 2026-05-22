import { useState, useEffect, useCallback } from "react";
import { UserCog, Plus, Edit2, Trash2, X, AlertTriangle, RefreshCw, Shield } from "lucide-react";
import * as permissionService from "../../../core/services/permissionService";
import "../styles/roles.css";

const PAGE_SIZE = 10;

function RolesPage() {
  const [roles, setRoles] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [modalMode, setModalMode] = useState(null);
  const [editRole, setEditRole] = useState(null);
  const [formName, setFormName] = useState("");
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const fetchRoles = useCallback(async (p = 1) => {
    setLoading(true);
    setError(null);
    try {
      const data = await permissionService.fetchAllRoles({ page: p, pageSize: PAGE_SIZE });
      setRoles(data.items || []);
      setTotalCount(data.totalCount || 0);
      setPage(p);
    } catch (err) {
      setError(err.message || "Failed to load roles");
      setRoles([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRoles(1);
  }, [fetchRoles]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const openCreate = () => {
    setModalMode("create");
    setEditRole(null);
    setFormName("");
    setFormError("");
  };

  const openEdit = (role) => {
    setModalMode("edit");
    setEditRole(role);
    setFormName(role.name);
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditRole(null);
    setFormName("");
    setFormError("");
  };

  const validateForm = () => {
    const trimmed = formName.trim();
    if (!trimmed) {
      setFormError("Role name is required");
      return false;
    }
    if (trimmed.length < 2) {
      setFormError("Role name must be at least 2 characters");
      return false;
    }
    if (trimmed.length > 100) {
      setFormError("Role name must be 100 characters or fewer");
      return false;
    }
    setFormError("");
    return true;
  };

  const handleSave = async (e) => {
    e.preventDefault();
    if (!validateForm()) return;
    setSaving(true);
    try {
      if (modalMode === "create") {
        await permissionService.createRole({ name: formName.trim() });
      } else if (modalMode === "edit" && editRole) {
        await permissionService.updateRole(editRole.id, { name: formName.trim() });
      }
      closeModal();
      await fetchRoles(page);
    } catch (err) {
      if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
        setFormError("A role with this name already exists");
      } else {
        setFormError(err.message || "Failed to save role");
      }
    } finally {
      setSaving(false);
    }
  };

  const openDelete = (role) => {
    setDeleteTarget(role);
  };

  const closeDelete = () => {
    setDeleteTarget(null);
    setDeleting(false);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await permissionService.deleteRole(deleteTarget.id);
      closeDelete();
      const newPage = roles.length === 1 && page > 1 ? page - 1 : page;
      await fetchRoles(newPage);
    } catch (err) {
      setDeleting(false);
      setError(err.message || "Failed to delete role");
      closeDelete();
    }
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleDateString("en-EG", {
        year: "numeric",
        month: "short",
        day: "numeric",
      });
    } catch {
      return "—";
    }
  };

  const renderPagination = () => {
    if (totalPages <= 1) return null;
    const pages = [];
    for (let i = 1; i <= totalPages; i++) {
      pages.push(
        <button
          key={i}
          className={`roles-page-btn ${i === page ? "active" : ""}`}
          onClick={() => fetchRoles(i)}
          disabled={i === page}
        >
          {i}
        </button>
      );
    }
    return (
      <div className="roles-pagination">
        <span>
          Showing {roles.length} of {totalCount} role{totalCount !== 1 ? "s" : ""}
        </span>
        <div className="roles-pagination-controls">
          <button
            className="roles-page-btn"
            onClick={() => fetchRoles(page - 1)}
            disabled={page <= 1}
          >
            Prev
          </button>
          {pages}
          <button
            className="roles-page-btn"
            onClick={() => fetchRoles(page + 1)}
            disabled={page >= totalPages}
          >
            Next
          </button>
        </div>
      </div>
    );
  };

  if (loading) {
    return (
      <div className="roles-page">
        <div className="roles-loading">
          <div className="roles-spinner" />
          <p>Loading roles…</p>
        </div>
      </div>
    );
  }

  if (error && roles.length === 0) {
    return (
      <div className="roles-page">
        <div className="roles-header">
          <div className="roles-header-left">
            <UserCog size={20} />
            <div>
              <h1>Roles</h1>
              <p>Manage role definitions and system roles</p>
            </div>
          </div>
        </div>
        <div className="roles-error">
          <AlertTriangle size={36} className="roles-error-icon" />
          <h3>Failed to load roles</h3>
          <p>{error}</p>
          <button className="roles-btn roles-btn-outline" onClick={() => fetchRoles(1)}>
            <RefreshCw size={13} />
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="roles-page">
      <div className="roles-header">
        <div className="roles-header-left">
          <UserCog size={20} />
          <div>
            <h1>Roles</h1>
            <p>Manage role definitions and system roles</p>
          </div>
        </div>
        <div className="roles-header-actions">
          <button className="roles-btn roles-btn-primary" onClick={openCreate}>
            <Plus size={14} />
            Create Role
          </button>
        </div>
      </div>

      {error && (
        <div className="roles-error" style={{ padding: "12px 16px", marginBottom: 16, borderRadius: 8, background: "#fef2f2", flexDirection: "row" }}>
          <AlertTriangle size={16} style={{ flexShrink: 0 }} />
          <p style={{ margin: 0, fontSize: 13, textAlign: "left" }}>{error}</p>
          <button
            className="roles-btn roles-btn-outline"
            style={{ marginLeft: "auto", padding: "4px 12px", fontSize: 12 }}
            onClick={() => fetchRoles(page)}
          >
            <RefreshCw size={11} />
            Retry
          </button>
        </div>
      )}

      {roles.length === 0 ? (
        <div className="roles-empty">
          <Shield size={40} className="roles-empty-icon" />
          <h3>No roles found</h3>
          <p>Create your first role to get started with access control.</p>
          <button className="roles-btn roles-btn-primary" onClick={openCreate}>
            <Plus size={14} />
            Create Role
          </button>
        </div>
      ) : (
        <>
          <div className="roles-table-wrapper">
            <table className="roles-table">
              <thead>
                <tr>
                  <th>Role Name</th>
                  <th>Type</th>
                  <th>Created</th>
                  <th className="col-actions">Actions</th>
                </tr>
              </thead>
              <tbody>
                {roles.map((role) => (
                  <tr key={role.id}>
                    <td className="roles-role-name">{role.name}</td>
                    <td>
                      <span className={`roles-badge ${role.isSystemRole ? "roles-badge-system" : "roles-badge-custom"}`}>
                        {role.isSystemRole ? "System" : "Custom"}
                      </span>
                    </td>
                    <td className="roles-date">{formatDate(role.createdAt)}</td>
                    <td className="col-actions">
                      <button
                        className="roles-action-btn edit"
                        onClick={() => openEdit(role)}
                        title="Edit role"
                      >
                        <Edit2 size={13} />
                      </button>
                      <button
                        className="roles-action-btn delete"
                        onClick={() => openDelete(role)}
                        disabled={role.isSystemRole}
                        title={role.isSystemRole ? "System roles cannot be deleted" : "Delete role"}
                      >
                        <Trash2 size={13} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {renderPagination()}
        </>
      )}

      {/* Create / Edit Modal */}
      {modalMode && (
        <div className="roles-modal-overlay" onClick={closeModal}>
          <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <h2>{modalMode === "create" ? "Create Role" : "Edit Role"}</h2>
              <button className="roles-modal-close" onClick={closeModal}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleSave}>
              <div className="roles-modal-body">
                <div className="roles-form-group">
                  <label htmlFor="role-name">Role Name</label>
                  <input
                    id="role-name"
                    type="text"
                    className={`roles-form-input ${formError ? "error" : ""}`}
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    placeholder="e.g. Department Head"
                    autoFocus
                    maxLength={100}
                  />
                  {formError && <span className="roles-form-error">{formError}</span>}
                  <span className="roles-form-hint">Must be unique and 2–100 characters.</span>
                </div>
              </div>
              <div className="roles-modal-footer">
                <button type="button" className="roles-btn roles-btn-outline" onClick={closeModal}>
                  Cancel
                </button>
                <button type="submit" className="roles-btn roles-btn-primary" disabled={saving}>
                  {(() => {
                    if (saving) return "Saving…";
                    if (modalMode === "create") return "Create";
                    return "Save Changes";
                  })()}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation */}
      {deleteTarget && (
        <div className="roles-modal-overlay" onClick={closeDelete}>
          <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <h2>Delete Role</h2>
              <button className="roles-modal-close" onClick={closeDelete}>
                <X size={16} />
              </button>
            </div>
            <div className="roles-delete-body">
              <AlertTriangle size={36} className="roles-delete-icon" />
              <p>
                Are you sure you want to delete <strong>{deleteTarget.name}</strong>?
              </p>
              <p className="roles-delete-hint">
                This action cannot be undone. Any users assigned to this role will lose its permissions.
              </p>
            </div>
            <div className="roles-modal-footer">
              <button className="roles-btn roles-btn-outline" onClick={closeDelete} disabled={deleting}>
                Cancel
              </button>
              <button className="roles-btn roles-btn-danger" onClick={handleDelete} disabled={deleting}>
                {deleting ? "Deleting…" : "Delete"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default RolesPage;
