import { useEffect, useMemo, useRef, useState } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { AlertCircle, CreditCard, Wallet, Info, ClipboardList } from "lucide-react";
import {
  getServiceById,
  getStudentRequestById,
  getPendingRequestForService,
  getStudentRequestAttachments,
} from "../../studentServices/services/studentServicesService";
import {
  useCreateDraft, useSaveStep, useSubmitRequest, useProcessPayment,
} from "../hooks/useStudentRequests";
import { WORKFLOW_STEP_TYPE, STEP_FIELD_TYPE } from "../constants/workflowTypes";
import { REQUEST_STATUS } from "../constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import DynamicFormRenderer from "../components/DynamicFormRenderer";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import styles from "./RequestSubmission.module.css";

const RESUMABLE_STATUSES = [
  REQUEST_STATUS.Draft,
  REQUEST_STATUS.MoreInfoRequired,
  REQUEST_STATUS.PaymentPending,
];

/** SubmittedData is keyed by step Order (string) → { fieldId: value }. */
function parseSubmittedData(raw) {
  if (!raw) return {};
  let obj = raw;
  if (typeof raw === "string") {
    try { obj = JSON.parse(raw); } catch { return {}; }
  }
  if (!obj || typeof obj !== "object" || Array.isArray(obj)) return {};
  const out = {};
  for (const [key, val] of Object.entries(obj)) {
    if (val && typeof val === "object" && !Array.isArray(val)) out[key] = val;
  }
  return out;
}

function errMsg(err, fallback) {
  return err?.response?.data?.message || err?.response?.data?.title || err?.message || fallback;
}

function RequestSubmission() {
  const { id: serviceId } = useParams();
  const [searchParams] = useSearchParams();
  const resumeId = searchParams.get("resume");
  const navigate = useNavigate();
  const { t } = useTranslation();

  const createDraftMut = useCreateDraft();
  const saveStepMut = useSaveStep();
  const submitMut = useSubmitRequest();
  const payMut = useProcessPayment();

  const serviceQuery = useQuery({
    queryKey: ["portal", "services", "detail", String(serviceId)],
    enabled: Boolean(serviceId),
    queryFn: () => getServiceById(serviceId),
  });
  const service = serviceQuery.data;

  // boot: "loading" | "ready" | "blocked" (open non-editable request) | "error"
  const [bootState, setBootState] = useState("loading");
  const [bootError, setBootError] = useState(null);
  const [existingRequest, setExistingRequest] = useState(null);
  const [resumed, setResumed] = useState(false);

  const [requestId, setRequestId] = useState(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [stepValues, setStepValues] = useState({});          // { [stepOrder]: { [fieldId]: value } }
  const [attachmentsByField, setAttachmentsByField] = useState({}); // { [fieldId]: fileEntry[] }
  const [paymentMethod, setPaymentMethod] = useState("Card");
  const [busy, setBusy] = useState(false);
  const [wizError, setWizError] = useState(null);

  // ── wizard step model ─────────────────────────────────────────────────────
  const formSteps = useMemo(
    () =>
      (service?.workflow?.steps || [])
        .filter((s) => s.stepType === WORKFLOW_STEP_TYPE.Form)
        .sort((a, b) => (a.order ?? 0) - (b.order ?? 0)),
    [service]
  );

  const wizardSteps = useMemo(() => {
    if (!service?.workflow) return [];
    const declared = service.workflow.steps || [];
    const reviewDef = declared.find((s) => s.stepType === WORKFLOW_STEP_TYPE.Review);
    const paymentDef = declared.find((s) => s.stepType === WORKFLOW_STEP_TYPE.Payment);
    const steps = [
      ...formSteps.map((s) => ({ kind: "form", step: s, title: s.title })),
      {
        kind: "review",
        title: reviewDef?.title || t("portal_requests.review", { defaultValue: "Review" }),
      },
    ];
    if (service.isPaid) {
      steps.push({
        kind: "payment",
        title: paymentDef?.title || t("portal_requests.payment_step", { defaultValue: "Payment" }),
      });
    }
    return steps;
  }, [service, formSteps, t]);

  const paymentIndex = service?.isPaid ? formSteps.length + 1 : -1;
  const current = wizardSteps[stepIndex];

  // ── boot: resume existing draft or create one ─────────────────────────────
  const bootedRef = useRef(false);
  const [bootNonce, setBootNonce] = useState(0);

  useEffect(() => {
    if (!service || bootedRef.current) return;
    bootedRef.current = true;

    // Nothing to apply with — don't create a junk draft (render shows the
    // "no steps" empty state before bootState is ever consulted).
    if (!formSteps.length) return;

    const sameService = (req) =>
      String(req?.serviceId || "").toLowerCase() === String(serviceId).toLowerCase();

    const seedAttachments = async (req) => {
      // Best effort: map server attachments (keyed by step order) onto the
      // first File field of that step so review + validation see them.
      try {
        const list = await getStudentRequestAttachments(req.id);
        if (!Array.isArray(list) || !list.length) return;
        const seeded = {};
        for (const att of list) {
          const step = formSteps.find((s) => String(s.order) === String(att.stepKey));
          const fileField = step?.fields?.find((f) => f.fieldType === STEP_FIELD_TYPE.File);
          if (!fileField) continue;
          (seeded[fileField.id] ||= []).push({
            id: att.id,
            attachmentId: att.id,
            name: att.fileName,
            size: att.fileSize,
            uploaded: true,
            uploading: false,
            error: null,
          });
        }
        if (Object.keys(seeded).length) setAttachmentsByField((prev) => ({ ...seeded, ...prev }));
      } catch {
        // Attachments listing may not be available — resume without them.
      }
    };

    (async () => {
      setBootState("loading");
      setBootError(null);
      try {
        let req = null;
        if (resumeId) {
          try {
            const byId = await getStudentRequestById(resumeId);
            if (byId && sameService(byId)) req = byId;
          } catch {
            // Fall back to the pending lookup below.
          }
        }
        if (!req) {
          const pending = await getPendingRequestForService(serviceId);
          if (pending && pending.id) req = pending;
        }

        if (req) {
          if (!RESUMABLE_STATUSES.includes(req.status)) {
            setExistingRequest(req);
            setBootState("blocked");
            return;
          }
          setRequestId(req.id);
          const restored = parseSubmittedData(req.submittedData);
          if (Object.keys(restored).length) {
            setStepValues(restored);
            setResumed(true);
          }
          await seedAttachments(req);
          if (req.status === REQUEST_STATUS.PaymentPending && service.isPaid) {
            setStepIndex(formSteps.length + 1); // payment step
          } else {
            const firstUnfilled = formSteps.findIndex((s) => !restored[String(s.order)]);
            setStepIndex(firstUnfilled === -1 ? formSteps.length : firstUnfilled);
          }
        } else {
          const draft = await createDraftMut.mutateAsync(serviceId);
          setRequestId(draft.id);
        }
        setBootState("ready");
      } catch (err) {
        setBootState("error");
        setBootError(errMsg(err, t("portal_requests.boot_failed", { defaultValue: "Couldn't start your application." })));
      }
    })();
  }, [service, bootNonce]); // eslint-disable-line react-hooks/exhaustive-deps

  const retryBoot = () => {
    bootedRef.current = false;
    setBootNonce((n) => n + 1);
  };

  // ── helpers ───────────────────────────────────────────────────────────────
  const setFieldValue = (order, fieldId, val) => {
    setStepValues((prev) => ({
      ...prev,
      [String(order)]: { ...(prev[String(order)] || {}), [fieldId]: val },
    }));
  };

  const setFieldAttachments = (fieldId, files) => {
    setAttachmentsByField((prev) => ({ ...prev, [fieldId]: files }));
  };

  const uploadedFiles = (fieldId) =>
    (attachmentsByField[fieldId] || []).filter((f) => f.uploaded);

  const validateFormStep = (step) => {
    const vals = stepValues[String(step.order)] || {};
    for (const f of step.fields || []) {
      if (!f.isRequired) continue;
      if (f.fieldType === STEP_FIELD_TYPE.File) {
        if (!uploadedFiles(f.id).length) return false;
      } else if (f.fieldType === STEP_FIELD_TYPE.Checkbox) {
        if (!vals[f.id]) return false;
      } else if (f.fieldType === STEP_FIELD_TYPE.MultiSelect) {
        if (!Array.isArray(vals[f.id]) || !vals[f.id].length) return false;
      } else if (vals[f.id] == null || String(vals[f.id]).trim() === "") {
        return false;
      }
    }
    return true;
  };

  const buildStepPayload = (step) => {
    const payload = { ...(stepValues[String(step.order)] || {}) };
    for (const f of step.fields || []) {
      if (f.fieldType === STEP_FIELD_TYPE.File) {
        payload[f.id] = uploadedFiles(f.id).map((x) => x.name);
      }
    }
    return payload;
  };

  const anyFileUploading = Object.values(attachmentsByField).some((files) =>
    (files || []).some((f) => f.uploading)
  );

  // ── actions ───────────────────────────────────────────────────────────────
  const handleNext = async () => {
    if (busy || !current) return;
    setWizError(null);

    if (current.kind === "form" && !validateFormStep(current.step)) {
      setWizError(t("portal_requests.fill_required", { defaultValue: "Please complete all required fields before continuing." }));
      return;
    }

    setBusy(true);
    try {
      if (current.kind === "form") {
        const orderKey = String(current.step.order);
        // SubmittedData is a flat dict keyed by step order — send { order: values }.
        await saveStepMut.mutateAsync({
          requestId,
          stepKey: orderKey,
          data: { [orderKey]: buildStepPayload(current.step) },
        });
        setStepIndex((i) => i + 1);
      } else if (current.kind === "review") {
        const res = await submitMut.mutateAsync(requestId);
        if (res?.status === REQUEST_STATUS.PaymentPending && paymentIndex !== -1) {
          setStepIndex(paymentIndex);
        } else {
          navigate(`/student/requests/${requestId}`);
        }
      } else if (current.kind === "payment") {
        await payMut.mutateAsync({ requestId, method: paymentMethod });
        // Mock gateway pays instantly but the backend parks the request back in
        // Draft after payment — submit again so it lands in Pending.
        await submitMut.mutateAsync(requestId);
        navigate(`/student/requests/${requestId}`);
      }
    } catch (err) {
      // Keep the wizard mounted so entered data survives — show inline and let the user retry.
      setWizError(errMsg(err, t("portal_requests.error_generic", { defaultValue: "Something went wrong. Please try again." })));
    } finally {
      setBusy(false);
    }
  };

  const handlePrev = () => {
    if (busy || stepIndex === 0) return;
    setWizError(null);
    setStepIndex((i) => i - 1);
  };

  // ── render helpers ────────────────────────────────────────────────────────
  const displayValue = (field, vals) => {
    if (field.fieldType === STEP_FIELD_TYPE.File) {
      const names = uploadedFiles(field.id).map((f) => f.name);
      return names.length ? names.join(", ") : t("portal_requests.no_value", { defaultValue: "—" });
    }
    const v = vals[field.id];
    if (field.fieldType === STEP_FIELD_TYPE.Checkbox) {
      return v
        ? t("portal_requests.yes", { defaultValue: "Yes" })
        : t("portal_requests.no", { defaultValue: "No" });
    }
    if (Array.isArray(v)) return v.length ? v.join(", ") : t("portal_requests.no_value", { defaultValue: "—" });
    return v == null || String(v).trim() === "" ? t("portal_requests.no_value", { defaultValue: "—" }) : String(v);
  };

  const renderStepBody = () => {
    if (!current) return null;

    if (current.kind === "form") {
      return (
        <>
          {current.step.description && <p className={styles.stepDesc}>{current.step.description}</p>}
          <DynamicFormRenderer
            fields={current.step.fields || []}
            values={stepValues[String(current.step.order)] || {}}
            onChange={(fieldId, val) => setFieldValue(current.step.order, fieldId, val)}
            requestId={requestId}
            stepKey={String(current.step.order)}
            attachments={attachmentsByField}
            onAttachmentsChange={setFieldAttachments}
            disabled={busy}
          />
        </>
      );
    }

    if (current.kind === "review") {
      return (
        <div className={styles.review}>
          <p className={styles.stepDesc}>
            {t("portal_requests.review_hint", { defaultValue: "Review your information before submitting." })}
          </p>
          {formSteps.map((step) => {
            const vals = stepValues[String(step.order)] || {};
            return (
              <div key={step.order} className={styles.reviewStep}>
                <h4 className={styles.reviewStepTitle}>{step.title}</h4>
                <dl className={styles.reviewList}>
                  {(step.fields || []).map((f) => (
                    <div key={f.id} className={styles.reviewRow}>
                      <dt>{f.label}</dt>
                      <dd>{displayValue(f, vals)}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            );
          })}
          {service.isPaid && (
            <div className={styles.reviewFee}>
              <span>{t("portal_requests.amount_due", { defaultValue: "Amount due" })}</span>
              <strong>{fmtAmount(service.price)} EGP</strong>
            </div>
          )}
        </div>
      );
    }

    // payment
    return (
      <div className={styles.payment}>
        <div className={styles.paymentAmount}>
          <span>{t("portal_requests.amount_due", { defaultValue: "Amount due" })}</span>
          <strong>{fmtAmount(service.price)} EGP</strong>
        </div>
        <div className={styles.paymentMethods}>
          <span className={styles.paymentLabel}>
            {t("portal_requests.payment_method", { defaultValue: "Payment method" })}
          </span>
          <label className={`${styles.methodOption} ${paymentMethod === "Card" ? styles.methodActive : ""}`}>
            <input
              type="radio"
              name="payment-method"
              checked={paymentMethod === "Card"}
              onChange={() => setPaymentMethod("Card")}
            />
            <CreditCard size={16} />
            <span>{t("portal_requests.method_card", { defaultValue: "Credit / debit card" })}</span>
          </label>
          <label className={`${styles.methodOption} ${paymentMethod === "Wallet" ? styles.methodActive : ""}`}>
            <input
              type="radio"
              name="payment-method"
              checked={paymentMethod === "Wallet"}
              onChange={() => setPaymentMethod("Wallet")}
            />
            <Wallet size={16} />
            <span>{t("portal_requests.method_wallet", { defaultValue: "Mobile wallet" })}</span>
          </label>
        </div>
        <p className={styles.paymentNote}>
          <Info size={13} />
          {t("portal_requests.payment_hint", { defaultValue: "Test gateway — the payment completes instantly." })}
        </p>
      </div>
    );
  };

  const nextLabel = () => {
    if (busy) return t("portal_requests.processing", { defaultValue: "Processing…" });
    if (!current) return t("next", { defaultValue: "Next" });
    if (current.kind === "review") return t("submit", { defaultValue: "Submit" });
    if (current.kind === "payment") return t("portal_requests.pay_and_submit", { defaultValue: "Pay & submit" });
    return t("next", { defaultValue: "Next" });
  };

  // ── page states ───────────────────────────────────────────────────────────
  const shellProps = {
    title: service
      ? t("portal_requests.apply_for", { defaultValue: "Apply: {{service}}", service: service.name })
      : t("portal_requests.apply_title", { defaultValue: "Apply for a service" }),
    subtitle: t("portal_requests.wizard_subtitle", { defaultValue: "Complete the steps to submit your request" }),
    backTo: `/student/services/${serviceId}`,
  };

  if (service && (!service.workflow || !formSteps.length)) {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={ClipboardList}
          title={t("portal_requests.no_steps", { defaultValue: "This service has no application form configured yet." })}
        />
      </PortalPageShell>
    );
  }

  if (serviceQuery.isLoading || (service && bootState === "loading")) {
    return (
      <PortalPageShell {...shellProps}>
        <div className={styles.loading}>
          <PortalSkeleton variant="block" height={48} />
          <PortalSkeleton variant="block" height={260} />
        </div>
      </PortalPageShell>
    );
  }

  if (serviceQuery.isError || !service) {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_services.not_found", { defaultValue: "Service not found." })}
          onAction={() => serviceQuery.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      </PortalPageShell>
    );
  }

  if (bootState === "blocked" && existingRequest) {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={ClipboardList}
          title={t("portal_requests.open_request_exists", { defaultValue: "You already have an open request for this service." })}
          action={{
            to: `/student/requests/${existingRequest.id}`,
            label: t("portal_requests.go_to_request", { defaultValue: "Go to request" }),
          }}
        />
      </PortalPageShell>
    );
  }

  if (bootState === "error") {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={AlertCircle}
          title={bootError || t("portal_requests.boot_failed", { defaultValue: "Couldn't start your application." })}
          onAction={retryBoot}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      </PortalPageShell>
    );
  }

  return (
    <PortalPageShell {...shellProps}>
      {resumed && (
        <div className={styles.resumeNotice}>
          <Info size={15} />
          {t("portal_requests.resume_notice", { defaultValue: "We restored your saved draft." })}
        </div>
      )}

      <div className={styles.stepper}>
        {wizardSteps.map((s, idx) => (
          <div
            key={`${s.kind}-${idx}`}
            className={`${styles.stepChip} ${idx === stepIndex ? styles.stepActive : idx < stepIndex ? styles.stepDone : ""}`}
          >
            <span className={styles.stepNum}>{idx + 1}</span>
            <span className={styles.stepTitle}>{s.title}</span>
          </div>
        ))}
      </div>

      <PortalCard className={styles.body}>
        {wizError && (
          <div className={styles.errorBanner} role="alert">
            <AlertCircle size={15} />
            <span>{wizError}</span>
          </div>
        )}
        {renderStepBody()}
      </PortalCard>

      <div className={styles.actions}>
        {stepIndex > 0 && (
          <button type="button" className={styles.btnSecondary} onClick={handlePrev} disabled={busy}>
            {t("back", { defaultValue: "Back" })}
          </button>
        )}
        <button
          type="button"
          className={styles.btnPrimary}
          onClick={handleNext}
          disabled={busy || anyFileUploading}
        >
          {nextLabel()}
        </button>
      </div>
    </PortalPageShell>
  );
}

export default RequestSubmission;
