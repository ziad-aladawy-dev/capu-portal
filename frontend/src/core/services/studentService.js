import api from "../api/apiClient";

export async function fetchAllStudents(params = {}) {
  const { data } = await api.get("/students", { params });
  return data;
}

export async function fetchStudentById(id) {
  const { data } = await api.get(`/students/${id}`);
  return data;
}

export async function searchStudents(params = {}) {
  const { data } = await api.get("/students/search", { params });
  return data;
}

export async function createStudent(body) {
  const { data } = await api.post("/students", body);
  return data;
}

export async function updateStudent(id, body) {
  const { data } = await api.put(`/students/${id}`, body);
  return data;
}

export async function deleteStudent(id) {
  const { data } = await api.delete(`/students/${id}`);
  return data;
}

export async function toggleStudentStatus(id) {
  const { data } = await api.patch(`/students/${id}/toggle-status`);
  return data;
}

export async function fetchStudentStatistics(params = {}) {
  const { data } = await api.get("/students/statistics", { params });
  return data;
}

export async function exportStudentCsv(params = {}) {
  const { data } = await api.get("/students/export/csv", { params });
  return data;
}

export async function exportStudentExcel(params = {}) {
  const { data } = await api.get("/students/export-excel", { params });
  return data;
}
