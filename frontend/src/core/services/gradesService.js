import api from "../api/apiClient";

export async function fetchGradeHistory() {
  const { data } = await api.get("/grades/history");
  return data;
}

export async function fetchSemesterGrades(semesterId) {
  const { data } = await api.get(`/grades/history/${semesterId}`);
  return data;
}

export async function fetchGradeSummary() {
  const { data } = await api.get("/grades/summary");
  return data;
}
