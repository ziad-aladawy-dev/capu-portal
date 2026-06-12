
import { useTranslation } from "react-i18next";
import FileUploader from "./FileUploader";
import { STEP_FIELD_TYPE } from "../../../core/constants/workflowTypes";
import "../styles/components/DynamicFormRenderer.css";

const DynamicFormRenderer = ({ fields = [], value = {}, onChange, requestId }) => {
  const { t } = useTranslation();
  if (!fields.length) return <div className="dfr-empty">{t("no_fields")}</div>;

  const handleChange = (fieldId, val) => onChange({ ...value, [fieldId]: val });

  const getFieldTypeNumber = (ft) => {
    if (typeof ft === "number") return ft;
    return STEP_FIELD_TYPE[ft] || STEP_FIELD_TYPE.Text;
  };

  const renderField = (field) => {
    const typeNum = getFieldTypeNumber(field.fieldType);
    const val = value[field.id] ?? "";
    switch (typeNum) {
      case STEP_FIELD_TYPE.Text:
        return <input type="text" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case STEP_FIELD_TYPE.TextArea:
        return <textarea rows="3" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case STEP_FIELD_TYPE.Number:
        return <input type="number" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case STEP_FIELD_TYPE.Date:
        return <input type="date" value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired} />;
      case STEP_FIELD_TYPE.Select:
        return (
          <select value={val} onChange={e => handleChange(field.id, e.target.value)} required={field.isRequired}>
            <option value="">{t("select_option")}</option>
            {field.options?.map(opt => <option key={opt} value={opt}>{opt}</option>)}
          </select>
        );
      case STEP_FIELD_TYPE.MultiSelect:
        return (
          <select multiple value={Array.isArray(val) ? val : []} onChange={e => handleChange(field.id, Array.from(e.target.selectedOptions, o => o.value))} required={field.isRequired}>
            {field.options?.map(opt => <option key={opt} value={opt}>{opt}</option>)}
          </select>
        );
      case STEP_FIELD_TYPE.File:
        return <FileUploader requestId={requestId} stepKey={field.id} value={val} onChange={(files) => handleChange(field.id, files)} />;
      case STEP_FIELD_TYPE.Checkbox:
        return <input type="checkbox" checked={!!val} onChange={e => handleChange(field.id, e.target.checked)} />;
      case STEP_FIELD_TYPE.Radio:
        return (
          <div className="dfr-radio-group" role="radiogroup">
            {field.options?.map(opt => (
              <label key={opt} className="dfr-radio-option">
                <input
                  type="radio"
                  name={field.id}
                  value={opt}
                  checked={val === opt}
                  onChange={() => handleChange(field.id, opt)}
                  required={field.isRequired}
                />
                <span>{opt}</span>
              </label>
            ))}
          </div>
        );
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