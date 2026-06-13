import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, Trash2, MoveUp, MoveDown, ChevronRight, CheckCircle } from "lucide-react";
import FormBuilder from "./FormBuilder";
import { WORKFLOW_STEP_TYPE, WORKFLOW_STEP_TYPE_NAMES } from "../../../core/constants/workflowTypes";
import "../styles/components/WorkflowBuilder.css";

const WorkflowBuilder = ({ workflow = { steps: [] }, onChange }) => {
  const { t } = useTranslation();
  const [steps, setSteps] = useState(workflow.steps || []);

  const commit = (updated) => { setSteps(updated); onChange({ steps: updated }); };

  const addStep = () => {
    const newStep = { order: steps.length + 1, title: t("new_step"), description: "", stepType: WORKFLOW_STEP_TYPE.Form, isRequired: true, fields: [] };
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

  const getStepTypeStr = (v) => typeof v === "number" ? WORKFLOW_STEP_TYPE_NAMES[v] || "Form" : v;

  const handleStepTypeChange = (order, str) =>
    updateStep(order, "stepType", WORKFLOW_STEP_TYPE[str] || WORKFLOW_STEP_TYPE.Form);

  return (
    <div className="wb-container">
      <div className="wb-header">
        <h4>{t("workflow_steps")}</h4>
        <button className="btn-primary" onClick={addStep}>
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
                <span className="wb-step-type-label">
                  {t("step_type")}
                </span>
                <div className="wb-select-wrap">
                  <select
                    value={getStepTypeStr(step.stepType)}
                    onChange={e => handleStepTypeChange(step.order, e.target.value)}
                  >
                    <option value="Form">{t("form")}</option>
                    <option value="Review">{t("review")}</option>
                    <option value="Payment">{t("payment")}</option>
                  </select>
                  <ChevronRight size={13} className="wb-select-arrow" />
                </div>

                <label className="wb-custom-checkbox">
                  <input
                    type="checkbox"
                    checked={step.isRequired}
                    onChange={e => updateStep(step.order, "isRequired", e.target.checked)}
                  />
                  {t("required_step")}
                </label>
              </div>

              {step.stepType === WORKFLOW_STEP_TYPE.Payment && (
                <div className="wb-price-field">
                  <label>{t("price")}</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={step.price ?? ""}
                    onChange={e => updateStep(step.order, "price", e.target.value ? parseFloat(e.target.value) : null)}
                  />
                </div>
              )}
            </div>

            {step.stepType === WORKFLOW_STEP_TYPE.Form && (
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