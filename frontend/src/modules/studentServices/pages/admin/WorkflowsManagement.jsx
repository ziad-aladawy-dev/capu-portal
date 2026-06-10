import { useState, useEffect, useCallback } from "react";
import { Plus, Edit3, Trash2, Search, X, AlertTriangle, Power } from "lucide-react";
import * as studentServicesService from "../../services/studentServicesService";
import PermissionGate from "../../../../core/auth/PermissionGate";
import WorkflowBuilder from "../../components/WorkflowBuilder";
import "../../styles/admin/WorkflowsManagement.css";

const EMPTY_FORM = { name: "", description: "", isActive: true };

function WorkflowsManagement() {
  const [workflows, setWorkflows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");

  const [modalMode, setModalMode] = useState(null);
  const [editTarget, setEditTarget] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [workflowSteps, setWorkflowSteps] = useState([]);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const [deleteTarget, setDeleteTarget] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await studentServicesService.getWorkflows();
      setWorkflows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load workflows");
      setWorkflows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setModalMode("create");
    setEditTarget(null);
    setForm(EMPTY_FORM);
    setWorkflowSteps([]);
    setFormError("");
  };

  const openEdit = (w) => {
    setModalMode("edit");
    setEditTarget(w);
    setForm({ name: w.name, description: w.description || "", isActive: w.isActive });
    setWorkflowSteps(w.steps || []);
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditTarget(null);
    setForm(EMPTY_FORM);
    setWorkflowSteps([]);
    setFormError("");
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.name.trim()) {
      setFormError("Workflow name is required.");
      return;
    }
    setSaving(true);
    try {
      const data = {
        name: form.name.trim(),
        description: form.description.trim(),
        isActive: form.isActive,
        steps: workflowSteps,
      };
      if (modalMode === "create") {
        await studentServicesService.createWorkflow(data);
      } else {
        await studentServicesService.updateWorkflow(editTarget.id, data);
      }
      closeModal();
      await load();
    } catch (err) {
      setFormError(err.message || "Failed to save workflow");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await studentServicesService.deleteWorkflow(deleteTarget.id);
      setDeleteTarget(null);
      await load();
    } catch (err) {
      setError(err.message || "Failed to delete workflow");
      setDeleteTarget(null);
    }
  };

  const filtered = workflows.filter((w) =>
    w.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="wm-page">
      <div className="wm-header">
        <div>
          <h1>Workflows</h1>
          <p>Manage service workflow definitions and step configurations</p>
        </div>
        <PermissionGate resource="student-services.workflows" minLevel={2}>
          <button className="wm-btn wm-btn-primary" onClick={openCreate}>
            <Plus size={15} /> Create Workflow
          </button>
        </PermissionGate>
      </div>

      <div className="wm-filters-row">
        <div className="wm-search-box">
          <Search size={15} />
          <input
            type="text"
            placeholder="Search workflows…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error && (
        <div className="wm-error-banner">
          <AlertTriangle size={15} />
          <span>{error}</span>
          <button onClick={() => setError(null)} className="wm-error-close"><X size={14} /></button>
        </div>
      )}

      {loading ? (
        <div className="wm-loading">
          <div className="wm-spinner" />
          <p>Loading workflows…</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="wm-empty">
          <Power size={36} />
          <h3>{search ? "No matching workflows" : "No workflows yet"}</h3>
          <p>{search ? "Try a different search term" : "Create your first workflow to define service request steps."}</p>
          {!search && (
            <PermissionGate resource="student-services.workflows" minLevel={2}>
              <button className="wm-btn wm-btn-primary" onClick={openCreate}>
                <Plus size={14} /> Create Workflow
              </button>
            </PermissionGate>
          )}
        </div>
      ) : (
        <div className="wm-table-wrap">
          <table className="wm-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Steps</th>
                <th>Status</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((w) => (
                <tr key={w.id}>
                  <td>
                    <strong>{w.name}</strong>
                    {w.description && <br />}
                    {w.description && <small>{w.description}</small>}
                  </td>
                  <td>{(w.steps || []).length} step{(w.steps || []).length !== 1 ? "s" : ""}</td>
                  <td>
                    <span className={`wm-status-badge ${w.isActive ? "active" : "inactive"}`}>
                      {w.isActive ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td className="wm-cell-muted">
                    {w.createdAt ? new Date(w.createdAt).toLocaleDateString() : "—"}
                  </td>
                  <td>
                    <div className="wm-actions">
                      <PermissionGate resource="student-services.workflows" minLevel={3}>
                        <button className="wm-action-btn edit" onClick={() => openEdit(w)} title="Edit">
                          <Edit3 size={15} />
                        </button>
                      </PermissionGate>
                      <PermissionGate resource="student-services.workflows" minLevel={5}>
                        <button className="wm-action-btn delete" onClick={() => setDeleteTarget(w)} title="Delete">
                          <Trash2 size={15} />
                        </button>
                      </PermissionGate>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modalMode && (
        <div className="wm-overlay" onClick={closeModal}>
          <div className="wm-modal wm-modal-wide" onClick={(e) => e.stopPropagation()}>
            <div className="wm-modal-header">
              <h2>{modalMode === "create" ? "Create Workflow" : "Edit Workflow"}</h2>
              <button className="wm-modal-close" onClick={closeModal}><X size={16} /></button>
            </div>
            <form onSubmit={handleSubmit}>
              <div className="wm-modal-body">
                <div className="wm-form-row cols-2">
                  <div className="wm-form-group">
                    <label>Name <span className="wm-req">*</span></label>
                    <input
                      className="wm-input"
                      value={form.name}
                      onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))}
                      placeholder="e.g. Transcript Request"
                    />
                  </div>
                  <div className="wm-form-group">
                    <label>Status</label>
                    <label className="wm-toggle-row">
                      <input
                        type="checkbox"
                        checked={form.isActive}
                        onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                      />
                      {form.isActive ? "Active" : "Inactive"}
                    </label>
                  </div>
                </div>
                <div className="wm-form-group">
                  <label>Description</label>
                  <textarea
                    className="wm-input wm-textarea"
                    value={form.description}
                    onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))}
                    placeholder="Brief description of this workflow"
                    rows={2}
                  />
                </div>
                <div className="wm-form-divider" />
                <WorkflowBuilder
                  workflow={{ steps: workflowSteps }}
                  onChange={({ steps }) => setWorkflowSteps(steps)}
                />
                {formError && <span className="wm-form-error">{formError}</span>}
              </div>
              <div className="wm-modal-footer">
                <button type="button" className="wm-btn wm-btn-outline" onClick={closeModal} disabled={saving}>
                  Cancel
                </button>
                <button type="submit" className="wm-btn wm-btn-primary" disabled={saving}>
                  {saving ? "Saving…" : modalMode === "create" ? "Create" : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {deleteTarget && (
        <div className="wm-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="wm-modal" onClick={(e) => e.stopPropagation()}>
            <div className="wm-modal-header">
              <h2>Delete Workflow</h2>
              <button className="wm-modal-close" onClick={() => setDeleteTarget(null)}><X size={16} /></button>
            </div>
            <div className="wm-delete-body">
              <AlertTriangle size={32} color="#dc2626" />
              <p>
                Delete workflow <strong>"{deleteTarget.name}"</strong>?
              </p>
              <p className="wm-delete-hint">This cannot be undone. Services referencing this workflow will lose their workflow definition.</p>
            </div>
            <div className="wm-modal-footer">
              <button className="wm-btn wm-btn-outline" onClick={() => setDeleteTarget(null)}>Cancel</button>
              <button className="wm-btn wm-btn-danger" onClick={handleDelete}>Delete</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default WorkflowsManagement;
