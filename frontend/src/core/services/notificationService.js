import api from "../api/apiClient";

export async function fetchNotifications() {
  return api.get("/api/notifications");
}

export async function fetchUnreadNotifications() {
  return api.get("/api/notifications/unread");
}

export async function markNotificationRead(id) {
  return api.put(`/api/notifications/${id}/read`);
}
