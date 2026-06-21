import { useTranslation } from "react-i18next";
import { getLocalized } from "../../../core/utils/getLocalized";
import { STEP_FIELD_TYPE } from "../constants/workflowTypes";
import FileUploader from "./FileUploader";
import styles from "./DynamicFormRenderer.module.css";

/**
 * Renders one Form step's fields from the backend WorkflowStepFieldDto shape:
 * { id, order, label, fieldType (int), isRequired, options }.
 *
 * - `values`        { [fieldId]: value } for non-file fields
 * - `onChange`      (fieldId, nextValue)
 * - `attachments`   { [fieldId]: fileEntry[] } for File (7) fields
 * - `onAttachmentsChange` (fieldId, nextFiles)
 * - `requestId` / `stepKey` feed the embedded FileUploader.
 */
function DynamicFormRenderer({
  fields = [],
  values = {},
  onChange,
  requestId,
  stepKey,
  attachments = {},
  onAttachmentsChange,
  disabled = false,
}) {
  const { t, i18n } = useTranslation();
  const sorted = [...fields].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

  const renderControl = (field) => {
    const val = values[field.id] ?? "";
    const localizedLabel = getLocalized(field.label, i18n.language) || field.label;

    switch (field.fieldType) {
      case STEP_FIELD_TYPE.Text:
        return (
          <input
            type="text"
            className={styles.input}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          />
        );

      case STEP_FIELD_TYPE.TextArea:
        return (
          <textarea
            rows={3}
            className={styles.textarea}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          />
        );

      case STEP_FIELD_TYPE.Number:
        return (
          <input
            type="number"
            className={styles.input}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          />
        );

      case STEP_FIELD_TYPE.Date:
        return (
          <input
            type="date"
            className={styles.input}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          />
        );

      case STEP_FIELD_TYPE.Select:
        return (
          <select
            className={styles.select}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          >
            <option value="">{t("portal_requests.select_option", { defaultValue: "Select…" })}</option>
            {(field.options || []).map((opt) => (
              <option key={opt} value={opt}>{opt}</option>
            ))}
          </select>
        );

      case STEP_FIELD_TYPE.MultiSelect: {
        const selected = Array.isArray(val) ? val : [];
        const toggle = (opt) =>
          onChange(
            field.id,
            selected.includes(opt) ? selected.filter((o) => o !== opt) : [...selected, opt]
          );
        return (
          <div className={styles.optionGroup}>
            {(field.options || []).map((opt) => (
              <label key={opt} className={styles.optionLabel}>
                <input
                  type="checkbox"
                  checked={selected.includes(opt)}
                  onChange={() => toggle(opt)}
                  disabled={disabled}
                />
                <span>{opt}</span>
              </label>
            ))}
          </div>
        );
      }

      case STEP_FIELD_TYPE.File:
        return (
          <FileUploader
            requestId={requestId}
            stepKey={stepKey}
            value={attachments[field.id] || []}
            onChange={(files) => onAttachmentsChange?.(field.id, files)}
            disabled={disabled}
          />
        );

      case STEP_FIELD_TYPE.Checkbox:
        return (
          <label className={styles.optionLabel}>
            <input
              type="checkbox"
              checked={Boolean(val)}
              onChange={(e) => onChange(field.id, e.target.checked)}
              disabled={disabled}
            />
            <span>{localizedLabel}</span>
          </label>
        );

      case STEP_FIELD_TYPE.Radio:
        return (
          <div className={styles.optionGroup}>
            {(field.options || []).map((opt) => (
              <label key={opt} className={styles.optionLabel}>
                <input
                  type="radio"
                  name={`dfr-${field.id}`}
                  checked={val === opt}
                  onChange={() => onChange(field.id, opt)}
                  disabled={disabled}
                />
                <span>{opt}</span>
              </label>
            ))}
          </div>
        );

      default:
        return (
          <input
            type="text"
            className={styles.input}
            value={val}
            onChange={(e) => onChange(field.id, e.target.value)}
            disabled={disabled}
          />
        );
    }
  };

  return (
    <div className={styles.container}>
      {sorted.map((field) => {
        const localizedLabel = getLocalized(field.label, i18n.language) || field.label;
        return (
          <div key={field.id} className={styles.field}>
            {field.fieldType !== STEP_FIELD_TYPE.Checkbox && (
              <label className={styles.label}>
                {localizedLabel}
                {field.isRequired && <span className={styles.required}> *</span>}
              </label>
            )}
            {renderControl(field)}
          </div>
        );
      })}
    </div>
  );
}

export default DynamicFormRenderer;