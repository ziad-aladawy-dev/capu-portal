import React from "react";
import { useTranslation } from "react-i18next";
import "../styles/components/StatusBadge.css";

const StatusBadge = ({ status }) => {
  const { t } = useTranslation();

  const getStatusString = (status) => {
    if (typeof status === "number") {
      const statusMap = {
        1: "Draft",
        2: "Pending",
        3: "UnderReview",
        4: "MoreInfoRequired",
        5: "Approved",
        6: "Rejected",
        7: "PaymentPending",
        8: "Completed",
        9: "Cancelled",
        10: "ReadyForPickup"
      };
      const paymentMap = {
        1: "NotRequired",
        2: "Pending",
        3: "Paid",
        4: "Failed",
        5: "Refunded"
      };
      if (paymentMap[status]) return paymentMap[status];
      return statusMap[status] || "Unknown";
    }
    return status;
  };

  const statusStr = getStatusString(status);
  const getStatusClass = () => {
    const map = {
      Draft: "status-draft",
      Pending: "status-pending",
      UnderReview: "status-review",
      MoreInfoRequired: "status-review",
      Approved: "status-approved",
      Rejected: "status-rejected",
      Completed: "status-completed",
      PaymentPending: "status-payment",
      Cancelled: "status-cancelled",
      ReadyForPickup: "status-completed",
      NotRequired: "status-default",
      Paid: "status-active",
      Failed: "status-inactive",
      Refunded: "status-pending"
    };
    return map[statusStr] || "status-default";
  };

  const displayText = t(statusStr) || statusStr;

  return (
    <span className={`status-badge ${getStatusClass()}`}>
      {displayText}
    </span>
  );
};

export default StatusBadge;