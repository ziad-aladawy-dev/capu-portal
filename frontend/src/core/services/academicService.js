import api from "../api/apiClient";

export async function fetchAcademicYears() {
  return api.get("/api/academic-years");
}

export async function fetchCurrentAcademicYear() {
  const allYears = await api.get("/api/academic-years");
  return (allYears || []).find((y) => y.isCurrent) || (allYears || [])[0] || null;
}

export async function fetchAcademicYear(id) {
  return api.get(`/api/academic-years/${id}`);
}

export async function createAcademicYear(data) {
  return api.post("/api/academic-years", data);
}

export async function updateAcademicYear(id, data) {
  return api.patch(`/api/academic-years/${id}`, data);
}

export async function deleteAcademicYear(id) {
  return api.delete(`/api/academic-years/${id}`);
}

export async function resolveCurrentAcademicYear() {
  return api.post("/api/academic-years/resolve");
}

export async function fetchSemesters(academicYearId) {
  return api.get(`/api/academic-years/${academicYearId}/semesters`);
}

export async function fetchCurrentSemester() {
  return api.get("/api/semesters/current");
}

export async function fetchSemester(id) {
  return api.get(`/api/semesters/${id}`);
}

export async function createSemester(data) {
  return api.post("/api/semesters", data);
}

export async function updateSemester(id, data) {
  return api.patch(`/api/semesters/${id}`, data);
}

export async function deleteSemester(id) {
  return api.delete(`/api/semesters/${id}`);
}

export async function resolveCurrentSemester() {
  return api.post("/api/semesters/resolve");
}
