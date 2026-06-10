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

export const SERVICE_TYPE = { General: 1, Specialized: 2, Administrative: 3 };
export const SERVICE_TYPE_LABELS = { 1: "General", 2: "Specialized", 3: "Administrative" };

/** Ordered columns for the request Kanban board. */
export const KANBAN_COLUMNS = [
  { key: REQUEST_STATUS.Draft, label: "Draft" },
  { key: REQUEST_STATUS.Pending, label: "Pending" },
  { key: REQUEST_STATUS.UnderReview, label: "Under Review" },
  { key: REQUEST_STATUS.MoreInfoRequired, label: "Action Needed" },
  { key: REQUEST_STATUS.PaymentPending, label: "Payment" },
  { key: REQUEST_STATUS.Approved, label: "Approved" },
  { key: REQUEST_STATUS.ReadyForPickup, label: "Ready" },
  { key: REQUEST_STATUS.Completed, label: "Completed" },
];
