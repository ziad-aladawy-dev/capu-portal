import api from "../api/apiClient";

export const INVOICE_STATUS = {
  Pending: 0,
  PartiallyPaid: 1,
  Paid: 2,
  Cancelled: 3,
  Refunded: 4,
};

export const INVOICE_STATUS_LABELS = {
  0: "Pending",
  1: "Partially Paid",
  2: "Paid",
  3: "Cancelled",
  4: "Refunded",
};

export function getInvoiceStatusLabel(value) {
  return INVOICE_STATUS_LABELS[value] || "Unknown";
}

export async function fetchInvoice(id) {
  const { data } = await api.get(`/invoices/${id}`);
  return data;
}

export async function fetchInvoicesForStudent(studentId) {
  const { data } = await api.get(`/invoices/by-student/${studentId}`);
  return data;
}

export async function createInvoice(body) {
  const { data } = await api.post("/invoices", body);
  return data;
}

export async function cancelInvoice(id, reason) {
  const { data } = await api.post(`/invoices/${id}/cancel`, { reason });
  return data;
}
