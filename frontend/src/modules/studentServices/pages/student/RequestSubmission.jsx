import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import { useFileUpload } from "../../hooks/useFileUpload";
import { getServiceById } from "../../services/studentServicesService";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import DynamicFormRenderer from "../../components/DynamicFormRenderer";
import FileUploader from "../../components/FileUploader";
import "../../styles/student/RequestSubmission.css";

const RequestSubmission = () => {
  const { id: serviceId } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { createDraft, saveStep, submit, pay, getPendingForService } = useStudentRequests();
  const { upload } = useFileUpload();

  const [service, setService] = useState(null);
  const [requestId, setRequestId] = useState(null);
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [stepData, setStepData] = useState({});
  const [attachments, setAttachments] = useState({});
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [paymentCompleted, setPaymentCompleted] = useState(false);
  const [fieldsMap, setFieldsMap] = useState({});

  useEffect(() => {
    const init = async () => {
      setLoading(true);
      try {
        const srv = await getServiceById(serviceId);
        setService(srv);
        const map = {};
        srv.workflow?.steps?.forEach(step => {
          step.fields?.forEach(field => { map[field.id] = field.label; });
        });
        setFieldsMap(map);
        const pending = await getPendingForService(serviceId);
        if (pending && (pending.status === "Draft" || pending.status === "PaymentPending")) {
          setRequestId(pending.id);
          setCurrentStepIndex(pending.currentStepOrder || 0);
          setStepData(pending.submittedData || {});
        } else {
          const draft = await createDraft(serviceId);
          setRequestId(draft.id);
          setCurrentStepIndex(0);
          setStepData({});
          setAttachments({});
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    init();
  }, [serviceId]);

  const workflow = service?.workflow;
  const steps = workflow?.steps || [];
  const currentStep = steps[currentStepIndex];
  const isLastStep = currentStepIndex === steps.length - 1;

  const isStepValid = () => {
    if (!currentStep) return false;
    const stepType = currentStep.stepType;
    const stepOrder = currentStep.order?.toString() || "1";

    if (stepType === 1 || stepType === "Form") {
      const fields = currentStep.fields || [];
      for (const field of fields) {
        if (field.isRequired) {
          let value;
          if (field.fieldType === 7 || field.fieldType === "File") {
            const fileList = stepData[field.id];
            value = fileList && fileList.length > 0 && fileList[0]?.uploaded;
          } else {
            value = stepData[field.id];
          }
          if (value === undefined || value === null || (typeof value === "string" && value.trim() === "")) {
            setError(`${field.label} ${t("is_required")}`);
            return false;
          }
        }
      }
      return true;
    }
    if (stepType === 2 || stepType === "Review") return true;
    if (stepType === 3 || stepType === "Payment") return true;
    return true;
  };

  const handleNext = async () => {
    setError(null);
    if (!isStepValid()) return;
    setSubmitting(true);
    try {
      let localRequestId = requestId;
      if (!localRequestId) {
        const draft = await createDraft(serviceId);
        localRequestId = draft.id;
        setRequestId(localRequestId);
      }

      const stepType = currentStep.stepType;
      const stepOrder = currentStep.order?.toString() || "1";

      if (stepType === 1 || stepType === "Form") {
        // Handle file uploads inside form fields
        const formDataToSave = { ...stepData };
        const fieldsWithFiles = (currentStep.fields || []).filter(f => f.fieldType === 7 || f.fieldType === "File");
        for (const field of fieldsWithFiles) {
          const fileList = stepData[field.id];
          if (fileList && Array.isArray(fileList) && fileList.length > 0) {
            const uploadedIds = [];
            for (const fileItem of fileList) {
              if (fileItem.file && !fileItem.attachmentId && !fileItem.uploaded) {
                const result = await upload(localRequestId, stepOrder, fileItem.file);
                uploadedIds.push(result.attachmentId);
              } else if (fileItem.attachmentId) {
                uploadedIds.push(fileItem.attachmentId);
              }
            }
            formDataToSave[field.id] = uploadedIds;
          }
        }
        await saveStep(localRequestId, stepOrder, formDataToSave);
      }

      if (isLastStep) {
        if (service?.isPaid && !paymentCompleted) {
          const paymentResult = await pay(localRequestId, "CreditCard");
          if (paymentResult.paymentStatus !== "Paid") {
            setError(t("payment_failed"));
            return;
          }
          setPaymentCompleted(true);
        }
        const submitted = await submit(localRequestId);
        navigate(`/student/requests/${submitted.id}`);
      } else {
        setCurrentStepIndex(prev => prev + 1);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const handlePrev = () => {
    if (currentStepIndex > 0) setCurrentStepIndex(prev => prev - 1);
    setError(null);
  };

  const handlePayNow = async () => {
    setError(null);
    setSubmitting(true);
    try {
      const localRequestId = requestId;
      if (!localRequestId) { setError(t("no_request_id")); return; }
      const paymentResult = await pay(localRequestId, "CreditCard");
      if (paymentResult.paymentStatus === "Paid") {
        setPaymentCompleted(true);
        const submitted = await submit(localRequestId);
        navigate(`/student/requests/${submitted.id}`);
      } else {
        setError(t("payment_failed"));
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const renderStepContent = () => {
    if (!currentStep) return null;
    const stepType = currentStep.stepType;
    const fields = currentStep.fields || [];
    const stepOrder = currentStep.order?.toString() || "1";

    if (stepType === 1 || stepType === "Form") {
      return <DynamicFormRenderer fields={fields} value={stepData} onChange={setStepData} requestId={requestId} />;
    } else if (stepType === 2 || stepType === "Review") {
      return (
        <div className="review-box">
          <h4>{t("review_step_message")}</h4>
          <div className="review-data">
            {Object.entries(stepData).map(([key, value]) => (
              <div key={key} className="review-row">
                <strong>{fieldsMap[key] || key}:</strong>
                <span>{typeof value === "object" ? JSON.stringify(value) : String(value)}</span>
              </div>
            ))}
          </div>
        </div>
      );
    } else if (stepType === 3 || stepType === "Payment") {
      const requiredAmount = service?.price || 0;
      return (
        <div className="payment-box">
          <p>{t("amount")}: ${requiredAmount}</p>
          {!paymentCompleted ? (
            <button onClick={handlePayNow} disabled={submitting} className="pay-now-btn">
              {submitting ? t("processing") : t("pay_now")}
            </button>
          ) : (
            <p className="payment-success">{t("payment_successful")}</p>
          )}
        </div>
      );
    }
    return <div>{currentStep.description}</div>;
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <div className="rs-error">{error}</div>;
  if (!service) return <div>{t("service_not_found")}</div>;

  const isPaymentStep = currentStep?.stepType === 3 || currentStep?.stepType === "Payment";
  const canProceed = isPaymentStep ? paymentCompleted : true;

  return (
    <div className="rs-container">
      <h1>{t("apply_for")} {service.name}</h1>
      <div className="rs-steps">
        {steps.map((step, idx) => (
          <div key={step.order || idx} className={`rs-step ${idx === currentStepIndex ? "active" : idx < currentStepIndex ? "completed" : ""}`}>
            <span className="rs-step-number">{idx + 1}</span>
            <span className="rs-step-title">{step.title}</span>
          </div>
        ))}
      </div>
      <div className="rs-content">{renderStepContent()}</div>
      <div className="rs-actions">
        {currentStepIndex > 0 && (
          <button onClick={handlePrev} className="rs-btn-secondary" disabled={submitting}>
            {t("back")}
          </button>
        )}
        <button onClick={handleNext} className="rs-btn-primary" disabled={submitting || !canProceed}>
          {isLastStep ? (submitting ? t("submitting") : t("submit")) : t("next")}
        </button>
      </div>
    </div>
  );
};

export default RequestSubmission;