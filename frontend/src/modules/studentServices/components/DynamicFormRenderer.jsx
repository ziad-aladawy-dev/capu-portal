import React from "react";
import { useTranslation } from "react-i18next";
import FileUploader from "./FileUploader";
import "../styles/components/DynamicFormRenderer.css";

const DynamicFormRenderer = ({ fields = [], value = {}, onChange, requestId }) => {
  const { t } = useTranslation();
  if (!fields.length) return <div className="dfr-empty">{t("no_fields")}</div>;

  const handleChange = (fieldId, val) => onChange({ ...value, [fieldId]: val });

  const getFieldTypeNumber = (ft) => {
    if (typeof ft === "number") return ft;
    const map = { Text:1, TextArea:2, Number:3, Date:4, Select:5, MultiSelect:6, File:7, Checkbox:8, Radio:9 };
    return map[ft] || 1;
  };

  const renderField = (field) => {
    const typeNum = getFieldTypeNumber(field.fieldType);
    const val = value[field.id] ?? "";
    switch (typeNum) {
      case 1: // Text
        return <input type="text" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case 2: // TextArea
        return <textarea rows="3" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case 3: // Number
        return <input type="number" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case 4: // Date
        return <input type="date" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case 5: // Select
        return (
          <select value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired}>
            <option value="">{t("select_option")}</option>
            {field.options?.map(opt => <option key={opt} value={opt}>{opt}</option>)}
          </select>
        );
      case 6: // MultiSelect
        return (
          <select multiple value={Array.isArray(val) ? val : []} onChange={e => handleChange(field.id, Array.from(e.target.selectedOptions, o => o.value))} required={field.isRequired}>
            {field.options?.map(opt => <option key={opt} value={opt}>{opt}</option>)}
          </select>
        );
      case 7: // File
        return <FileUploader requestId={requestId} stepKey={field.id} value={val} onChange={(files) => handleChange(field.id, files)} />;
      case 8: // Checkbox
        return <input type="checkbox" checked={!!val} onChange={e => handleChange(field.id, e.target.checked)} />;
      default:
        return <input type="text" value={val} onChange={e => handleChange(field.id, e.target.value)} />;
    }
  };

  return (
    <div className="dfr-container">
      {fields.map(field => (
        <div key={field.id} className="dfr-field">
          <label>{field.label}{field.isRequired && <span className="dfr-required"> *</span>}</label>
          {renderField(field)}
        </div>
      ))}
    </div>
  );
};

export default DynamicFormRenderer;