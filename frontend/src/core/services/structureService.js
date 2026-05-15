import api from "../api/apiClient";

export async function fetchFaculties() {
  return api.get("/structure/lookups/faculties");
}

export async function fetchPrograms() {
  return api.get("/structure/lookups/programs");
}

export async function fetchLevels() {
  return api.get("/structure/lookups/levels");
}

export async function fetchDepartments() {
  return api.get("/structure/lookups/departments");
}

export async function fetchSpecializations() {
  return api.get("/structure/lookups/specializations");
}

export async function fetchChildNodes(parentId) {
  return api.get(`/structure/lookups/${parentId}/children`);
}

export async function fetchChildNodesByType(parentId, type) {
  return api.get(`/structure/lookups/${parentId}/children/${type}`);
}

export async function fetchStructureTree() {
  return api.get("/university-structure/tree");
}

export async function fetchStructureNode(id) {
  return api.get(`/university-structure/${id}`);
}

export async function createStructureNode(data) {
  return api.post("/university-structure", data);
}

export async function updateStructureNode(id, data) {
  return api.put(`/university-structure/${id}`, data);
}

export async function deleteStructureNode(id) {
  return api.delete(`/university-structure/${id}`);
}

export async function moveStructureNode(id, data) {
  return api.put(`/university-structure/move/${id}`, data);
}

export async function reorderStructureNode(id, data) {
  return api.put(`/university-structure/reorder/${id}`, data);
}

export async function fetchStructureRoots() {
  return api.get("/university-structure/roots");
}

export async function fetchStructureChildren(id) {
  return api.get(`/university-structure/children/${id}`);
}

export async function fetchStructureBreadcrumb(id) {
  return api.get(`/university-structure/breadcrumb/${id}`);
}

export async function fetchStructureSubtree(id) {
  return api.get(`/university-structure/subtree/${id}`);
}

export async function fetchStructureAncestors(id) {
  return api.get(`/university-structure/ancestors/${id}`);
}
