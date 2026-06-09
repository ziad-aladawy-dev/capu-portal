import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStaffRequests } from "../../hooks/useStaffRequests";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../components/StatusBadge";
import PermissionGate from "../../../../core/auth/PermissionGate";
import LoadingSpinner from "../../components/LoadingSpinner";
import "../../styles/admin/RequestReview.css";

const RequestReview = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { currentRequest, loading, getRequest, changeStatus, addCommentToRequest, assign } = useStaffRequests();
  const [comment, setComment] = useState("");
  const [updating, setUpdating] = useState(false);
  const [staffId, setStaffId] = useState("");

  useEffect(() => {
    getRequest(id);
  }, [id]);

  const handleStatusChange = async (status) => {
    if (updating) return;
    setUpdating(true);
    try {
      await changeStatus(id, status, comment);
      setComment("");
    } catch (err) {
      console.error(err);
    } finally {
      setUpdating(false);
    }
  };

  const handleAddComment = async () => {
    if (!comment.trim() || updating) return;
    setUpdating(true);
    try {
      await addCommentToRequest(id, comment);
      setComment("");
    } catch (err) {
      console.error(err);
    } finally {
      setUpdating(false);
    }
  };

  const handleAssign = async () => {
    if (!staffId) return;
    try {
      await assign(id, staffId);
    } catch (err) {
      console.error(err);
    }
  };

  if (loading) return <LoadingSpinner />;
  if (!currentRequest) return <div className="rr-error">{t("request_not_found")}</div>;

  return (
    <div className="rr-container">
      <div className="rr-header">
        <button className="rr-back-btn" onClick={() => navigate("/admin/student-services/requests")}>
          ← {t("back")}
        </button>
          <h1>{t("request_details")} #{currentRequest.id}</h1>
          <PermissionGate resource="student-services.requests" minLevel={4}>
            <div className="rr-assign">
              <input
                type="text"
                placeholder={t("staff_id")}
                value={staffId}
                onChange={e => setStaffId(e.target.value)}
              />
              <button onClick={handleAssign}>{t("assign")}</button>
            </div>
          </PermissionGate>
      </div>
      <div className="rr-layout">
        <div className="rr-left">
          <div className="rr-info-card">
            <h3>{t("service")}</h3>
            <p>{currentRequest.serviceName}</p>
          </div>
          <div className="rr-info-card">
            <h3>{t("submitted")}</h3>
            <p>{currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-"}</p>
          </div>
          <div className="rr-info-card">
            <h3>{t("payment")}</h3>
            <p><StatusBadge status={currentRequest.paymentStatus} /></p>
          </div>
          <div className="rr-info-card">
            <h3>{t("submitted_data")}</h3>
            <pre className="rr-json">{JSON.stringify(currentRequest.submittedData, null, 2)}</pre>
          </div>
        </div>
        <div className="rr-right">
          <div className="rr-timeline-card">
            <RequestTimeline timeline={currentRequest.history} />
          </div>
          <PermissionGate resource="student-services.requests" minLevel={3}>
            <div className="rr-actions-card">
              <h3>{t("actions")}</h3>
              <select onChange={e => handleStatusChange(e.target.value)} defaultValue="">
                <option disabled value="">{t("change_status")}</option>
                <option value="UnderReview">Under Review</option>
                <option value="Approved">Approved</option>
                <option value="Rejected">Rejected</option>
                <option value="Completed">Completed</option>
                <option value="MoreInfoRequired">More Info Required</option>
              </select>
              <textarea
                className="rr-comment-textarea"
                placeholder={t("add_comment")}
                value={comment}
                onChange={e => setComment(e.target.value)}
                rows="3"
              />
              <button className="rr-comment-btn" onClick={handleAddComment} disabled={updating}>
                {t("add_comment")}
              </button>
            </div>
          </PermissionGate>
        </div>
      </div>
    </div>
  );
};

export default RequestReview;