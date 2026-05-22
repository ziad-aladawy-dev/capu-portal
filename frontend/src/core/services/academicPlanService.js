import api from "../api/apiClient";

export async function fetchAcademicPlan(id) {
  return api.get(`/academic-plans/${id}`);
}

export async function fetchPlansForStructure(structureNodeId) {
  return api.get(`/academic-plans/by-structure/${structureNodeId}`);
}

export async function createAcademicPlan(data) {
  return api.post("/academic-plans", data);
}

export async function updateAcademicPlan(id, data) {
  return api.patch(`/academic-plans/${id}`, data);
}

export async function deleteAcademicPlan(id) {
  return api.delete(`/academic-plans/${id}`);
}

export async function addPlanCourse(planId, data) {
  return api.post(`/academic-plans/${planId}/courses`, data);
}

export async function removePlanCourse(planId, planCourseId) {
  return api.delete(`/academic-plans/${planId}/courses/${planCourseId}`);
}
