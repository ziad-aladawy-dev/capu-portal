import api from "../api/apiClient";

/* ────────────────────────────────────────────────────────────────
   Enums — mirror the backend int-backed Treasury enums
   (CapitalUniversity.Module.Payments.Abstractions/Payments/Treasury)
   ──────────────────────────────────────────────────────────────── */

export const FEE_STATUS = {
  Pending: 0,
  IncludedInOrder: 1,
  Paid: 2,
  Cancelled: 3,
  Refunded: 4,
};

export const ORDER_STATUS = {
  Created: 0,
  PendingPayment: 1,
  Paid: 2,
  Failed: 3,
  Expired: 4,
  Refunded: 5,
  Cancelled: 6,
};

export const GATEWAY = {
  Mastercard: 0,
  BankMisr: 1,
  EFinance: 2,
};

export const QUANTITY_MODE = {
  Fixed: 0,
  CallerSupplied: 1,
};

/* i18n key maps — resolve with t() at render time */
export const FEE_STATUS_KEYS = {
  [FEE_STATUS.Pending]: "treasury_fee_status_pending",
  [FEE_STATUS.IncludedInOrder]: "treasury_fee_status_in_order",
  [FEE_STATUS.Paid]: "treasury_fee_status_paid",
  [FEE_STATUS.Cancelled]: "treasury_fee_status_cancelled",
  [FEE_STATUS.Refunded]: "treasury_fee_status_refunded",
};

export const ORDER_STATUS_KEYS = {
  [ORDER_STATUS.Created]: "treasury_order_status_created",
  [ORDER_STATUS.PendingPayment]: "treasury_order_status_pending_payment",
  [ORDER_STATUS.Paid]: "treasury_order_status_paid",
  [ORDER_STATUS.Failed]: "treasury_order_status_failed",
  [ORDER_STATUS.Expired]: "treasury_order_status_expired",
  [ORDER_STATUS.Refunded]: "treasury_order_status_refunded",
  [ORDER_STATUS.Cancelled]: "treasury_order_status_cancelled",
};

export const GATEWAY_KEYS = {
  [GATEWAY.Mastercard]: "treasury_gateway_mastercard",
  [GATEWAY.BankMisr]: "treasury_gateway_bankmisr",
  [GATEWAY.EFinance]: "treasury_gateway_efinance",
};

export const QUANTITY_MODE_KEYS = {
  [QUANTITY_MODE.Fixed]: "treasury_quantity_mode_fixed",
  [QUANTITY_MODE.CallerSupplied]: "treasury_quantity_mode_caller",
};

/* StatusBadge visual variants for each order/fee status */
export const ORDER_STATUS_BADGE = {
  [ORDER_STATUS.Created]: "draft",
  [ORDER_STATUS.PendingPayment]: "open",
  [ORDER_STATUS.Paid]: "active",
  [ORDER_STATUS.Failed]: "cancelled",
  [ORDER_STATUS.Expired]: "closed",
  [ORDER_STATUS.Refunded]: "draft",
  [ORDER_STATUS.Cancelled]: "cancelled",
};

/** Order statuses an employee can still act on. */
export function isOrderActionable(status) {
  return status === ORDER_STATUS.Created || status === ORDER_STATUS.PendingPayment;
}

/** Extract the localized human message from an API error (RFC-7807 ProblemDetails). */
export function apiErrorMessage(err, fallback = "") {
  return (
    err?.response?.data?.detail ||
    err?.response?.data?.title ||
    err?.message ||
    fallback
  );
}

/** Format money with tabular separators: 12,345.00 */
export function fmtAmount(n) {
  return Number(n ?? 0).toLocaleString("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

/* ────────────────────────────────────────────────────────────────
   Receipts
   ──────────────────────────────────────────────────────────────── */

/** Locally synced receipt catalog — Portal Guid ids (used by fees + mappings). */
export async function fetchReceipts() {
  const { data } = await api.get("/payments/receipts");
  return data;
}

/** Live passthrough from the HU Treasury system — integer external ids. */
export async function fetchLiveReceipts() {
  const { data } = await api.get("/treasury/receipts");
  return data;
}

/* ────────────────────────────────────────────────────────────────
   Student fees
   ──────────────────────────────────────────────────────────────── */

/** Unpaid (Pending) fees for a student — the order-assembly pool. */
export async function fetchUnpaidFees(studentId) {
  const { data } = await api.get(`/payments/fees/by-student/${studentId}`);
  return data;
}

export async function fetchFee(id) {
  const { data } = await api.get(`/payments/fees/${id}`);
  return data;
}

/* ────────────────────────────────────────────────────────────────
   Orders
   ──────────────────────────────────────────────────────────────── */

export async function createOrder({ studentId, feeIds, gateway }) {
  const { data } = await api.post("/payments/orders", { studentId, feeIds, gateway });
  return data;
}

export async function fetchOrder(id) {
  const { data } = await api.get(`/payments/orders/${id}`);
  return data;
}

export async function fetchOrdersForStudent(studentId) {
  const { data } = await api.get(`/payments/orders/by-student/${studentId}`);
  return data;
}

/** Only valid while the order is still in Created status. */
export async function cancelOrder(id) {
  const { data } = await api.post(`/payments/orders/${id}/cancel`);
  return data;
}

/**
 * Move the order to PendingPayment and obtain the gateway redirect URL.
 * Idempotent — re-initiating a PendingPayment order returns the same session.
 */
export async function initiatePayment(id, redirectUrl) {
  const { data } = await api.post(
    `/payments/orders/${id}/initiate`,
    null,
    { params: redirectUrl ? { redirectUrl } : {} }
  );
  return data;
}

/* ────────────────────────────────────────────────────────────────
   Service → receipt mappings
   ──────────────────────────────────────────────────────────────── */

export async function fetchServiceMappings() {
  const { data } = await api.get("/payments/service-receipt-mappings");
  return data;
}

export async function fetchServiceMapping(id) {
  const { data } = await api.get(`/payments/service-receipt-mappings/${id}`);
  return data;
}

export async function createServiceMapping({ studentServiceId, receiptId, quantityMode }) {
  const { data } = await api.post("/payments/service-receipt-mappings", {
    studentServiceId,
    receiptId,
    quantityMode,
  });
  return data;
}

export async function deactivateServiceMapping(id) {
  const { data } = await api.post(`/payments/service-receipt-mappings/${id}/deactivate`);
  return data;
}
