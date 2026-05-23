import { X } from "lucide-react";

function AcademicYearForm({
  mode,
  year,
  formData,
  setFormData,
  formError,
  saving,
  onSave,
  onClose,
}) {
  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData({
      ...formData,
      [name]: type === "checkbox" ? checked : value,
    });
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{mode === "create" ? "Create Academic Year" : "Edit Academic Year"}</h3>
          <button className="btn-close" onClick={onClose} disabled={saving}>
            <X size={20} />
          </button>
        </div>

        <form onSubmit={onSave} className="academic-year-form">
          <div className="form-group">
            <label htmlFor="name">Academic Year Name *</label>
            <input
              id="name"
              type="text"
              name="name"
              value={formData.name}
              onChange={handleChange}
              placeholder="e.g., 2024-2025"
              disabled={saving}
              required
            />
            <small>e.g., 2024-2025 or Academic Year 2024</small>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="startDate">Start Date *</label>
              <input
                id="startDate"
                type="date"
                name="startDate"
                value={formData.startDate}
                onChange={handleChange}
                disabled={saving}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="endDate">End Date *</label>
              <input
                id="endDate"
                type="date"
                name="endDate"
                value={formData.endDate}
                onChange={handleChange}
                disabled={saving}
                required
              />
            </div>
          </div>

          <div className="form-group checkbox">
            <label htmlFor="isCurrent">
              <input
                id="isCurrent"
                type="checkbox"
                name="isCurrent"
                checked={formData.isCurrent}
                onChange={handleChange}
                disabled={saving}
              />
              Mark as current academic year
            </label>
          </div>

          {formError && (
            <div className="form-error">
              <small>{formError}</small>
            </div>
          )}

          <div className="modal-footer">
            <button
              type="button"
              className="btn-cancel"
              onClick={onClose}
              disabled={saving}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={saving}
            >
              {saving ? "Saving..." : mode === "create" ? "Create" : "Update"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default AcademicYearForm;
