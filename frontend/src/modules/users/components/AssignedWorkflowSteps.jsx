import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ChevronDown, ChevronUp, ExternalLink } from "lucide-react";
import { WORKFLOW_STEP_TYPE_NAMES } from "../../../core/constants/workflowTypes";
import "./assignedWorkflowSteps.css";

export default function AssignedWorkflowSteps({ item, onViewRequest }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const steps = item.workflowSteps;
  const currentStep = item.currentStepOrder;

  return (
    <div className="sawf-card">
      <button className="sawf-card-header" onClick={() => setOpen(o => !o)}>
        <div className="sawf-card-summary">
          <span className="sawf-service">{item.serviceName || "—"}</span>
          <span className="sawf-student">{item.studentName || "—"}</span>
        </div>
        <div className="sawf-card-meta">
          {item.requestNumber > 0 && (
            <span className="sawf-pill neutral sawf-req-num">#{item.requestNumber}</span>
          )}
          <span className={`sawf-pill ${getStatusClass(item.status)}`}>
            {statusLabel(item.status)}
          </span>
          {item.createdAt && (
            <span className="sawf-date">{new Date(item.createdAt).toLocaleDateString()}</span>
          )}
        </div>
        <div className="sawf-card-chevron">
          {open ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
        </div>
      </button>

      {open && steps && steps.length > 0 && (
        <div className="sawf-steps">
          <div className="sawf-steps-header">
            <span>{t("workflow_steps")}</span>
          </div>
          {steps.map((step) => {
            const isCurrent = step.order === currentStep;
            const isPast = step.order < currentStep;
            return (
              <div key={step.order} className={`sawf-step-row ${isCurrent ? "is-current" : ""} ${isPast ? "is-done" : ""}`}>
                <span className="sawf-step-order">{step.order}</span>
                <div className="sawf-step-body">
                  <span className="sawf-step-title">{step.title || `Step ${step.order}`}</span>
                  <div className="sawf-step-meta">
                    <span className="sawf-step-type">{WORKFLOW_STEP_TYPE_NAMES[step.stepType] || "—"}</span>
                    {isCurrent && <span className="sawf-pill sm info">{t("current")}</span>}
                    {isPast && <span className="sawf-step-date sawf-step-done">&check; {t("completed")}</span>}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {onViewRequest && (
        <div className="sawf-card-actions">
          <button className="sawf-action-btn" onClick={() => onViewRequest(item)}>
            <ExternalLink size={11} /> {t("view_request")}
          </button>
        </div>
      )}
    </div>
  );
}

function statusLabel(status) {
  const MAP = { 2: "Pending", 3: "Under Review", 4: "More Info", 5: "Approved", 6: "Rejected", 7: "Payment", 8: "Completed", 9: "Cancelled", 10: "Ready" };
  return MAP[status] || status;
}

function getStatusClass(status) {
  const MAP = { 2: "warn", 3: "info", 4: "warn", 5: "good", 6: "bad", 7: "info", 8: "good", 9: "bad", 10: "good" };
  return MAP[status] || "";
}
