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

export async function fetchProfileRecords(studentId, params = {}) {
  const { data } = await api.get(`/students/${studentId}/profile-records`, { params });
  return data;
}

export async function fetchProfileRecordByCategory(studentId, category, customKey) {
  const params = customKey ? { customKey } : undefined;
  const { data } = await api.get(`/students/${studentId}/profile-records/by-category/${category}`, { params });
  return data;
}

export async function fetchProfileRecordById(studentId, recordId) {
  const { data } = await api.get(`/students/${studentId}/profile-records/${recordId}`);
  return data;
}

export async function upsertProfileRecord(studentId, body) {
  const { data } = await api.put(`/students/${studentId}/profile-records`, body);
  return data;
}

export async function verifyProfileRecord(studentId, recordId, verifiedBy) {
  const { data } = await api.post(`/students/${studentId}/profile-records/${recordId}/verify`, {
    verifiedBy,
  });
  return data;
}

export async function deleteProfileRecord(studentId, recordId) {
  const { data } = await api.delete(`/students/${studentId}/profile-records/${recordId}`);
  return data;
}

export async function batchUpsertProfileRecords(studentId, records) {
  const { data } = await api.put(`/students/${studentId}/profile-records/batch`, { records });
  return data;
}

export async function batchVerifyProfileRecords(studentId, recordIds, verifiedBy) {
  const { data } = await api.post(`/students/${studentId}/profile-records/verify`, {
    recordIds,
    verifiedBy,
  });
  return data;
}
