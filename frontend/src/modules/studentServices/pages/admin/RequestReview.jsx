import React, { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStaffRequests } from "../../hooks/useStaffRequests";
import LoadingSpinner from "../../components/LoadingSpinner";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/admin/RequestReview.css";

const getLocalizedText = (text, lang) => { if (!text) return ""; if (typeof text === "string" && (text.startsWith("{") || text.includes("{"))) { try { const parsed = JSON.parse(text); if (lang === "ar") return parsed.ar || parsed.en || ""; return parsed.en || parsed.ar || ""; } catch { return text; } } return text; };

const RequestReview = () => {
  const { id } = useParams(); const { t, i18n } = useTranslation(); const { currentRequest, loading, getRequest, changeStatus, getAttachments, attachments, loadingAttachments } = useStaffRequests();
  const [comment, setComment] = useState(""); const [updating, setUpdating] = useState(false); const [selectedStatus, setSelectedStatus] = useState("");
  useEffect(() => { if (id) { getRequest(id); getAttachments(id); } }, [id]);
  const handleStatusChange = async () => { if (updating || !selectedStatus) return; setUpdating(true); try { await changeStatus(id, selectedStatus, comment); setComment(""); setSelectedStatus(""); } catch (err) { console.error(err); } finally { setUpdating(false); } };
  if (loading) return <LoadingSpinner />; if (!currentRequest) return <div className="rr-error">{t("request_not_found")}</div>;
  const localizedServiceName = getLocalizedText(currentRequest.serviceName, i18n.language); const localizedStudentName = getLocalizedText(currentRequest.studentName, i18n.language); const isFree = currentRequest.paymentStatus === "NotRequired" || currentRequest.paymentStatus === 1; const price = currentRequest.amountPaid || 0; const submittedDate = currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-";
  return (
    <div className="rr-container"><div className="rr-header"><h1>{t("request")} #{currentRequest.requestNumber}</h1></div>
      <div className="rr-layout"><div className="rr-left"><div className="rr-info-card"><h3>{t("service_details")}</h3><div><strong>{t("service_name")}:</strong> {localizedServiceName}</div></div>
      <div className="rr-info-card"><h3>{t("student_info")}</h3><div><strong>{t("student_name")}:</strong> {localizedStudentName}</div><div><strong>{t("student_code")}:</strong> {currentRequest.studentCode || "-"}</div><div><strong>{t("submitted_on")}:</strong> {submittedDate}</div></div>
      <div className="rr-info-card"><h3>{t("payment_info")}</h3><div><strong>{t("payment_status")}:</strong> <StatusBadge status={currentRequest.paymentStatus} /></div>{!isFree && <div><strong>{t("amount")}:</strong> ${price}</div>}</div>
      {currentRequest.submittedData && Object.keys(currentRequest.submittedData).length > 0 && (<div className="rr-info-card"><h3>{t("submitted_data")}</h3><pre className="rr-json">{JSON.stringify(currentRequest.submittedData, null, 2)}</pre></div>)}
      <div className="rr-info-card"><h3>{t("attachments")}</h3>{loadingAttachments ? <div>{t("loading")}</div> : attachments.length === 0 ? <div>{t("no_attachments")}</div> : (<ul className="rr-attachments-list">{attachments.map(att => (<li key={att.id}><a href={`/api/student-services/upload/attachment/${att.id}`} target="_blank" rel="noopener noreferrer">{att.fileName}</a><span className="rr-attachment-step">{att.stepKey}</span></li>))}</ul>)}</div></div>
      <div className="rr-right"><div className="rr-timeline-card"><RequestTimeline timeline={currentRequest.history} /></div><div className="rr-actions-card"><h3>{t("actions")}</h3><select value={selectedStatus} onChange={e => setSelectedStatus(e.target.value)}><option value="">{t("change_status")}</option><option value="UnderReview">{t("UnderReview")}</option><option value="Approved">{t("Approved")}</option><option value="Rejected">{t("Rejected")}</option><option value="Completed">{t("Completed")}</option><option value="MoreInfoRequired">{t("MoreInfoRequired")}</option></select><textarea className="rr-comment-textarea" placeholder={t("add_comment_optional")} value={comment} onChange={e => setComment(e.target.value)} rows="3" /><button className="rr-comment-btn" onClick={handleStatusChange} disabled={updating || !selectedStatus}>{updating ? t("updating") : t("update_status")}</button></div></div></div></div>
  );
};

export default RequestReview;