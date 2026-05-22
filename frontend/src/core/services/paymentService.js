import api from "../api/apiClient";

export const PAYMENT_TX_STATUS = {
  Pending: 0,
  Succeeded: 1,
  Failed: 2,
  Refunded: 3,
};

export const PAYMENT_TX_STATUS_LABELS = {
  0: "Pending",
  1: "Succeeded",
  2: "Failed",
  3: "Refunded",
};

export function getPaymentStatusLabel(value) {
  return PAYMENT_TX_STATUS_LABELS[value] || "Unknown";
}

export async function recordPayment(data) {
  return api.post("/payments/transactions", data);
}

export async function fetchTransactionsForInvoice(invoiceId) {
  return api.get(`/payments/invoices/${invoiceId}/transactions`);
}
