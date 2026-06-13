import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Download, UserCheck, ArrowLeft, Send, Lock, Unlock } from "lucide-react";
import { useStaffRequests, useStaffRequestDetail, useStaffRequestAttachments } from "../../hooks/useStaffRequests";
import { useFileUpload } from "../../hooks/useFileUpload";
import { usePermission } from "../../../../core/auth/usePermission";
import { fetchAllStaff } from "../../../../core/services/staffService";
import { getServiceById } from "../../services/studentServicesService";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import { REQUEST_STATUS, PAYMENT_STATUS } from "../../../../core/constants/requestStatus";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import PageHeader from "../../../../core/components/PageHeader";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { useToast } from "../../../../core/components/Toast";
import "../../styles/admin/RequestReview.css";

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

const ASSIGNABLE_STATUSES = [REQUEST_STATUS.Pending, REQUEST_STATUS.UnderReview];
const CLOSABLE_STATUSES = [REQUEST_STATUS.Completed, REQUEST_STATUS.Cancelled];

const RequestReview = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const { addToast } = useToast();
  const { assign, changeStatus, addCommentToRequest, closeRecord, openRecord } = useStaffRequests();
  const { download, downloading } = useFileUpload();

  const { data: currentRequest, isLoading: loading, error: reqError } = useStaffRequestDetail(id);
  const { data: attachments = [], isLoading: loadingAttachments } = useStaffRequestAttachments(id);

  const [comment, setComment] = useState("");
  const [updating, setUpdating] = useState(false);
  const [selectedStatus, setSelectedStatus] = useState("");
  const [staffList, setStaffList] = useState([]);
  const [selectedStaffId, setSelectedStaffId] = useState("");
  const [assigning, setAssigning] = useState(false);
  const [commentSubmitting, setCommentSubmitting] = useState(false);
  const [serviceWorkflow, setServiceWorkflow] = useState(null);

  const [confirmClose, setConfirmClose] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [closingRecord, setClosingRecord] = useState(false);
  const [openingRecord, setOpeningRecord] = useState(false);

  const canAssign = can("student-services.requests.assign");
  const canEditClose = can("student-services.requests.editclose");
  const canOpen = can("student-services.requests.open");
  const status = Number(currentRequest?.status);
  const assignable = canAssign && ASSIGNABLE_STATUSES.includes(status);
  const canClose = canEditClose && CLOSABLE_STATUSES.includes(status) && !currentRequest?.isClosed;
  const canReopen = canOpen && currentRequest?.isClosed;

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

  useEffect(() => {
    if (!currentRequest?.serviceId) return;
    let cancelled = false;
    getServiceById(currentRequest.serviceId)
      .then((svc) => { if (!cancelled) setServiceWorkflow(svc?.workflow || null); })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [currentRequest?.serviceId]);

  const handleStatusChange = async () => {
    if (updating || !selectedStatus) return;
    setUpdating(true);
    try {
      await changeStatus(id, Number(selectedStatus), comment);
      setComment("");
      setSelectedStatus("");
      addToast(t("status_updated"), "success");
    } catch (err) {
      addToast(err.message || t("failed_to_update"), "error");
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
      addToast(t("request_assigned"), "success");
    } catch (err) {
      addToast(err.message || t("failed_to_assign"), "error");
    } finally {
      setAssigning(false);
    }
  };

  const handleAddComment = async () => {
    if (commentSubmitting || !comment.trim()) return;
    setCommentSubmitting(true);
    try {
      await addCommentToRequest(id, comment.trim());
      setComment("");
      addToast(t("comment_added"), "success");
    } catch (err) {
      addToast(err.message || t("failed_to_add_comment"), "error");
    } finally {
      setCommentSubmitting(false);
    }
  };

  const handleClose = async () => {
    if (closingRecord) return;
    setClosingRecord(true);
    try {
      await closeRecord(id);
      setConfirmClose(false);
      addToast(t("record_closed"), "success");
    } catch (err) {
      addToast(err.message || t("failed_to_update"), "error");
    } finally {
      setClosingRecord(false);
    }
  };

  const handleOpen = async () => {
    if (openingRecord) return;
    setOpeningRecord(true);
    try {
      await openRecord(id);
      setConfirmOpen(false);
      addToast(t("record_reopened"), "success");
    } catch (err) {
      addToast(err.message || t("failed_to_update"), "error");
    } finally {
      setOpeningRecord(false);
    }
  };

  if (loading || (!currentRequest && !reqError)) return <LoadingSpinner />;
  if (!currentRequest) return <ErrorMessage message={t("request_not_found")} />;

  const localizedServiceName = getLocalized(currentRequest.serviceName, i18n.language);
  const localizedStudentName = getLocalized(currentRequest.studentName, i18n.language);
  const isFree = Number(currentRequest.paymentStatus) === PAYMENT_STATUS.NotRequired;
  const price = currentRequest.amountPaid || 0;
  const submittedDate = currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-";
  const nextStatuses = VALID_NEXT[status] || [];

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

  const resolveSubmittedData = (data) => {
    if (!data || Object.keys(data).length === 0) return null;
    const steps = serviceWorkflow?.steps || [];
    return (
      <div className="rr-submitted-fields">
        {Object.entries(data).map(([stepOrder, stepData]) => {
          const step = steps.find((s) => String(s.order) === stepOrder);
          const stepTitle = step?.title || `${t("step")} ${stepOrder}`;
          return (
            <div key={stepOrder} className="rr-step-group">
              <div className="rr-step-group-title">{stepTitle}</div>
              {typeof stepData === "object" && stepData !== null ? (
                Object.entries(stepData).map(([fieldId, value]) => {
                  const field = step?.fields?.find((f) => String(f.id).toLowerCase() === fieldId.toLowerCase());
                  const label = field?.label || fieldId;
                  return (
                    <div key={fieldId} className="rr-field-row">
                      <span className="rr-field-label">{label}</span>
                      <span className="rr-field-value">
                        {typeof value === "object" && value !== null
                          ? Array.isArray(value) ? value.join(", ") : JSON.stringify(value)
                          : String(value ?? "-")}
                      </span>
                    </div>
                  );
                })
              ) : (
                <div className="rr-field-row">
                  <span className="rr-field-value">{String(stepData)}</span>
                </div>
              )}
            </div>
          );
        })}
      </div>
    );
  };

  return (
    <div className="rr-container">
      <PageHeader
        icon={Send}
        kicker={t("student_requests")}
        title={`${t("request")} #${currentRequest.requestNumber}`}
        subtitle={localizedServiceName}
        leading={
          <button className="btn-icon" onClick={() => navigate("/admin/student-services/requests")} title={t("back_to_list")}>
            <ArrowLeft size={16} />
          </button>
        }
        actions={
          currentRequest.isClosed && (
            <span className="rr-closed-badge">
              <Lock size={12} /> {t("closed_record")}
            </span>
          )
        }
      />
      <div className="rr-layout">
        <div className="rr-left">
          <div className="rr-info-card">
            <h3>{t("service_details")}</h3>
            <div><strong>{t("service_name")}:</strong> {localizedServiceName}</div>
          </div>
          <div className="rr-info-card">
            <h3>{t("student_info")}</h3>
            <div><strong>{t("student_name")}:</strong> {localizedStudentName}</div>
            <div><strong>{t("student_code")}:</strong> {currentRequest.studentCode || "-"}</div>
            <div><strong>{t("submitted_on")}:</strong> {submittedDate}</div>
          </div>
          <div className="rr-info-card">
            <h3>{t("payment_info")}</h3>
            <div><strong>{t("payment_status")}:</strong> <StatusBadge status={currentRequest.paymentStatus} type="payment" /></div>
            {!isFree && <div><strong>{t("amount")}:</strong> {fmtAmount(price)} EGP</div>}
          </div>
          {currentRequest.submittedData && Object.keys(currentRequest.submittedData).length > 0 && (
            <div className="rr-info-card">
              <h3>{t("submitted_data")}</h3>
              {resolveSubmittedData(currentRequest.submittedData)}
            </div>
          )}
          <div className="rr-info-card">
            <h3>{t("attachments")}</h3>
            {loadingAttachments ? (
              <div>{t("loading")}</div>
            ) : attachments.length === 0 ? (
              <div>{t("no_attachments")}</div>
            ) : (
              <ul className="rr-attachments-list">
                {attachments.map((att) => (
                  <li key={att.id}>
                    <button
                      type="button"
                      className="rr-attachment-link"
                      onClick={() => download(att.id, att.fileName)}
                      disabled={downloading}
                    >
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
          <div className="rr-timeline-card">
            <RequestTimeline timeline={currentRequest.history} />
          </div>
          {canAssign && (
            <div className="rr-actions-card">
              <h3>{t("assign")}</h3>
              <div className="rr-assignee">
                <UserCheck size={14} /> <strong>{t("assigned_to")}:</strong> {assigneeName || t("not_assigned")}
              </div>
              {assignable ? (
                <>
                  <select value={selectedStaffId} onChange={(e) => setSelectedStaffId(e.target.value)}>
                    <option value="">{t("select_staff")}</option>
                    {staffList.map((s) => (
                      <option key={s.id} value={s.id}>
                        {getLocalized(s.name, i18n.language) || s.employeeCode}
                      </option>
                    ))}
                  </select>
                  <button
                    className="btn-secondary rr-block-btn"
                    onClick={handleAssign}
                    disabled={assigning || !selectedStaffId}
                  >
                    {assigning ? t("updating") : t("assign_to_staff")}
                  </button>
                </>
              ) : (
                <div className="rr-assign-hint">{t("assign_only_pending_review")}</div>
              )}
            </div>
          )}
          <div className="rr-actions-card">
            <h3>{t("status_action")}</h3>
            <select
              value={selectedStatus}
              onChange={(e) => setSelectedStatus(e.target.value)}
              disabled={nextStatuses.length === 0}
            >
              <option value="">{t("change_status")}</option>
              {nextStatuses.map((value) => (
                <option key={value} value={value}>{statusLabel(value)}</option>
              ))}
            </select>
            <textarea
              className="rr-comment-textarea"
              placeholder={t("add_comment_optional")}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows="3"
            />
            <div className="rr-action-buttons">
              <button
                className="btn-primary rr-block-btn"
                onClick={handleStatusChange}
                disabled={updating || !selectedStatus}
              >
                {updating ? t("updating") : t("update_status")}
              </button>
              {comment.trim() && (
                <button
                  className="btn-outline rr-block-btn"
                  onClick={handleAddComment}
                  disabled={commentSubmitting}
                >
                  {commentSubmitting ? t("sending") : t("add_comment")}
                </button>
              )}
            </div>
          </div>
          {(canClose || canReopen) && (
            <div className="rr-actions-card">
              <h3>{t("record_management")}</h3>
              {canClose && (
                <>
                  <p className="rr-hint">{t("close_record_hint")}</p>
                  <button
                    className="btn-danger rr-block-btn"
                    onClick={() => setConfirmClose(true)}
                  >
                    <Lock size={14} /> {t("close_record")}
                  </button>
                </>
              )}
              {canReopen && (
                <>
                  <p className="rr-hint">{t("open_record_hint")}</p>
                  <button
                    className="btn-secondary rr-block-btn"
                    onClick={() => setConfirmOpen(true)}
                  >
                    <Unlock size={14} /> {t("open_record")}
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      </div>
      <ConfirmDialog
        open={confirmClose}
        onClose={() => setConfirmClose(false)}
        onConfirm={handleClose}
        title={t("close_record")}
        message={t("confirm_close_message")}
        confirmLabel={t("close_record")}
        cancelLabel={t("cancel")}
        variant="danger"
      />
      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={handleOpen}
        title={t("open_record")}
        message={t("confirm_open_message")}
        confirmLabel={t("open_record")}
        cancelLabel={t("cancel")}
        variant="default"
      />
    </div>
  );
};

export default RequestReview;
