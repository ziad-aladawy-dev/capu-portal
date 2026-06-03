import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, Trash2, MoveUp, MoveDown } from "lucide-react";
import FormBuilder from "./FormBuilder";
import "../styles/components/WorkflowBuilder.css";

const WorkflowBuilder = ({ workflow = { steps: [] }, onChange }) => {
  const { t } = useTranslation();
  const [steps, setSteps] = useState(workflow.steps || []);

  const addStep = () => {
    const newStep = {
      id: Date.now(),
      order: steps.length + 1,
      title: "New Step",
      description: "",
      stepType: "Form",
      isRequired: true,
      fields: [],
    };
    const updated = [...steps, newStep];
    setSteps(updated);
    onChange({ steps: updated });
  };

  const updateStep = (id, key, value) => {
    const updated = steps.map((s) => (s.id === id ? { ...s, [key]: value } : s));
    setSteps(updated);
    onChange({ steps: updated });
  };

  const removeStep = (id) => {
    const updated = steps.filter((s) => s.id !== id).map((s, idx) => ({ ...s, order: idx + 1 }));
    setSteps(updated);
    onChange({ steps: updated });
  };

  const moveStep = (id, direction) => {
    const index = steps.findIndex((s) => s.id === id);
    if ((direction === "up" && index === 0) || (direction === "down" && index === steps.length - 1)) return;
    const newIndex = direction === "up" ? index - 1 : index + 1;
    const updated = [...steps];
    [updated[index], updated[newIndex]] = [updated[newIndex], updated[index]];
    updated.forEach((s, i) => (s.order = i + 1));
    setSteps(updated);
    onChange({ steps: updated });
  };

  const handleFieldsChange = (stepId, fields) => {
    const updated = steps.map((s) => (s.id === stepId ? { ...s, fields } : s));
    setSteps(updated);
    onChange({ steps: updated });
  };

  return (
    <div className="wb-container">
      <div className="wb-header">
        <h4>{t("workflow_steps")}</h4>
        <button className="wb-add-step" onClick={addStep}>
          <Plus size={14} /> {t("add_step")}
        </button>
      </div>
      {steps.length === 0 && <div className="wb-empty">{t("no_steps")}</div>}
      <div className="wb-steps-list">
        {steps.map((step) => (
          <div key={step.id} className="wb-step-card">
            <div className="wb-step-header">
              <div className="wb-step-order">#{step.order}</div>
              <input
                value={step.title}
                onChange={(e) => updateStep(step.id, "title", e.target.value)}
                placeholder={t("step_title")}
                className="wb-step-title-input"
              />
              <div className="wb-step-actions">
                <button onClick={() => moveStep(step.id, "up")} title={t("move_up")}><MoveUp size={14} /></button>
                <button onClick={() => moveStep(step.id, "down")} title={t("move_down")}><MoveDown size={14} /></button>
                <button onClick={() => removeStep(step.id)} className="wb-danger" title={t("delete")}><Trash2 size={14} /></button>
              </div>
            </div>
            <div className="wb-step-details">
              <textarea
                value={step.description || ""}
                onChange={(e) => updateStep(step.id, "description", e.target.value)}
                placeholder={t("step_description")}
                rows="2"
              />
              <div className="wb-step-type">
                <label>{t("step_type")}</label>
                <select value={step.stepType} onChange={(e) => updateStep(step.id, "stepType", e.target.value)}>
                  <option value="Form">Form (نموذج)</option>
                  <option value="FileUpload">File Upload (رفع ملفات)</option>
                  <option value="Review">Review (مراجعة)</option>
                  <option value="Payment">Payment (دفع)</option>
                  <option value="Submit">Submit (إرسال)</option>
                </select>
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={step.isRequired}
                    onChange={(e) => updateStep(step.id, "isRequired", e.target.checked)}
                  />
                  {t("required_step")}
                </label>
              </div>
            </div>
            {step.stepType === "Form" && (
              <div className="wb-step-fields">
                <FormBuilder fields={step.fields || []} onChange={(fields) => handleFieldsChange(step.id, fields)} />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};

export default WorkflowBuilder;