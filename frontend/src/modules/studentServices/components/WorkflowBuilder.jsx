import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, Trash2, MoveUp, MoveDown, ChevronRight, CheckCircle } from "lucide-react";
import FormBuilder from "./FormBuilder";
import "../styles/components/WorkflowBuilder.css";

const stepTypeToNumber = { Form: 1, Review: 2, Payment: 3 };
const numberToStepType = { 1: "Form", 2: "Review", 3: "Payment" };

const WorkflowBuilder = ({ workflow = { steps: [] }, onChange }) => {
  const { t } = useTranslation();
  const [steps, setSteps] = useState(workflow.steps || []);

  const commit = (updated) => { setSteps(updated); onChange({ steps: updated }); };

  const addStep = () => {
    const newStep = { order: steps.length + 1, title: "New Step", description: "", stepType: 1, isRequired: true, fields: [] };
    commit([...steps, newStep]);
  };

  const updateStep = (order, key, value) =>
    commit(steps.map(s => s.order === order ? { ...s, [key]: value } : s));

  const removeStep = (order) =>
    commit(steps.filter(s => s.order !== order).map((s, i) => ({ ...s, order: i + 1 })));

  const moveStep = (order, dir) => {
    const idx = steps.findIndex(s => s.order === order);
    if ((dir === "up" && idx === 0) || (dir === "down" && idx === steps.length - 1)) return;
    const ni = dir === "up" ? idx - 1 : idx + 1;
    const u = [...steps];
    [u[idx], u[ni]] = [u[ni], u[idx]];
    u.forEach((s, i) => (s.order = i + 1));
    commit(u);
  };

  const handleFieldsChange = (order, fields) =>
    commit(steps.map(s => s.order === order ? { ...s, fields } : s));

  const getStepTypeStr = (v) => typeof v === "number" ? numberToStepType[v] || "Form" : v;

  const handleStepTypeChange = (order, str) =>
    updateStep(order, "stepType", stepTypeToNumber[str] || 1);

  return (
    <div className="wb-container">
      <div className="wb-header">
        <h4>{t("workflow_steps")}</h4>
        <button className="wb-add-step" onClick={addStep}>
          <Plus size={13} /> {t("add_step")}
        </button>
      </div>

      {steps.length === 0 && <div className="wb-empty">{t("no_steps")}</div>}

      <div className="wb-steps-list">
        {steps.map(step => (
          <div key={step.order} className="wb-step-card">
            <div className="wb-step-header">
              <div className="wb-step-order">{step.order}</div>
              <input
                value={step.title}
                onChange={e => updateStep(step.order, "title", e.target.value)}
                placeholder={t("step_title")}
                className="wb-step-title-input"
              />
              <div className="wb-step-actions">
                <button onClick={() => moveStep(step.order, "up")} title={t("move_up")}>
                  <MoveUp size={13} />
                </button>
                <button onClick={() => moveStep(step.order, "down")} title={t("move_down")}>
                  <MoveDown size={13} />
                </button>
                <button onClick={() => removeStep(step.order)} className="wb-danger" title={t("delete")}>
                  <Trash2 size={13} />
                </button>
              </div>
            </div>

            <div className="wb-step-details">
              <textarea
                value={step.description || ""}
                onChange={e => updateStep(step.order, "description", e.target.value)}
                placeholder={t("step_description")}
                rows="2"
              />
              <div className="wb-step-type">
                <span style={{ fontSize: 9, fontWeight: 800, color: "#6b7280", textTransform: "uppercase", letterSpacing: ".5px" }}>
                  {t("step_type")}
                </span>
                <div className="wb-select-wrap">
                  <select
                    value={getStepTypeStr(step.stepType)}
                    onChange={e => handleStepTypeChange(step.order, e.target.value)}
                  >
                    <option value="Form">Form (نموذج)</option>
                    <option value="Review">Review (مراجعة)</option>
                    <option value="Payment">Payment (دفع)</option>
                  </select>
                  <ChevronRight size={13} className="wb-select-arrow" />
                </div>

                <label className="wb-custom-checkbox">
                  <input
                    type="checkbox"
                    checked={step.isRequired}
                    onChange={e => updateStep(step.order, "isRequired", e.target.checked)}
                  />
                  <span className="wb-cb-box"><CheckCircle size={10} /></span>
                  <span className="wb-cb-text">{t("required_step")}</span>
                </label>
              </div>
            </div>

            {step.stepType === 1 && (
              <div className="wb-step-fields">
                <FormBuilder
                  fields={step.fields || []}
                  onChange={fields => handleFieldsChange(step.order, fields)}
                />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};

export default WorkflowBuilder;