import { useTranslation } from "react-i18next";
import StatusBadge from "./StatusBadge";
import {
  REQUEST_STATUS_LABELS,
  PAYMENT_STATUS_LABELS,
} from "../constants/requestStatus";

// Student-services request/payment statuses arrive as integer enums
// (no JsonStringEnumConverter on the backend). `type` selects the lookup;
// strings are still accepted for legacy callers.

const REQUEST_STATUS_NAMES = {
  1: "Draft", 2: "Pending", 3: "UnderReview", 4: "MoreInfoRequired", 5: "Approved",
  6: "Rejected", 7: "PaymentPending", 8: "Completed", 9: "Cancelled", 10: "ReadyForPickup",
};

const PAYMENT_STATUS_NAMES = {
  1: "NotRequired", 2: "Pending", 3: "Paid", 4: "Failed", 5: "Refunded",
};

// Status name → core StatusBadge tone.
const TONES = {
  Draft: "draft",
  Pending: "pending",
  UnderReview: "review",
  MoreInfoRequired: "review",
  Approved: "approved",
  Rejected: "rejected",
  PaymentPending: "payment",
  Completed: "completed",
  Cancelled: "cancelled",
  ReadyForPickup: "completed",
  NotRequired: "inactive",
  Paid: "active",
  Failed: "rejected",
  Refunded: "cancelled",
  active: "active",
  inactive: "inactive",
};

export default function RequestStatusBadge({ status, type = "request", style }) {
  const { t } = useTranslation();
  if (status == null) return null;

  let name;
  let fallbackLabel;
  if (typeof status === "number") {
    const names = type === "payment" ? PAYMENT_STATUS_NAMES : REQUEST_STATUS_NAMES;
    const labels = type === "payment" ? PAYMENT_STATUS_LABELS : REQUEST_STATUS_LABELS;
    name = names[status] || "inactive";
    fallbackLabel = labels[status] || String(status);
  } else {
    name = status;
    fallbackLabel = String(status).replace(/([A-Z])/g, " $1").trim();
  }

  const label = t(name, { defaultValue: fallbackLabel });
  return <StatusBadge status={TONES[name] || "inactive"} label={label} style={style} />;
}
