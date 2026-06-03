import React from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import StatusBadge from "./StatusBadge";
import "../styles/components/RequestCard.css";

const RequestCard = ({ request, role = "student" }) => {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className="request-card" onClick={() => navigate(`/${role}/requests/${request.id}`)}>
      <div className="request-card-header">
        <span className="request-id">#{request.id}</span>
        <StatusBadge status={request.status} />
      </div>
      <div className="request-card-body">
        <h4>{request.serviceName}</h4>
        <p>{t("submitted")}: {new Date(request.submittedDate).toLocaleDateString()}</p>
        {role === "staff" && <p>{t("student")}: {request.studentName}</p>}
      </div>
    </div>
  );
};

export default RequestCard;