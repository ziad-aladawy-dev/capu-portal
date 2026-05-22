import api from "../api/apiClient";

export const NOTIFICATION_TYPE = {
  Info: 1,
  Warning: 2,
  Error: 3,
};

export const NOTIFICATION_TYPE_LABELS = {
  1: "Info",
  2: "Warning",
  3: "Error",
};

export function getNotificationTypeLabel(value) {
  return NOTIFICATION_TYPE_LABELS[value] || "Info";
}

export async function fetchNotifications() {
  return api.get("/api/notifications");
}

export async function fetchAllNotifications() {
  return api.get("/api/notifications");
}

export async function fetchUnreadNotifications() {
  return api.get("/api/notifications/unread");
}

export async function markNotificationRead(id) {
  return api.put(`/api/notifications/${id}/read`);
}
