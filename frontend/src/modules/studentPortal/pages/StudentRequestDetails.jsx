import { useMemo, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, AlertCircle, Paperclip, Download, CreditCard, Loader2 } from "lucide-react";
import { getServiceById } from "../../studentServices/services/studentServicesService";
import {
  useStudentRequest,
  useRequestAttachments,
  useProcessPayment,
  useSubmitRequest,
  requestKeys,
} from "../hooks/useStudentRequests";
import { useFileUpload } from "../hooks/useFileUpload";
import { REQUEST_STATUS } from "../constants/requestStatus";
import { STEP_FIELD_TYPE } from "../constants/workflowTypes";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import { getLocalized } from "../../../core/utils/getLocalized";
import { RequestStatusBadge, PaymentStatusBadge } from "../components/RequestBadges";
import RequestTimeline from "../components/RequestTimeline";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { formatDateTime } from "../utils/format";
import styles from "./StudentRequestDetails.module.css";

function parseSubmittedData(raw) {
  if (!raw) return {};
  let obj = raw;
  if (typeof raw === "string") {
    try { obj = JSON.parse(raw); } catch { return {}; }
  }
  if (!obj || typeof obj !== "object" || Array.isArray(obj)) return {};
  return obj;
}

function formatValue(value, t) {
  if (value == null || value === "") return t("portal_requests.no_value", { defaultValue: "—" });
  if (typeof value === "boolean") {
    return value
      ? t("portal_requests.yes", { defaultValue: "Yes" })
      : t("portal_requests.no", { defaultValue: "No" });
  }
  if (Array.isArray(value)) {
    return value.length ? value.join(", ") : t("portal_requests.no_value", { defaultValue: "—" });
  }
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function formatFileSize(bytes) {
  if (!bytes) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function StudentRequestDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const qc = useQueryClient();

  const requestQuery = useStudentRequest(id);
  const req = requestQuery.data;

  const serviceQuery = useQuery({
    queryKey: ["portal", "services", "detail", String(req?.serviceId || "")],
    enabled: Boolean(req?.serviceId),
    queryFn: () => getServiceById(req.serviceId),
  });
  const workflowSteps = useMemo(
    () => serviceQuery.data?.workflow?.steps || [],
    [serviceQuery.data]
  );

  const attachmentsQuery = useRequestAttachments(id);
  const { download, downloading } = useFileUpload();
  const [downloadingId, setDownloadingId] = useState(null);
  const [downloadError, setDownloadError] = useState(null);

  const payMut = useProcessPayment();
  const submitMut = useSubmitRequest();
  const [paying, setPaying] = useState(false);
  const [payError, setPayError] = useState(null);

  const localizedServiceName = req?.serviceName ? getLocalized(req.serviceName, i18n.language) || req.serviceName : "";

  const submittedEntries = useMemo(() => {
    const data = parseSubmittedData(req?.submittedData);
    const orders = Object.keys(data).sort((a, b) => Number(a) - Number(b));
    return orders.map((order) => {
      const step = workflowSteps.find((s) => String(s.order) === order);
      const values = data[order];
      const rows = [];
      if (values && typeof values === "object" && !Array.isArray(values)) {
        for (const [fieldId, value] of Object.entries(values)) {
          const field = step?.fields?.find((f) => String(f.id).toLowerCase() === fieldId.toLowerCase());
          const label = field?.label ? (getLocalized(field.label, i18n.language) || field.label) : fieldId;
          rows.push({
            key: fieldId,
            label,
            value: formatValue(value, t),
            isFile: field?.fieldType === STEP_FIELD_TYPE.File,
          });
        }
      } else {
        rows.push({ key: order, label: order, value: formatValue(values, t), isFile: false });
      }
      const stepTitle = step?.title ? (getLocalized(step.title, i18n.language) || step.title) : t("portal_requests.step", { defaultValue: "Step {{order}}", order });
      return {
        order,
        title: stepTitle,
        rows,
      };
    });
  }, [req?.submittedData, workflowSteps, t, i18n.language]);

  const handleDownload = async (att) => {
    setDownloadError(null);
    setDownloadingId(att.id);
    try {
      await download(att.id, att.fileName || "file");
    } catch (err) {
      setDownloadError(err.message || t("portal_requests.download_failed", { defaultValue: "Download failed." }));
    } finally {
      setDownloadingId(null);
    }
  };

  const handlePayNow = async () => {
    if (paying) return;
    setPayError(null);
    setPaying(true);
    try {
      await payMut.mutateAsync({ requestId: req.id, method: "Card" });
      await submitMut.mutateAsync(req.id);
      await qc.invalidateQueries({ queryKey: requestKeys.detail(req.id) });
    } catch (err) {
      setPayError(err?.response?.data?.message || err.message || t("portal_requests.pay_failed", { defaultValue: "Payment failed. Please try again." }));
    } finally {
      setPaying(false);
    }
  };

  const shellProps = {
    title: req
      ? `${t("portal_requests.request", { defaultValue: "Request" })} #${req.requestNumber || String(req.id).slice(0, 8)}`
      : t("portal_requests.request", { defaultValue: "Request" }),
    backTo: "/student/requests",
  };

  if (requestQuery.isLoading || (!req && !requestQuery.isError)) {
    return (
      <PortalPageShell {...shellProps}>
        <div className={styles.loading}>
          <PortalSkeleton variant="block" height={64} />
          <PortalSkeleton variant="block" height={180} />
          <PortalSkeleton variant="block" height={120} />
        </div>
      </PortalPageShell>
    );
  }

  if (requestQuery.isError || !req) {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_requests.not_found", { defaultValue: "Request not found." })}
          onAction={() => requestQuery.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      </PortalPageShell>
    );
  }

  const needsInfo = req.status === REQUEST_STATUS.MoreInfoRequired;
  const needsPayment = req.status === REQUEST_STATUS.PaymentPending;
  const lastComment = [...(req.history || [])].reverse().find((h) => h.comment)?.comment;
  const attachments = attachmentsQuery.data || [];

  return (
    <PortalPageShell
      {...shellProps}
      actions={<RequestStatusBadge status={req.status} />}
    >
      {needsInfo && (
        <div className={styles.actionBanner}>
          <AlertTriangle size={18} />
          <div className={styles.bannerBody}>
            <strong>{t("portal_requests.action_required", { defaultValue: "Action required" })}</strong>
            <p>{lastComment || t("portal_requests.more_info_needed", { defaultValue: "Additional information is needed to continue processing your request." })}</p>
          </div>
          <button
            type="button"
            className={styles.bannerBtn}
            onClick={() => navigate(`/student/services/${req.serviceId}/apply?resume=${req.id}`)}
          >
            {t("portal_requests.respond", { defaultValue: "Update & resubmit" })}
          </button>
        </div>
      )}

      {needsPayment && (
        <div className={`${styles.actionBanner} ${styles.payBanner}`}>
          <CreditCard size={18} />
          <div className={styles.bannerBody}>
            <strong>{t("portal_requests.payment_due", { defaultValue: "Payment required" })}</strong>
            <p>
              {t("portal_requests.payment_due_hint", { defaultValue: "Complete the payment to submit your request." })}
              {req.servicePrice != null && <> — <strong>{fmtAmount(req.servicePrice)} EGP</strong></>}
            </p>
            {payError && <p className={styles.bannerError}>{payError}</p>}
          </div>
          <button type="button" className={styles.bannerBtn} onClick={handlePayNow} disabled={paying}>
            {paying
              ? t("portal_requests.processing", { defaultValue: "Processing…" })
              : t("portal_requests.pay_now", { defaultValue: "Pay now" })}
          </button>
        </div>
      )}

      <div className={styles.grid}>
        <div className={styles.main}>
          <PortalCard className={styles.card}>
            <h3 className={styles.cardTitle}>{t("portal_requests.service", { defaultValue: "Service" })}</h3>
            <p className={styles.cardText}>{localizedServiceName}</p>
            <div className={styles.metaRow}>
              <span className={styles.metaItem}>
                {t("portal_requests.submitted_on", { defaultValue: "Submitted on" })}:{" "}
                {req.submittedAt ? formatDateTime(req.submittedAt, i18n.language) : t("portal_requests.no_value", { defaultValue: "—" })}
              </span>
              <span className={styles.metaItem}>
                {t("portal_requests.payment", { defaultValue: "Payment" })}: <PaymentStatusBadge status={req.paymentStatus} />
                {req.amountPaid != null && req.amountPaid > 0 && (
                  <span className={styles.amount}>{fmtAmount(req.amountPaid)} EGP</span>
                )}
              </span>
            </div>
          </PortalCard>

          {submittedEntries.length > 0 && (
            <PortalCard className={styles.card}>
              <h3 className={styles.cardTitle}>{t("portal_requests.submitted_data", { defaultValue: "Submitted information" })}</h3>
              {serviceQuery.isLoading && <PortalSkeleton.Lines count={3} />}
              {!serviceQuery.isLoading && submittedEntries.map((entry) => (
                <div key={entry.order} className={styles.dataStep}>
                  <h4 className={styles.dataStepTitle}>{entry.title}</h4>
                  <dl className={styles.dataList}>
                    {entry.rows.map((row) => (
                      <div key={row.key} className={styles.dataRow}>
                        <dt>{row.label}</dt>
                        <dd>{row.value}</dd>
                      </div>
                    ))}
                  </dl>
                </div>
              ))}
            </PortalCard>
          )}

          <PortalCard className={styles.card}>
            <h3 className={styles.cardTitle}>
              <Paperclip size={14} /> {t("portal_requests.attachments", { defaultValue: "Attachments" })}
            </h3>
            {attachmentsQuery.isLoading ? (
              <PortalSkeleton.Lines count={2} />
            ) : attachments.length === 0 ? (
              <p className={styles.cardText}>{t("portal_requests.no_attachments", { defaultValue: "No files attached." })}</p>
            ) : (
              <ul className={styles.attachmentList}>
                {attachments.map((att) => (
                  <li key={att.id} className={styles.attachmentItem}>
                    <span className={styles.attachmentName}>{att.fileName}</span>
                    <span className={styles.attachmentSize}>{formatFileSize(att.fileSize)}</span>
                    <button
                      type="button"
                      className={styles.downloadBtn}
                      onClick={() => handleDownload(att)}
                      disabled={downloading && downloadingId === att.id}
                      title={t("portal_requests.download", { defaultValue: "Download" })}
                    >
                      {downloadingId === att.id ? <Loader2 size={14} className={styles.spin} /> : <Download size={14} />}
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {downloadError && <p className={styles.inlineError}>{downloadError}</p>}
          </PortalCard>
        </div>

        <PortalCard className={styles.side}>
          <RequestTimeline timeline={req.history} />
        </PortalCard>
      </div>
    </PortalPageShell>
  );
}

export default StudentRequestDetails;