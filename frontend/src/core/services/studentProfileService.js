import api from "../api/apiClient";

export const STUDENT_PROFILE_CATEGORY = {
  Custom: 0,
  MilitaryInformation: 1,
  VaccinationInformation: 2,
  EmergencyContact: 3,
  DisabilityInformation: 4,
  HousingInformation: 5,
};

export const STUDENT_PROFILE_CATEGORY_LABELS = {
  0: "Custom",
  1: "Military Information",
  2: "Vaccination Information",
  3: "Emergency Contact",
  4: "Disability Information",
  5: "Housing Information",
};

export function getProfileCategoryLabel(value) {
  return STUDENT_PROFILE_CATEGORY_LABELS[value] ?? "Custom";
}

export const STUDENT_PROFILE_CATEGORY_OPTIONS = Object.entries(
  STUDENT_PROFILE_CATEGORY_LABELS
).map(([value, label]) => ({ value: Number(value), label }));

export async function fetchProfileRecords(studentId) {
  return api.get(`/students/${studentId}/profile-records`);
}

export async function fetchProfileRecordByCategory(studentId, category, customKey) {
  const params = customKey ? { customKey } : undefined;
  return api.get(`/students/${studentId}/profile-records/by-category/${category}`, params);
}

export async function fetchProfileRecordById(studentId, recordId) {
  return api.get(`/students/${studentId}/profile-records/${recordId}`);
}

export async function upsertProfileRecord(studentId, data) {
  return api.put(`/students/${studentId}/profile-records`, data);
}

export async function verifyProfileRecord(studentId, recordId, verifiedBy) {
  return api.post(`/students/${studentId}/profile-records/${recordId}/verify`, {
    verifiedBy,
  });
}

export async function deleteProfileRecord(studentId, recordId) {
  return api.delete(`/students/${studentId}/profile-records/${recordId}`);
}
