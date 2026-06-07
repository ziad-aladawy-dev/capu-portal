import api from "../api/apiClient";

export async function fetchAllStaff(params = {}) {
  const { data } = await api.get("/staff", { params });
  return data;
}

export async function fetchStaffById(id) {
  const { data } = await api.get(`/staff/${id}`);
  return data;
}

export async function searchStaff(params = {}) {
  const { data } = await api.get("/staff/search", { params });
  return data;
}

export async function createStaff(body) {
  const { data } = await api.post("/staff", body);
  return data;
}

export async function updateStaff(id, body) {
  const { data } = await api.put(`/staff/${id}`, body);
  return data;
}

export async function deleteStaff(id) {
  const { data } = await api.delete(`/staff/${id}`);
  return data;
}

export async function toggleStaffStatus(id) {
  const { data } = await api.patch(`/staff/${id}/toggle-status`);
  return data;
}

export async function fetchStaffStatistics(params = {}) {
  const { data } = await api.get("/staff/statistics", { params });
  return data;
}

export async function exportStaffCsv(params = {}) {
  const { data } = await api.get("/staff/export/csv", { params });
  return data;
}

export async function exportStaffExcel(params = {}) {
  const { data } = await api.get("/staff/export-excel", { params });
  return data;
}
