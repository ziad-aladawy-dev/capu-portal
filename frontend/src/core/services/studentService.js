import api from "../api/apiClient";

export async function fetchAllStudents(params = {}) {
  return api.get("/api/students", params);
}

export async function fetchStudentById(id) {
  return api.get(`/api/students/${id}`);
}

export async function searchStudents(params = {}) {
  return api.get("/api/students/search", params);
}

export async function createStudent(data) {
  return api.post("/api/students", data);
}

export async function updateStudent(id, data) {
  return api.put(`/api/students/${id}`, data);
}

export async function deleteStudent(id) {
  return api.delete(`/api/students/${id}`);
}

export async function toggleStudentStatus(id) {
  return api.patch(`/api/students/${id}/toggle-status`);
}

export async function fetchStudentStatistics(params = {}) {
  return api.get("/api/students/statistics", params);
}

export async function exportStudentCsv(params = {}) {
  return api.get("/api/students/export/csv", params);
}

export async function exportStudentExcel(params = {}) {
  return api.get("/api/students/export-excel", params);
}

export async function bulkImportStudents(data) {
  return api.post("/api/students/bulk-import", data);
}

export async function importStudentExcel(file) {
  const formData = new FormData();
  formData.append("file", file);
  return api.post("/api/students/import-excel", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
}
