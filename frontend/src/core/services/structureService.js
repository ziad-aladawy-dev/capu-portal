import api from "../api/apiClient";

export async function fetchFaculties() {
  return api.get("/api/structure/lookups/faculties");
}

export async function fetchPrograms() {
  return api.get("/api/structure/lookups/programs");
}

export async function fetchLevels() {
  return api.get("/api/structure/lookups/levels");
}

export async function fetchDepartments() {
  return api.get("/api/structure/lookups/departments");
}

export async function fetchSpecializations() {
  return api.get("/api/structure/lookups/specializations");
}

export async function fetchSystems() {
  return api.get("/api/structure/lookups/systems");
}

export async function fetchChildNodes(parentId) {
  return api.get(`/api/structure/lookups/${parentId}/children`);
}

export async function fetchChildNodesByType(parentId, type) {
  return api.get(`/api/structure/lookups/${parentId}/children/${type}`);
}

export async function fetchStructureTree() {
  return api.get("/api/university-structure/tree");
}

export async function fetchStructureNode(id) {
  return api.get(`/api/university-structure/${id}`);
}

export async function createStructureNode(data) {
  return api.post("/api/university-structure", data);
}

export async function updateStructureNode(id, data) {
  return api.put(`/api/university-structure/${id}`, data);
}

export async function deleteStructureNode(id) {
  return api.delete(`/api/university-structure/${id}`);
}

export async function moveStructureNode(id, data) {
  return api.put(`/api/university-structure/move/${id}`, data);
}

export async function reorderStructureNode(id, data) {
  return api.put(`/api/university-structure/reorder/${id}`, data);
}

export async function fetchStructureRoots() {
  return api.get("/api/university-structure/roots");
}

export async function fetchStructureChildren(id) {
  return api.get(`/api/university-structure/children/${id}`);
}

export async function fetchStructureBreadcrumb(id) {
  return api.get(`/api/university-structure/breadcrumb/${id}`);
}

export async function fetchStructureSubtree(id) {
  return api.get(`/api/university-structure/subtree/${id}`);
}

export async function fetchStructureAncestors(id) {
  return api.get(`/api/university-structure/ancestors/${id}`);
}
