import React from "react";
import { useTranslation } from "react-i18next";
import "../styles/components/DynamicFormRenderer.css";

const DynamicFormRenderer = ({ fields = [], value = {}, onChange }) => {
  const { t } = useTranslation();

  const handleChange = (id, val) => {
    onChange({ ...value, [id]: val });
  };

  const renderField = (field) => {
    const val = value[field.id] ?? "";
    switch (field.type) {
      case "text":
      case "number":
      case "date":
        return (
          <input
            type={field.type}
            value={val}
            onChange={(e) => handleChange(field.id, e.target.value)}
            required={field.required}
            placeholder={field.placeholder || ""}
          />
        );
      case "textarea":
        return (
          <textarea
            rows={field.rows || 3}
            value={val}
            onChange={(e) => handleChange(field.id, e.target.value)}
            required={field.required}
            placeholder={field.placeholder || ""}
          />
        );
      case "select":
        return (
          <select
            value={val}
            onChange={(e) => handleChange(field.id, e.target.value)}
            required={field.required}
          >
            <option value="">{t("select_option")}</option>
            {field.options?.map((opt) => (
              <option key={opt.value || opt} value={opt.value || opt}>
                {opt.label || opt}
              </option>
            ))}
          </select>
        );
      case "checkbox":
        return (
          <label className="dfr-checkbox-label">
            <input
              type="checkbox"
              checked={!!val}
              onChange={(e) => handleChange(field.id, e.target.checked)}
            />
            <span>{field.label}</span>
          </label>
        );
      default:
        return (
          <input
            type="text"
            value={val}
            onChange={(e) => handleChange(field.id, e.target.value)}
            required={field.required}
          />
        );
    }
  };

  return (
    <div className="dfr-container">
      {fields.map((field) => (
        <div key={field.id} className="dfr-field">
          {field.type !== "checkbox" && (
            <label>
              {field.label}
              {field.required && <span className="dfr-required"> *</span>}
            </label>
          )}
          {renderField(field)}
        </div>
      ))}
    </div>
  );
};

export default DynamicFormRenderer;