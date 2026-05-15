import api from "../api/apiClient";

export async function fetchAcademicYears() {
  return api.get("/academic-years");
}

export async function fetchCurrentAcademicYear() {
  const allYears = await api.get("/academic-years");
  return (allYears || []).find((y) => y.isCurrent) || (allYears || [])[0] || null;
}

export async function fetchAcademicYear(id) {
  return api.get(`/academic-years/${id}`);
}

export async function createAcademicYear(data) {
  return api.post("/academic-years", data);
}

export async function updateAcademicYear(id, data) {
  return api.patch(`/academic-years/${id}`, data);
}

export async function deleteAcademicYear(id) {
  return api.delete(`/academic-years/${id}`);
}

export async function fetchSemesters(academicYearId) {
  return api.get(`/academic-years/${academicYearId}/semesters`);
}

export async function fetchCurrentSemester() {
  return api.get("/semesters/current");
}

export async function fetchSemester(id) {
  return api.get(`/semesters/${id}`);
}
