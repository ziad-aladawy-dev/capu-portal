import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { X } from "lucide-react";
import { parseLocalizedValue } from "../../../core/utils/getLocalized";
import { normalizeType, getAllowedChildTypes, getNodeTypeValue, getNodeMetadataFields } from "../utils/nodeTypeHelpers";
import { ALL_NODE_TYPES_LIST, NODE_TYPES, getNodeTypeLabel } from "../utils/nodeTypeRegistry";

const METADATA_INPUT_CONFIG = {
  durationYears: { labelKey: "duration_years", type: "number", min: 1, max: 10 },
  degreeType: {
    labelKey: "degree_type",
    type: "select",
    options: ["Bachelor", "Master", "PhD", "Diploma"],
  },
  deanId: { labelKey: "dean", type: "text" },
  establishedDate: { labelKey: "established_date", type: "date" },
};

export function AddEditNodeModal({ isOpen, onClose, onSave, node, parentId, parentType, siblingsCount }) {
  const { t } = useTranslation();
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [type, setType] = useState("");
  const [order, setOrder] = useState(0);
  const [metadata, setMetadata] = useState({});
  const [errors, setErrors] = useState({});
  const [allowedTypes, setAllowedTypes] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isEditMode = !!node;

  const metadataFields = getNodeMetadataFields(type);

  useEffect(() => {
    if (isOpen) {
      if (isEditMode) {
        const { ar, en } = parseLocalizedValue(node.name);
        setNameAr(ar);
        setNameEn(en);
        const currentType = node.type ? normalizeType(node.type) : getNodeTypeLabel(NODE_TYPES.Department);
        setType(currentType);
        setOrder(node.order ?? 0);
        setAllowedTypes(ALL_NODE_TYPES_LIST.filter(t => t.value === currentType));
        const fields = getNodeMetadataFields(currentType);
        const initialMetadata = {};
        fields.forEach(f => { initialMetadata[f] = node.metadata?.[f] ?? ""; });
        setMetadata(initialMetadata);
      } else {
        setNameAr("");
        setNameEn("");
        const parentTypeStr = parentType ? normalizeType(parentType) : null;
        if (parentTypeStr) {
          const allowed = getAllowedChildTypes(parentTypeStr);
          setAllowedTypes(ALL_NODE_TYPES_LIST.filter(t => allowed.includes(t.value)));
          setType(allowed.length > 0 ? allowed[0] : "");
        } else {
          setAllowedTypes(ALL_NODE_TYPES_LIST.filter(t => t.value === getNodeTypeLabel(NODE_TYPES.University)));
          setType(getNodeTypeLabel(NODE_TYPES.University));
        }
        setOrder(siblingsCount ?? 0);
      }
    }
  }, [node, siblingsCount, isOpen, parentType, isEditMode]);

  useEffect(() => {
    if (!isEditMode && type) {
      const fields = getNodeMetadataFields(type);
      setMetadata(prev => {
        const next = {};
        fields.forEach(f => { next[f] = prev[f] ?? ""; });
        return next;
      });
    }
  }, [type, isEditMode]);

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
                {allowedTypes.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {t(opt.labelKey) || opt.value}
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
            {metadataFields.length > 0 && (
              <div className="metadata-section" style={{ borderTop: "1px solid #e5e7eb", paddingTop: 12, marginTop: 4 }}>
                <label style={{ fontSize: 12, color: "#888", marginBottom: 8, display: "block", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.5px" }}>
                  {t("additional_info")}
                </label>
                {metadataFields.map(field => {
                  const config = METADATA_INPUT_CONFIG[field];
                  if (!config) return null;
                  return (
                    <div key={field} className="form-group">
                      <label>{t(config.labelKey)}</label>
                      {config.type === "select" ? (
                        <select
                          value={metadata[field] ?? ""}
                          onChange={(e) => setMetadata(prev => ({ ...prev, [field]: e.target.value }))}
                          disabled={isSubmitting}
                        >
                          <option value="">— {t("select")} —</option>
                          {(config.options || []).map(opt => (
                            <option key={opt} value={opt}>{t(`degree_${opt}`)}</option>
                          ))}
                        </select>
                      ) : (
                        <input
                          type={config.type}
                          value={metadata[field] ?? ""}
                          onChange={(e) => setMetadata(prev => ({ ...prev, [field]: e.target.value }))}
                          min={config.min}
                          max={config.max}
                          disabled={isSubmitting}
                        />
                      )}
                    </div>
                  );
                })}
              </div>
            )}
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
