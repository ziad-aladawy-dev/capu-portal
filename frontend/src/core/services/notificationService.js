import api from "../api/apiClient";

export const NOTIFICATION_TYPE = {
  Info: 1,
  Warning: 2,
};

export const NOTIFICATION_TYPE_LABELS = {
  1: "Info",
  2: "Warning",
};

export function getNotificationTypeLabel(value) {
  return NOTIFICATION_TYPE_LABELS[value] || "Info";
}

export async function fetchAllNotifications() {
  const { data } = await api.get("/notifications");
  return data;
}

export async function fetchUnreadNotifications() {
  const { data } = await api.get("/notifications/unread");
  return data;
}

export async function markNotificationRead(id) {
  const { data } = await api.put(`/notifications/${id}/read`);
  return data;
}
