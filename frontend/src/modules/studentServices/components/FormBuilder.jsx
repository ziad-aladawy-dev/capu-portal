import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, GripVertical, Trash2, MoveUp, MoveDown } from "lucide-react";
import "../styles/components/FormBuilder.css";

const fieldTypes = [
  { value: "Text", label: "نص قصير" },
  { value: "TextArea", label: "نص طويل" },
  { value: "Number", label: "رقم" },
  { value: "Date", label: "تاريخ" },
  { value: "Select", label: "قائمة منسدلة" },
  { value: "MultiSelect", label: "قائمة متعددة" },
  { value: "File", label: "رفع ملف" },
  { value: "Checkbox", label: "خانة اختيار" },
];

const FormBuilder = ({ fields = [], onChange }) => {
  const { t } = useTranslation();
  const [localFields, setLocalFields] = useState(fields);

  const addField = () => {
    const newField = {
      id: Date.now(),
      type: "Text",
      label: t("new_field"),
      required: false,
      options: [],
    };
    const updated = [...localFields, newField];
    setLocalFields(updated);
    onChange(updated);
  };

  const updateField = (id, key, value) => {
    const updated = localFields.map((f) =>
      f.id === id ? { ...f, [key]: value } : f
    );
    setLocalFields(updated);
    onChange(updated);
  };

  const removeField = (id) => {
    const updated = localFields.filter((f) => f.id !== id);
    setLocalFields(updated);
    onChange(updated);
  };

  const moveField = (id, direction) => {
    const index = localFields.findIndex((f) => f.id === id);
    if ((direction === "up" && index === 0) || (direction === "down" && index === localFields.length - 1)) return;
    const newIndex = direction === "up" ? index - 1 : index + 1;
    const updated = [...localFields];
    [updated[index], updated[newIndex]] = [updated[newIndex], updated[index]];
    setLocalFields(updated);
    onChange(updated);
  };

  return (
    <div className="fb-container">
      <div className="fb-header">
        <h4>{t("dynamic_fields")}</h4>
        <button className="fb-add-btn" onClick={addField}>
          <Plus size={14} /> {t("add_field")}
        </button>
      </div>
      {localFields.length === 0 && <div className="fb-empty">{t("no_fields")}</div>}
      <div className="fb-fields-list">
        {localFields.map((field) => (
          <div key={field.id} className="fb-field-item">
            <div className="fb-field-drag"><GripVertical size={16} /></div>
            <div className="fb-field-controls">
              <select value={field.type} onChange={(e) => updateField(field.id, "type", e.target.value)}>
                {fieldTypes.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
              <input placeholder={t("label")} value={field.label} onChange={(e) => updateField(field.id, "label", e.target.value)} />
              <label className="fb-checkbox">
                <input type="checkbox" checked={field.required} onChange={(e) => updateField(field.id, "required", e.target.checked)} />
                {t("required")}
              </label>
            </div>
            <div className="fb-field-actions">
              <button onClick={() => moveField(field.id, "up")} title={t("move_up")}><MoveUp size={14} /></button>
              <button onClick={() => moveField(field.id, "down")} title={t("move_down")}><MoveDown size={14} /></button>
              <button onClick={() => removeField(field.id)} className="fb-danger" title={t("delete")}><Trash2 size={14} /></button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default FormBuilder;