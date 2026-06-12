import { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Download, UserCheck } from "lucide-react";
import { useStaffRequests } from "../../hooks/useStaffRequests";
import { useFileUpload } from "../../hooks/useFileUpload";
import { usePermission } from "../../../../core/auth/usePermission";
import { fetchAllStaff } from "../../../../core/services/staffService";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import { REQUEST_STATUS, PAYMENT_STATUS } from "../../../../core/constants/requestStatus";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import "../../styles/admin/RequestReview.css";

// Mirror of backend StudentRequestService.IsValidTransition — only offer
// transitions the API will accept (enums travel as ints, no string names).
const VALID_NEXT = {
  [REQUEST_STATUS.Draft]: [REQUEST_STATUS.Pending, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.Pending]: [REQUEST_STATUS.UnderReview, REQUEST_STATUS.Rejected, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.UnderReview]: [REQUEST_STATUS.Approved, REQUEST_STATUS.MoreInfoRequired, REQUEST_STATUS.Rejected, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.MoreInfoRequired]: [REQUEST_STATUS.Pending, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.Approved]: [REQUEST_STATUS.ReadyForPickup, REQUEST_STATUS.Completed, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.PaymentPending]: [REQUEST_STATUS.Approved, REQUEST_STATUS.Completed, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.ReadyForPickup]: [REQUEST_STATUS.Completed, REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.Completed]: [REQUEST_STATUS.Cancelled],
  [REQUEST_STATUS.Cancelled]: [],
};

const STATUS_NAMES = Object.fromEntries(
  Object.entries(REQUEST_STATUS).map(([name, value]) => [value, name])
);

// Assignment is only accepted by the backend for Pending / UnderReview.
const ASSIGNABLE_STATUSES = [REQUEST_STATUS.Pending, REQUEST_STATUS.UnderReview];

const RequestReview = () => {
  const { id } = useParams();
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const { currentRequest, loading, error, getRequest, changeStatus, assign, getAttachments, attachments, loadingAttachments } = useStaffRequests();
  const { download, downloading } = useFileUpload();
  const [comment, setComment] = useState("");
  const [updating, setUpdating] = useState(false);
  const [selectedStatus, setSelectedStatus] = useState("");
  const [staffList, setStaffList] = useState([]);
  const [selectedStaffId, setSelectedStaffId] = useState("");
  const [assigning, setAssigning] = useState(false);

  const canAssign = can("student-services.requests.assign");
  const status = Number(currentRequest?.status);
  const assignable = canAssign && ASSIGNABLE_STATUSES.includes(status);

  useEffect(() => { if (id) { getRequest(id); getAttachments(id); } }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!canAssign) return;
    let cancelled = false;
    fetchAllStaff()
      .then((data) => {
        if (cancelled) return;
        const items = data?.items || data || [];
        setStaffList(Array.isArray(items) ? items : []);
      })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [canAssign]);

  const handleStatusChange = async () => {
    if (updating || !selectedStatus) return;
    setUpdating(true);
    try {
      await changeStatus(id, Number(selectedStatus), comment);
      setComment("");
      setSelectedStatus("");
    } catch (err) {
      console.error(err);
    } finally {
      setUpdating(false);
    }
  };

  const handleAssign = async () => {
    if (assigning || !selectedStaffId) return;
    setAssigning(true);
    try {
      await assign(id, selectedStaffId);
      setSelectedStaffId("");
    } catch (err) {
      console.error(err);
    } finally {
      setAssigning(false);
    }
  };

  // `loading` starts false in the hook; without the !error guard the page
  // flashes "request_not_found" before the first fetch resolves.
  if (loading || (!currentRequest && !error)) return <LoadingSpinner />;
  if (!currentRequest) return <ErrorMessage message={t("request_not_found")} />;

  const localizedServiceName = getLocalized(currentRequest.serviceName, i18n.language);
  const localizedStudentName = getLocalized(currentRequest.studentName, i18n.language);
  const isFree = Number(currentRequest.paymentStatus) === PAYMENT_STATUS.NotRequired;
  const price = currentRequest.amountPaid || 0;
  const submittedDate = currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-";
  const nextStatuses = VALID_NEXT[status] || [];

  // Last "Assigned" history entry carries the staff id (the DTO itself has no
  // assignee field).
  const lastAssignEntry = [...(currentRequest.history || [])].reverse().find((h) => h.action === "Assigned");
  const assigneeId = lastAssignEntry?.performedByUserId || null;
  const assigneeStaff = assigneeId ? staffList.find((s) => s.id === assigneeId) : null;
  const assigneeName = assigneeStaff
    ? getLocalized(assigneeStaff.name, i18n.language) || assigneeStaff.employeeCode
    : assigneeId;

  const statusLabel = (value) => {
    const name = STATUS_NAMES[value] || String(value);
    return t(name, { defaultValue: name.replace(/([A-Z])/g, " $1").trim() });
  };

  return (
    <div className="rr-container">
      <div className="rr-header"><h1>{t("request")} #{currentRequest.requestNumber}</h1></div>
      <div className="rr-layout">
        <div className="rr-left">
          <div className="rr-info-card"><h3>{t("service_details")}</h3><div><strong>{t("service_name")}:</strong> {localizedServiceName}</div></div>
          <div className="rr-info-card"><h3>{t("student_info")}</h3><div><strong>{t("student_name")}:</strong> {localizedStudentName}</div><div><strong>{t("student_code")}:</strong> {currentRequest.studentCode || "-"}</div><div><strong>{t("submitted_on")}:</strong> {submittedDate}</div></div>
          <div className="rr-info-card"><h3>{t("payment_info")}</h3><div><strong>{t("payment_status")}:</strong> <StatusBadge status={currentRequest.paymentStatus} type="payment" /></div>{!isFree && <div><strong>{t("amount")}:</strong> {fmtAmount(price)} EGP</div>}</div>
          {currentRequest.submittedData && Object.keys(currentRequest.submittedData).length > 0 && (
            <div className="rr-info-card"><h3>{t("submitted_data")}</h3><pre className="rr-json">{JSON.stringify(currentRequest.submittedData, null, 2)}</pre></div>
          )}
          <div className="rr-info-card">
            <h3>{t("attachments")}</h3>
            {loadingAttachments ? <div>{t("loading")}</div> : attachments.length === 0 ? <div>{t("no_attachments")}</div> : (
              <ul className="rr-attachments-list">
                {attachments.map(att => (
                  <li key={att.id}>
                    <button type="button" className="rr-attachment-link" onClick={() => download(att.id, att.fileName)} disabled={downloading}>
                      <Download size={14} /> {att.fileName}
                    </button>
                    <span className="rr-attachment-step">{att.stepKey}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
        <div className="rr-right">
          <div className="rr-timeline-card"><RequestTimeline timeline={currentRequest.history} /></div>
          {canAssign && (
            <div className="rr-actions-card">
              <h3>{t("assign")}</h3>
              <div className="rr-assignee"><UserCheck size={14} /> <strong>{t("assigned_to")}:</strong> {assigneeName || t("not_assigned")}</div>
              {assignable ? (
                <>
                  <select value={selectedStaffId} onChange={e => setSelectedStaffId(e.target.value)}>
                    <option value="">{t("select_staff")}</option>
                    {staffList.map(s => (
                      <option key={s.id} value={s.id}>{getLocalized(s.name, i18n.language) || s.employeeCode}</option>
                    ))}
                  </select>
                  <button className="btn-secondary rr-block-btn" onClick={handleAssign} disabled={assigning || !selectedStaffId}>
                    {assigning ? t("updating") : t("assign_to_staff")}
                  </button>
                </>
              ) : (
                <div className="rr-assign-hint">{t("assign_only_pending_review")}</div>
              )}
            </div>
          )}
          <div className="rr-actions-card">
            <h3>{t("actions")}</h3>
            <select value={selectedStatus} onChange={e => setSelectedStatus(e.target.value)} disabled={nextStatuses.length === 0}>
              <option value="">{t("change_status")}</option>
              {nextStatuses.map(value => (
                <option key={value} value={value}>{statusLabel(value)}</option>
              ))}
            </select>
            <textarea className="rr-comment-textarea" placeholder={t("add_comment_optional")} value={comment} onChange={e => setComment(e.target.value)} rows="3" />
            <button className="btn-primary rr-block-btn" onClick={handleStatusChange} disabled={updating || !selectedStatus}>
              {updating ? t("updating") : t("update_status")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default RequestReview;
