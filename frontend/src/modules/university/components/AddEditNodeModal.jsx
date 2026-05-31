import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation();
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [type, setType] = useState("");
  const [order, setOrder] = useState(0);
  const [errors, setErrors] = useState({});
  const [allowedTypes, setAllowedTypes] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isEditMode = !!node;

  const parseNameFromJson = (nameJson) => {
    if (!nameJson) return { ar: "", en: "" };
    try {
      const parsed = JSON.parse(nameJson);
      return { ar: parsed.ar || "", en: parsed.en || "" };
    } catch {
      return { ar: nameJson, en: nameJson };
    }
  };

  useEffect(() => {
    if (isOpen) {
      if (isEditMode) {
        const { ar, en } = parseNameFromJson(node.name);
        setNameAr(ar);
        setNameEn(en);
        const currentType = node.type ? normalizeType(node.type) : "Department";
        setType(currentType);
        setOrder(node.order ?? 0);
        setAllowedTypes(ALL_NODE_TYPES.filter(t => t.value === currentType));
      } else {
        setNameAr("");
        setNameEn("");
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
    if (!nameAr.trim()) newErrors.nameAr = t("name_required");
    if (!type) newErrors.type = t("type_required");
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) return;
    setIsSubmitting(true);

    const numericType = getNodeTypeValue(type);
    const request = {
      name: nameAr.trim(),
      nameEn: nameEn.trim() || nameAr.trim(),
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
          <h3>{isEditMode ? t("edit_node") : t("add_new_node")}</h3>
          <button className="modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <div className="form-group">
              <label>{t("name_arabic")} *</label>
              <input
                type="text"
                value={nameAr}
                onChange={(e) => setNameAr(e.target.value)}
                className={errors.nameAr ? "error" : ""}
                disabled={isSubmitting}
              />
              {errors.nameAr && <span className="error-message">{errors.nameAr}</span>}
            </div>
            <div className="form-group">
              <label>{t("name_english")}</label>
              <input
                type="text"
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                disabled={isSubmitting}
              />
              <small className="input-hint">{t("english_name_hint")}</small>
            </div>
            <div className="form-group">
              <label>{t("type")} *</label>
              <select 
                value={type} 
                onChange={(e) => setType(e.target.value)} 
                disabled={isEditMode || allowedTypes.length === 1 || isSubmitting}
              >
                {allowedTypes.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
              {isEditMode && (
                <small className="input-hint" style={{ color: "#c9a84c" }}>
                  {t("type_cannot_change")}
                </small>
              )}
            </div>
            <div className="form-group">
              <label>{t("order")}</label>
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
              {t("cancel")}
            </button>
            <button type="submit" className="btn-primary" disabled={isSubmitting}>
              {isSubmitting ? t("saving") : (isEditMode ? t("update") : t("create"))}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}