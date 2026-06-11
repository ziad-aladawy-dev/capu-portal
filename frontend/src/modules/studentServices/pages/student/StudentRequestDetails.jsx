import React, { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import { getStudentRequestAttachments } from "../../services/studentServicesService";
import LoadingSpinner from "../../components/LoadingSpinner";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/student/StudentRequestDetails.css";

const StudentRequestDetails = () => {
  const { id } = useParams();
  const { t, i18n } = useTranslation();
  const { currentRequest, loading, getRequest } = useStudentRequests();
  const [attachments, setAttachments] = useState([]);
  const [loadingAttachments, setLoadingAttachments] = useState(false);

  useEffect(() => {
    const load = async () => {
      await getRequest(id);
      await loadAttachments();
    };
    load();
  }, [id]);

  const loadAttachments = async () => {
    setLoadingAttachments(true);
    try {
      const data = await getStudentRequestAttachments(id);
      setAttachments(data);
    } catch (err) { console.error(err); }
    finally { setLoadingAttachments(false); }
  };

  const getLocalizedText = (text) => {
    if (!text) return "";
    try {
      const parsed = JSON.parse(text);
      return i18n.language === "ar" ? parsed.ar || parsed.en : parsed.en || parsed.ar;
    } catch { return text; }
  };

  if (loading) return <LoadingSpinner />;
  if (!currentRequest) return <div className="srd-error">{t("request_not_found")}</div>;

  const requiredAmount = currentRequest.servicePrice || 0;
  const isPaid = currentRequest.paymentStatus === "Paid" || currentRequest.paymentStatus === 3;
  const amountPaid = currentRequest.amountPaid || 0;
  const remainingAmount = requiredAmount - amountPaid;

  return (
    <div className="srd-container">
      <div className="srd-header-blue">
        <div className="srd-header-icon"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M20 7H4C2.9 7 2 7.9 2 9V19C2 20.1 2.9 21 4 21H20C21.1 21 22 20.1 22 19V9C22 7.9 21.1 7 20 7Z" stroke="currentColor" strokeWidth="1.5" fill="none"/><path d="M16 21V5C16 3.9 15.1 3 14 3H10C8.9 3 8 3.9 8 5V21" stroke="currentColor" strokeWidth="1.5" fill="none"/></svg></div>
        <div><span className="srd-header-kicker">{t("student_portal")}</span><h1>{t("request")} #{currentRequest.requestNumber}</h1><p>{t("request_details")}</p></div>
      </div>
      <div className="srd-layout">
        <div className="srd-info">
          <div className="srd-card"><h3>{t("service")}</h3><p>{getLocalizedText(currentRequest.serviceName)}</p></div>
          <div className="srd-card"><h3>{t("submitted_on")}</h3><p>{currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-"}</p></div>
          <div className="srd-card"><h3>{t("payment_info")}</h3>
            <div className="srd-payment-row"><span>{t("payment_status")}:</span> <StatusBadge status={currentRequest.paymentStatus} /></div>
            {requiredAmount > 0 && (
              <>
                <div className="srd-payment-row"><span>{t("required_amount")}:</span> <strong>${requiredAmount}</strong></div>
                {!isPaid && remainingAmount > 0 && <div className="srd-payment-row srd-remaining"><span>{t("remaining_amount")}:</span> <strong>${remainingAmount}</strong></div>}
                {amountPaid > 0 && <div className="srd-payment-row"><span>{t("amount_paid")}:</span> <strong>${amountPaid}</strong></div>}
              </>
            )}
          </div>
          {currentRequest.submittedData && Object.keys(currentRequest.submittedData).length > 0 && (
            <div className="srd-card"><h3>{t("submitted_data")}</h3><div className="srd-submitted-data">{Object.entries(currentRequest.submittedData).map(([key, value]) => (<div key={key} className="srd-data-row"><strong>{key}:</strong><span>{typeof value === "object" ? JSON.stringify(value) : String(value)}</span></div>))}</div></div>
          )}
          <div className="srd-card"><h3>{t("attachments")}</h3>{loadingAttachments ? <div>{t("loading")}</div> : attachments.length === 0 ? <div>{t("no_attachments")}</div> : (<ul className="srd-attachments-list">{attachments.map(att => (<li key={att.id}><a href={`/api/student-services/upload/attachment/${att.id}`} target="_blank" rel="noopener noreferrer">{att.fileName}</a><span className="srd-attachment-step">{att.stepKey}</span></li>))}</ul>)}</div>
        </div>
        <div className="srd-timeline"><RequestTimeline timeline={currentRequest.history} /></div>
      </div>
    </div>
  );
};

export default StudentRequestDetails;