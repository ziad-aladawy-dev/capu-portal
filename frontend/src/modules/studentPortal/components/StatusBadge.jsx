import {
  REQUEST_STATUS_LABELS, REQUEST_STATUS_CLASS,
  PAYMENT_STATUS_LABELS, PAYMENT_STATUS_CLASS,
} from "../constants/requestStatus";
import "../../studentServices/styles/components/StatusBadge.css";

// Backend sends status as an integer enum. `type` selects the right lookup;
// strings are still accepted for any legacy caller.
const StatusBadge = ({ status, type = "request" }) => {
  if (status == null) return null;

  if (typeof status === "number") {
    const labels = type === "payment" ? PAYMENT_STATUS_LABELS : REQUEST_STATUS_LABELS;
    const classes = type === "payment" ? PAYMENT_STATUS_CLASS : REQUEST_STATUS_CLASS;
    return (
      <span className={`status-badge ${classes[status] || "status-default"}`}>
        {labels[status] || status}
      </span>
    );
  }

  // Legacy string path
  const legacyMap = {
    Pending: "status-pending", UnderReview: "status-review", Approved: "status-approved",
    Rejected: "status-rejected", Completed: "status-completed", Draft: "status-draft",
    PaymentPending: "status-payment", Cancelled: "status-cancelled",
    active: "status-active", inactive: "status-inactive",
  };
  return (
    <span className={`status-badge ${legacyMap[status] || "status-default"}`}>
      {status.replace(/([A-Z])/g, " $1").trim()}
    </span>
  );
};

export default StatusBadge;
