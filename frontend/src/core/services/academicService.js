import api from "../api/apiClient";

export async function fetchAcademicYears() {
  const { data } = await api.get("/academic-years");
  return data;
}

export async function fetchCurrentAcademicYear() {
  const allYears = await fetchAcademicYears();
  return (allYears || []).find((y) => y.isCurrent) || (allYears || [])[0] || null;
}

export async function fetchAcademicYear(id) {
  const { data } = await api.get(`/academic-years/${id}`);
  return data;
}

export async function createAcademicYear(body) {
  const { data } = await api.post("/academic-years", body);
  return data;
}

export async function updateAcademicYear(id, body) {
  const { data } = await api.patch(`/academic-years/${id}`, body);
  return data;
}

export async function deleteAcademicYear(id) {
  const { data } = await api.delete(`/academic-years/${id}`);
  return data;
}

export async function fetchSemesters(academicYearId) {
  const { data } = await api.get(`/academic-years/${academicYearId}/semesters`);
  return data;
}

export async function fetchCurrentSemester() {
  const { data } = await api.get("/semesters/current");
  return data;
}

export async function fetchSemester(id) {
  const { data } = await api.get(`/semesters/${id}`);
  return data;
}

export async function createSemester(body) {
  const { data } = await api.post("/semesters", body);
  return data;
}

export async function updateSemester(id, body) {
  const { data } = await api.patch(`/semesters/${id}`, body);
  return data;
}

export async function deleteSemester(id) {
  const { data } = await api.delete(`/semesters/${id}`);
  return data;
}

export async function closeSemester(id) {
  const { data } = await api.post(`/semesters/${id}/close-record`);
  return data;
}

export async function openSemester(id) {
  const { data } = await api.post(`/semesters/${id}/open-record`);
  return data;
}

export async function recomputeCurrentSemester() {
  const { data } = await api.post("/semesters/recompute-current");
  return data;
}

export async function closeAcademicYear(id) {
  const { data } = await api.post(`/academic-years/${id}/close-record`);
  return data;
}

export async function reopenAcademicYear(id) {
  const { data } = await api.post(`/academic-years/${id}/open-record`);
  return data;
}

export async function resolveCurrentAcademicYear() {
  const { data } = await api.post("/academic-years/recompute-current");
  return data;
}
