import PropTypes from "prop-types";
import { useState, useEffect } from "react";
import { X } from "lucide-react";
import { normalizeType, getAllowedChildTypes, getNodeTypeValue } from "../utils/nodeTypeHelpers";

const ALL_NODE_TYPES = [
  { value: "University", label: "University" },
  { value: "Faculty", label: "Faculty" },
  { value: "Department", label: "Department" },
  { value: "System", label: "System" },
  { value: "Program", label: "Program" },
  { value: "Level", label: "Level" },
  { value: "Specialization", label: "Specialization" },
];

export function AddEditNodeModal({ isOpen, onClose, onSave, node, parentId, parentType, siblingsCount }) {
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [order, setOrder] = useState(0);
  const [errors, setErrors] = useState({});
  const [allowedTypes, setAllowedTypes] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isEditMode = !!node;
  const submitLabel = (() => {
    if (isSubmitting) return "Saving...";
    if (isEditMode) return "Update";
    return "Create";
  })();

  useEffect(() => {
    if (isOpen) {
      if (isEditMode) {
        setName(node.name || "");
        const currentType = node.type ? normalizeType(node.type) : "Department";
        setType(currentType);
        setOrder(node.order ?? 0);
        setAllowedTypes(ALL_NODE_TYPES.filter(t => t.value === currentType));
      } else {
        setName("");
        const parentTypeStr = parentType ? normalizeType(parentType) : null;
        if (parentTypeStr) {
          const allowed = getAllowedChildTypes(parentTypeStr);
          setAllowedTypes(ALL_NODE_TYPES.filter(t => allowed.includes(t.value)));
          setType(allowed.length > 0 ? allowed[0] : "");
        } else {
          setAllowedTypes(ALL_NODE_TYPES.filter(t => t.value === "University"));
          setType("University");
        }
        setOrder(siblingsCount ?? 0);
      }
    }
  }, [node, siblingsCount, isOpen, parentType, isEditMode]);

  const validate = () => {
    const newErrors = {};
    if (!name.trim()) newErrors.name = "Name is required";
    if (!type) newErrors.type = "Type is required";
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) return;
    setIsSubmitting(true);

    const numericType = getNodeTypeValue(type);
    
    const request = {
      name: name.trim(),
      type: numericType,
      parentId: isEditMode ? node.parentId : (parentId || null),
      order: order,
    };
    
    try {
      await onSave(request, node?.id);
      onClose();
    } catch (err) {
      console.error("Save failed", err);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-container" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{isEditMode ? "Edit Node" : "Add New Node"}</h3>
          <button className="modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <div className="form-group">
              <label>Name *</label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className={errors.name ? "error" : ""}
                disabled={isSubmitting}
              />
              {errors.name && <span className="error-message">{errors.name}</span>}
            </div>
            <div className="form-group">
              <label>Type *</label>
              <select value={type} onChange={(e) => setType(e.target.value)} disabled={isEditMode ||allowedTypes.length === 1 || isSubmitting}>
                {allowedTypes.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
              {isEditMode && (
                <small className="input-hint" style={{ color: "#c9a84c" }}>
                  Type cannot be changed during edit.
                </small>
              )}
            </div>
            <div className="form-group">
              <label>Order</label>
              <input
                type="number"
                value={order}
                onChange={(e) => setOrder(parseInt(e.target.value) || 0)}
                min="0"
                disabled={isSubmitting}
              />
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={isSubmitting}>
              {submitLabel}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

AddEditNodeModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onClose: PropTypes.func.isRequired,
  onSave: PropTypes.func.isRequired,
  node: PropTypes.shape({
    id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
    name: PropTypes.string,
    type: PropTypes.string,
    parentId: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
    order: PropTypes.number,
  }),
  parentId: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  parentType: PropTypes.string,
  siblingsCount: PropTypes.number,
};