import api from "../api/apiClient";

export async function fetchAllStaff(params = {}) {
  return api.get("/api/staff", params);
}

export async function fetchStaffById(id) {
  return api.get(`/api/staff/${id}`);
}

export async function searchStaff(params = {}) {
  return api.get("/api/staff/search", params);
}

export async function createStaff(data) {
  return api.post("/api/staff", data);
}

export async function updateStaff(id, data) {
  return api.put(`/api/staff/${id}`, data);
}

export async function deleteStaff(id) {
  return api.delete(`/api/staff/${id}`);
}

export async function toggleStaffStatus(id) {
  return api.patch(`/api/staff/${id}/toggle-status`);
}

export async function fetchStaffStatistics(params = {}) {
  return api.get("/api/staff/statistics", params);
}

export async function exportStaffCsv(params = {}) {
  return api.get("/api/staff/export/csv", params);
}

export async function exportStaffExcel(params = {}) {
  return api.get("/api/staff/export-excel", params);
}

export async function bulkImportStaff(data) {
  return api.post("/api/staff/bulk-import", data);
}

export async function importStaffExcel(file) {
  const formData = new FormData();
  formData.append("file", file);
  return api.post("/api/staff/import-excel", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
}
