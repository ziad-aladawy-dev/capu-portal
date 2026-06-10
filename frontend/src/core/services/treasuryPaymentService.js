import api from "../api/apiClient";

// ── Enums (mirror backend; see docs/payment/Payment_Domain_Model.md) ─────────

/** StudentFee.Status */
export const FEE_STATUS = {
  Pending: 0, IncludedInOrder: 1, Paid: 2, Cancelled: 3, Refunded: 4,
};
export const FEE_STATUS_LABELS = {
  0: "Pending", 1: "In Order", 2: "Paid", 3: "Cancelled", 4: "Refunded",
};

/** Order.Status */
export const ORDER_STATUS = {
  Created: 0, PendingPayment: 1, Paid: 2, Failed: 3, Expired: 4, Refunded: 5, Cancelled: 6,
};
export const ORDER_STATUS_LABELS = {
  0: "Created", 1: "Pending Payment", 2: "Paid", 3: "Failed",
  4: "Expired", 5: "Refunded", 6: "Cancelled",
};
/** Terminal order states — no further action possible. */
export const ORDER_TERMINAL = [
  ORDER_STATUS.Paid, ORDER_STATUS.Failed, ORDER_STATUS.Expired,
  ORDER_STATUS.Refunded, ORDER_STATUS.Cancelled,
];

/** Payment gateways. Route segments: mastercard / bm / efinance. */
export const GATEWAY = { Mastercard: 0, BankMisr: 1, EFinance: 2 };
export const GATEWAY_LABELS = { 0: "Mastercard", 1: "Bank Misr", 2: "eFinance" };

// ── Reads ────────────────────────────────────────────────────────────────────

/** All fees for a student (Pending / IncludedInOrder / Paid / …). */
export async function fetchStudentFees(studentId) {
  const { data } = await api.get(`/payments/fees/by-student/${studentId}`);
  return Array.isArray(data) ? data : data?.items || [];
}

/** All payment orders for a student (latest first by the caller). */
export async function fetchStudentOrders(studentId) {
  const { data } = await api.get(`/payments/orders/by-student/${studentId}`);
  return Array.isArray(data) ? data : data?.items || [];
}

/** A single order with its fees — used to poll settlement after gateway return. */
export async function fetchOrder(orderId) {
  const { data } = await api.get(`/payments/orders/${orderId}`);
  return data || null;
}

// ── Writes ───────────────────────────────────────────────────────────────────

/** Create an order from selected unpaid fees. Returns the created OrderResponse. */
export async function createOrder({ studentId, feeIds, gateway }) {
  const { data } = await api.post("/payments/orders", { studentId, feeIds, gateway });
  return data;
}

/**
 * Open a Treasury payment session for an order. The gateway will send the
 * student back to redirectUrl after payment. Returns { orderId, merchantOrderId,
 * redirectUrl } — the caller navigates the browser to redirectUrl.
 */
export async function initiateOrder(orderId, redirectUrl) {
  const { data } = await api.post(
    `/payments/orders/${orderId}/initiate`,
    null,
    { params: { redirectUrl } }
  );
  return data;
}

/** Cancel a not-yet-paid order; its fees return to Pending. */
export async function cancelOrder(orderId) {
  const { data } = await api.post(`/payments/orders/${orderId}/cancel`);
  return data;
}

// ── Display helpers ──────────────────────────────────────────────────────────

export function fmtAmount(n) {
  return Number(n || 0).toLocaleString("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

/** Human label for a fee with no receipt name — source + quantity breakdown. */
export function feeLabel(fee) {
  const src = fee.sourceModule || "Fee";
  return fee.quantity > 1 ? `${src} (${fee.quantity}×)` : src;
}
