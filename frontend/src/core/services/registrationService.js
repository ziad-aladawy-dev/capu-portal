import api from "../api/apiClient";

// RegistrationStatus enum (read-only, synced from external systems).
export const REGISTRATION_STATUS = { Enrolled: 0, Completed: 1, Withdrawn: 2 };
export const REGISTRATION_STATUS_LABELS = {
  0: "Enrolled",
  1: "Completed",
  2: "Withdrawn",
};
export function getRegistrationStatusLabel(value) {
  return REGISTRATION_STATUS_LABELS[value] ?? "Unknown";
}

/** Current/active registered courses for the signed-in student. */
export async function fetchRegisteredCourses() {
  const { data } = await api.get("/courses/registered");
  return Array.isArray(data) ? data : data?.items || [];
}

/** Full registration history grouped by semester (latest → oldest). */
export async function fetchRegistrationHistory() {
  const { data } = await api.get("/courses/history");
  return Array.isArray(data) ? data : data?.items || [];
}

/** Every attempt of a specific course. */
export async function fetchCourseAttempts(courseId) {
  const { data } = await api.get(`/courses/${courseId}/attempts`);
  return Array.isArray(data) ? data : data?.items || [];
}
