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

export async function recordPayment(body) {
  const { data } = await api.post("/payments/transactions", body);
  return data;
}

export async function fetchTransactionsForInvoice(invoiceId) {
  const { data } = await api.get(`/payments/invoices/${invoiceId}/transactions`);
  return data;
}
