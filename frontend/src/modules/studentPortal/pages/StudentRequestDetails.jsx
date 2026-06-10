import { useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AlertTriangle } from "lucide-react";
import { useStudentRequests } from "../hooks/useStudentRequests";
import LoadingSpinner from "../../studentServices/components/LoadingSpinner";
import RequestTimeline from "../components/RequestTimeline";
import StatusBadge from "../components/StatusBadge";
import { REQUEST_STATUS } from "../constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import "../styles/StudentRequestDetails.css";

const StudentRequestDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { currentRequest, loading, getRequest } = useStudentRequests();

  useEffect(() => {
    getRequest(id);
  }, [id]);

  if (loading) return <LoadingSpinner />;
  if (!currentRequest) return <div className="srd-error">{t("request_not_found", { defaultValue: "Request not found." })}</div>;

  const req = currentRequest;
  const needsInfo = req.status === REQUEST_STATUS.MoreInfoRequired;
  // Latest staff note explaining what is needed.
  const lastComment = [...(req.history || [])].reverse().find((h) => h.comment)?.comment;

  return (
    <div className="srd-container">
      <button className="srd-back" onClick={() => navigate("/student/requests")}>
        ← {t("back_to_requests", { defaultValue: "Back to Requests" })}
      </button>
      <div className="srd-header">
        <h1>{t("request", { defaultValue: "Request" })} #{String(req.id).slice(0, 8)}</h1>
        <StatusBadge status={req.status} type="request" />
      </div>

      {needsInfo && (
        <div className="srd-action-banner">
          <AlertTriangle size={20} />
          <div className="srd-action-body">
            <strong>{t("action_required", { defaultValue: "Action required" })}</strong>
            <p>{lastComment || t("more_info_needed", { defaultValue: "Additional information is needed to continue processing your request." })}</p>
          </div>
          <button className="srd-action-btn" onClick={() => navigate(`/student/services/${req.serviceId}/apply`)}>
            {t("respond", { defaultValue: "Respond" })}
          </button>
        </div>
      )}

      <div className="srd-content">
        <div className="srd-info">
          <div className="srd-card">
            <h3>{t("service", { defaultValue: "Service" })}</h3>
            <p>{req.serviceName}</p>
          </div>
          <div className="srd-card">
            <h3>{t("submitted_on", { defaultValue: "Submitted on" })}</h3>
            <p>{req.submittedAt ? new Date(req.submittedAt).toLocaleString() : "-"}</p>
          </div>
          <div className="srd-card">
            <h3>{t("payment", { defaultValue: "Payment" })}</h3>
            <p className="srd-payment">
              <StatusBadge status={req.paymentStatus} type="payment" />
              {req.amountPaid ? <span className="srd-amount">{fmtAmount(req.amountPaid)} EGP</span> : null}
            </p>
          </div>
          {req.submittedData && Object.keys(req.submittedData).length > 0 && (
            <div className="srd-card">
              <h3>{t("submitted_data", { defaultValue: "Submitted Data" })}</h3>
              <pre>{JSON.stringify(req.submittedData, null, 2)}</pre>
            </div>
          )}
        </div>
        <div className="srd-timeline">
          <RequestTimeline timeline={req.history} />
        </div>
      </div>
    </div>
  );
};

export default StudentRequestDetails;
