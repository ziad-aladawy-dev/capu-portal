import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import { useFileUpload } from "../../hooks/useFileUpload";
import { getServiceById } from "../../services/studentServicesService";
import DynamicFormRenderer from "../../components/DynamicFormRenderer";
import FileUploader from "../../components/FileUploader";
import LoadingSpinner from "../../components/LoadingSpinner";
import "../../styles/student/RequestSubmission.css";

const RequestSubmission = () => {
  const { id: serviceId } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { createDraft, saveStep, submit, pay } = useStudentRequests();
  const { upload } = useFileUpload();
  
  const [service, setService] = useState(null);
  const [requestId, setRequestId] = useState(null);
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [stepData, setStepData] = useState({});
  const [attachments, setAttachments] = useState({});
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const workflow = service?.workflow;
  const steps = workflow?.steps || [];
  const currentStep = steps[currentStepIndex];
  const isLastStep = currentStepIndex === steps.length - 1;

  useEffect(() => {
    loadService();
  }, [serviceId]);

  const loadService = async () => {
    try {
      const data = await getServiceById(serviceId);
      setService(data);
    } catch (err) {
      console.error(err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleNext = async () => {
    setError(null);
    try {
      if (!requestId) {
        const draft = await createDraft(serviceId);
        setRequestId(draft.id);
      }
      if (currentStep.stepType === "Form") {
        await saveStep(requestId, currentStep.order.toString(), stepData);
      } else if (currentStep.stepType === "FileUpload") {
        const files = attachments[currentStep.order] || [];
        for (const file of files) {
          await upload(requestId, currentStep.order.toString(), file);
        }
      } else if (currentStep.stepType === "Payment" && service.isPaid) {
        await pay(requestId, "CreditCard");
      }
      if (isLastStep) {
        await submit(requestId);
        navigate(`/student/requests/${requestId}`);
      } else {
        setCurrentStepIndex(prev => prev + 1);
      }
    } catch (err) {
      console.error(err);
      setError(err.message);
    }
  };

  const handlePrev = () => {
    setError(null);
    setCurrentStepIndex(prev => prev - 1);
  };

  const renderStepContent = () => {
    if (!currentStep) return null;
    switch (currentStep.stepType) {
      case "Form":
        return (
          <DynamicFormRenderer
            fields={currentStep.fields || []}
            value={stepData}
            onChange={setStepData}
          />
        );
      case "FileUpload":
        return (
          <FileUploader
            requestId={requestId}
            stepKey={currentStep.order.toString()}
            value={attachments[currentStep.order] || []}
            onChange={(files) => setAttachments(prev => ({ ...prev, [currentStep.order]: files }))}
          />
        );
      case "Review":
        return <div className="review-box">{t("review_step_message")}</div>;
      case "Payment":
        return (
          <div className="payment-box">
            <p>{t("service_price")}: ${service?.price}</p>
            <p>{t("payment_instruction")}</p>
          </div>
        );
      default:
        return <div>{currentStep.description}</div>;
    }
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <div className="rs-error">{error}</div>;
  if (!service) return <div>{t("service_not_found")}</div>;

  return (
    <div className="rs-container">
      <h1>{t("apply_for")} {service.name}</h1>
      <div className="rs-steps">
        {steps.map((step, idx) => (
          <div key={step.order} className={`rs-step ${idx === currentStepIndex ? "active" : idx < currentStepIndex ? "completed" : ""}`}>
            <span className="rs-step-number">{idx + 1}</span>
            <span className="rs-step-title">{step.title}</span>
          </div>
        ))}
      </div>
      <div className="rs-content">
        {renderStepContent()}
      </div>
      <div className="rs-actions">
        {currentStepIndex > 0 && (
          <button onClick={handlePrev} className="rs-btn-secondary" disabled={submitting}>
            {t("back")}
          </button>
        )}
        <button onClick={handleNext} className="rs-btn-primary" disabled={submitting}>
          {isLastStep ? (submitting ? t("submitting") : t("submit")) : t("next")}
        </button>
      </div>
    </div>
  );
};

export default RequestSubmission;