import React, { useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import LoadingSpinner from "../../components/LoadingSpinner";
import RequestTimeline from "../../components/RequestTimeline";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/student/StudentRequestDetails.css";

const StudentRequestDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { currentRequest, loading, getRequest } = useStudentRequests();

  useEffect(() => {
    getRequest(id);
  }, [id]);

  if (loading) return <LoadingSpinner />;
  if (!currentRequest) return <div className="srd-error">{t("request_not_found")}</div>;

  return (
    <div className="srd-container">
      <button className="srd-back" onClick={() => navigate("/student/requests")}>
        ← {t("back_to_requests")}
      </button>
      <div className="srd-header">
        <h1>{t("request")} #{currentRequest.id}</h1>
        <StatusBadge status={currentRequest.status} />
      </div>
      <div className="srd-content">
        <div className="srd-info">
          <div className="srd-card">
            <h3>{t("service")}</h3>
            <p>{currentRequest.serviceName}</p>
          </div>
          <div className="srd-card">
            <h3>{t("submitted_on")}</h3>
            <p>{currentRequest.submittedAt ? new Date(currentRequest.submittedAt).toLocaleString() : "-"}</p>
          </div>
          <div className="srd-card">
            <h3>{t("payment")}</h3>
            <p>
              {currentRequest.paymentStatus} 
              {currentRequest.amountPaid ? ` ($${currentRequest.amountPaid})` : ""}
            </p>
          </div>
          {currentRequest.submittedData && Object.keys(currentRequest.submittedData).length > 0 && (
            <div className="srd-card">
              <h3>{t("submitted_data")}</h3>
              <pre>{JSON.stringify(currentRequest.submittedData, null, 2)}</pre>
            </div>
          )}
        </div>
        <div className="srd-timeline">
          <RequestTimeline timeline={currentRequest.history} />
        </div>
      </div>
    </div>
  );
};

export default StudentRequestDetails;