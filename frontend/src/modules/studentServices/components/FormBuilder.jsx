import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Plus, GripVertical, Trash2, MoveUp, MoveDown, ChevronRight, CheckCircle } from "lucide-react";
import "../styles/components/FormBuilder.css";

const fieldTypes = [
  { value: "Text",        label: "نص قصير" },
  { value: "TextArea",    label: "نص طويل" },
  { value: "Number",      label: "رقم" },
  { value: "Date",        label: "تاريخ" },
  { value: "Select",      label: "قائمة منسدلة" },
  { value: "MultiSelect", label: "قائمة متعددة" },
  { value: "File",        label: "رفع ملف" },
  { value: "Checkbox",    label: "خانة اختيار" },
];

const generateId = () => Date.now() + "-" + Math.random().toString(36).substr(2, 9);

const ensureUniqueIds = (fields) => {
  return fields.map(f => ({
    ...f,
    id: f.id && typeof f.id === 'string' ? f.id : generateId()
  }));
};

const FormBuilder = ({ fields = [], onChange }) => {
  const { t } = useTranslation();
  const [localFields, setLocalFields] = useState(() => ensureUniqueIds(fields));

  useEffect(() => {
    setLocalFields(ensureUniqueIds(fields));
  }, [fields]);

  const commit = (updated) => {
    setLocalFields(updated);
    onChange(updated);
  };

  const addField = () => {
    const newField = {
      id: generateId(),
      type: "Text",
      label: t("new_field"),
      required: false,
      options: []
    };
    commit([...localFields, newField]);
  };

  const updateField = (id, key, value) =>
    commit(localFields.map(f => f.id === id ? { ...f, [key]: value } : f));

  const removeField = (id) => commit(localFields.filter(f => f.id !== id));

  const moveField = (id, dir) => {
    const idx = localFields.findIndex(f => f.id === id);
    if ((dir === "up" && idx === 0) || (dir === "down" && idx === localFields.length - 1)) return;
    const ni = dir === "up" ? idx - 1 : idx + 1;
    const u = [...localFields];
    [u[idx], u[ni]] = [u[ni], u[idx]];
    commit(u);
  };

  return (
    <div className="fb-container">
      <div className="fb-header">
        <h4>{t("dynamic_fields")}</h4>
        <button className="fb-add-btn" onClick={addField}>
          <Plus size={12} /> {t("add_field")}
        </button>
      </div>

      {localFields.length === 0 && <div className="fb-empty">{t("no_fields")}</div>}

      <div className="fb-fields-list">
        {localFields.map(field => (
          <div key={field.id} className="fb-field-item">
            <div className="fb-field-drag"><GripVertical size={15} /></div>
            <div className="fb-field-controls">
              <div className="fb-select-wrap">
                <select value={field.type} onChange={e => updateField(field.id, "type", e.target.value)}>
                  {fieldTypes.map(opt => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
                <ChevronRight size={12} className="fb-select-arrow" />
              </div>
              <input
                placeholder={t("label")}
                value={field.label}
                onChange={e => updateField(field.id, "label", e.target.value)}
              />
              <label className="fb-custom-checkbox">
                <input
                  type="checkbox"
                  checked={field.required}
                  onChange={e => updateField(field.id, "required", e.target.checked)}
                />
                <span className="fb-cb-box"><CheckCircle size={9} /></span>
                <span className="fb-cb-text">{t("required")}</span>
              </label>
            </div>
            <div className="fb-field-actions">
              <button onClick={() => moveField(field.id, "up")} title={t("move_up")}>
                <MoveUp size={13} />
              </button>
              <button onClick={() => moveField(field.id, "down")} title={t("move_down")}>
                <MoveDown size={13} />
              </button>
              <button onClick={() => removeField(field.id)} className="fb-danger" title={t("delete")}>
                <Trash2 size={13} />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default FormBuilder;