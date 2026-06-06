import React from "react";
import "../../studentServices/styles/components/StatusBadge.css";

const StatusBadge = ({ status, type = "request" }) => {
  const getStatusClass = () => {
    const statusMap = {
      Pending: "status-pending",
      UnderReview: "status-review",
      Approved: "status-approved",
      Rejected: "status-rejected",
      Completed: "status-completed",
      Draft: "status-draft",
      PaymentPending: "status-payment",
      Cancelled: "status-cancelled",
      active: "status-active",
      inactive: "status-inactive",
    };
    return statusMap[status] || "status-default";
  };

  const getLabel = () => {
    if (typeof status === "string") {
      return status.replace(/([A-Z])/g, " $1").trim();
    }
    return status;
  };

  return <span className={`status-badge ${getStatusClass()}`}>{getLabel()}</span>;
};

export default StatusBadge;