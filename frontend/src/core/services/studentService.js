import api from "../api/apiClient";

export async function fetchAllStudents(params = {}) {
  return api.get("/students", params);
}

export async function fetchStudentById(id) {
  return api.get(`/students/${id}`);
}

export async function searchStudents(params = {}) {
  return api.get("/students/search", params);
}

export async function createStudent(data) {
  return api.post("/students", data);
}

export async function updateStudent(id, data) {
  return api.put(`/students/${id}`, data);
}

export async function deleteStudent(id) {
  return api.delete(`/students/${id}`);
}

export async function toggleStudentStatus(id) {
  return api.patch(`/students/${id}/toggle-status`);
}

export async function fetchStudentStatistics(params = {}) {
  return api.get("/students/statistics", params);
}

export async function exportStudentCsv(params = {}) {
  return api.get("/students/export/csv", params);
}

export async function exportStudentExcel(params = {}) {
  return api.get("/students/export-excel", params);
}
