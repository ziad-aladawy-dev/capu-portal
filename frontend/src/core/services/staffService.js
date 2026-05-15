import api from "../api/apiClient";

export async function fetchAllStaff(params = {}) {
  return api.get("/staff", params);
}

export async function fetchStaffById(id) {
  return api.get(`/staff/${id}`);
}

export async function searchStaff(params = {}) {
  return api.get("/staff/search", params);
}

export async function createStaff(data) {
  return api.post("/staff", data);
}

export async function updateStaff(id, data) {
  return api.put(`/staff/${id}`, data);
}

export async function deleteStaff(id) {
  return api.delete(`/staff/${id}`);
}

export async function toggleStaffStatus(id) {
  return api.patch(`/staff/${id}/toggle-status`);
}

export async function fetchStaffStatistics(params = {}) {
  return api.get("/staff/statistics", params);
}

export async function exportStaffCsv(params = {}) {
  return api.get("/staff/export/csv", params);
}

export async function exportStaffExcel(params = {}) {
  return api.get("/staff/export-excel", params);
}
