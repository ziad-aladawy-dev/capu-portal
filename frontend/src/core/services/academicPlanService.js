import api from "../api/apiClient";

export async function fetchAcademicPlan(id) {
  const { data } = await api.get(`/academic-plans/${id}`);
  return data;
}

export async function fetchPlansForStructure(structureNodeId) {
  const { data } = await api.get(`/academic-plans/by-structure/${structureNodeId}`);
  return data;
}

export async function createAcademicPlan(body) {
  const { data } = await api.post("/academic-plans", body);
  return data;
}

export async function updateAcademicPlan(id, body) {
  const { data } = await api.patch(`/academic-plans/${id}`, body);
  return data;
}

export async function deleteAcademicPlan(id) {
  const { data } = await api.delete(`/academic-plans/${id}`);
  return data;
}

export async function addPlanCourse(planId, body) {
  const { data } = await api.post(`/academic-plans/${planId}/courses`, body);
  return data;
}

export async function removePlanCourse(planId, planCourseId) {
  const { data } = await api.delete(`/academic-plans/${planId}/courses/${planCourseId}`);
  return data;
}
