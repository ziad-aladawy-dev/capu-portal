// Student-services request enums — integers, matching the backend
// (no JsonStringEnumConverter is configured, so enums serialize as ints).

export const REQUEST_STATUS = {
  Draft: 1, Pending: 2, UnderReview: 3, MoreInfoRequired: 4, Approved: 5,
  Rejected: 6, PaymentPending: 7, Completed: 8, Cancelled: 9, ReadyForPickup: 10,
};

export const REQUEST_STATUS_LABELS = {
  1: "Draft", 2: "Pending", 3: "Under Review", 4: "More Info Required",
  5: "Approved", 6: "Rejected", 7: "Payment Pending", 8: "Completed",
  9: "Cancelled", 10: "Ready for Pickup",
};

/** PascalCase enum names — used to build i18n keys (portal_requests.status_<Name>). */
export const REQUEST_STATUS_NAMES = {
  1: "Draft", 2: "Pending", 3: "UnderReview", 4: "MoreInfoRequired",
  5: "Approved", 6: "Rejected", 7: "PaymentPending", 8: "Completed",
  9: "Cancelled", 10: "ReadyForPickup",
};

/** PortalBadge tone per request status. */
export const REQUEST_STATUS_TONE = {
  1: "neutral", 2: "info", 3: "accent", 4: "warning", 5: "success",
  6: "danger", 7: "warning", 8: "success", 9: "neutral", 10: "primary",
};

// Maps to the status-* classes in studentServices/styles/components/StatusBadge.css.
export const REQUEST_STATUS_CLASS = {
  1: "status-draft", 2: "status-pending", 3: "status-review",
  4: "status-moreinfo", 5: "status-approved", 6: "status-rejected",
  7: "status-payment", 8: "status-completed", 9: "status-cancelled",
  10: "status-ready",
};

export const PAYMENT_STATUS = {
  NotRequired: 1, Pending: 2, Paid: 3, Failed: 4, Refunded: 5,
};

export const PAYMENT_STATUS_LABELS = {
  1: "Not Required", 2: "Pending", 3: "Paid", 4: "Failed", 5: "Refunded",
};

export const PAYMENT_STATUS_CLASS = {
  1: "status-default", 2: "status-pending", 3: "status-completed",
  4: "status-rejected", 5: "status-cancelled",
};

/** PascalCase enum names — i18n keys (portal_requests.payment_<Name>). */
export const PAYMENT_STATUS_NAMES = {
  1: "NotRequired", 2: "Pending", 3: "Paid", 4: "Failed", 5: "Refunded",
};

/** PortalBadge tone per payment status. */
export const PAYMENT_STATUS_TONE = {
  1: "neutral", 2: "warning", 3: "success", 4: "danger", 5: "neutral",
};

export const SERVICE_TYPE = { General: 1, Specialized: 2, Administrative: 3 };
export const SERVICE_TYPE_LABELS = { 1: "General", 2: "Specialized", 3: "Administrative" };

/**
 * Ordered columns for the request Kanban board. Every status maps to exactly
 * one column (Rejected + Cancelled share "Closed") so no request is invisible.
 * `labelKey` resolves under portal_requests.* in common.json.
 */
export const KANBAN_COLUMNS = [
  { key: "draft", statuses: [REQUEST_STATUS.Draft], labelKey: "status_Draft", label: "Draft" },
  { key: "pending", statuses: [REQUEST_STATUS.Pending], labelKey: "status_Pending", label: "Pending" },
  { key: "review", statuses: [REQUEST_STATUS.UnderReview], labelKey: "status_UnderReview", label: "Under Review" },
  { key: "action", statuses: [REQUEST_STATUS.MoreInfoRequired], labelKey: "status_MoreInfoRequired", label: "Action Needed" },
  { key: "payment", statuses: [REQUEST_STATUS.PaymentPending], labelKey: "status_PaymentPending", label: "Payment" },
  { key: "approved", statuses: [REQUEST_STATUS.Approved], labelKey: "status_Approved", label: "Approved" },
  { key: "ready", statuses: [REQUEST_STATUS.ReadyForPickup], labelKey: "status_ReadyForPickup", label: "Ready" },
  { key: "completed", statuses: [REQUEST_STATUS.Completed], labelKey: "status_Completed", label: "Completed" },
  { key: "closed", statuses: [REQUEST_STATUS.Rejected, REQUEST_STATUS.Cancelled], labelKey: "column_closed", label: "Closed" },
];
