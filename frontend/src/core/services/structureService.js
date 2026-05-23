import api from "../api/apiClient";

export async function fetchFaculties() {
  const { data } = await api.get("/structure/lookups/faculties");
  return data;
}

export async function fetchPrograms() {
  const { data } = await api.get("/structure/lookups/programs");
  return data;
}

export async function fetchLevels() {
  const { data } = await api.get("/structure/lookups/levels");
  return data;
}

export async function fetchDepartments() {
  const { data } = await api.get("/structure/lookups/departments");
  return data;
}

export async function fetchSpecializations() {
  const { data } = await api.get("/structure/lookups/specializations");
  return data;
}

export async function fetchChildNodes(parentId) {
  const { data } = await api.get(`/structure/lookups/${parentId}/children`);
  return data;
}

export async function fetchChildNodesByType(parentId, type) {
  const { data } = await api.get(`/structure/lookups/${parentId}/children/${type}`);
  return data;
}

export async function fetchStructureTree() {
  const { data } = await api.get("/university-structure/tree");
  return data;
}

export async function fetchStructureNode(id) {
  const { data } = await api.get(`/university-structure/${id}`);
  return data;
}

export async function createStructureNode(body) {
  const { data } = await api.post("/university-structure", body);
  return data;
}

export async function updateStructureNode(id, body) {
  const { data } = await api.put(`/university-structure/${id}`, body);
  return data;
}

export async function deleteStructureNode(id) {
  const { data } = await api.delete(`/university-structure/${id}`);
  return data;
}

export async function moveStructureNode(id, body) {
  const { data } = await api.put(`/university-structure/move/${id}`, body);
  return data;
}

export async function reorderStructureNode(id, body) {
  const { data } = await api.put(`/university-structure/reorder/${id}`, body);
  return data;
}

export async function fetchStructureRoots() {
  const { data } = await api.get("/university-structure/roots");
  return data;
}

export async function fetchStructureChildren(id) {
  const { data } = await api.get(`/university-structure/children/${id}`);
  return data;
}

export async function fetchStructureBreadcrumb(id) {
  const { data } = await api.get(`/university-structure/breadcrumb/${id}`);
  return data;
}

export async function fetchStructureSubtree(id) {
  const { data } = await api.get(`/university-structure/subtree/${id}`);
  return data;
}

export async function fetchStructureAncestors(id) {
  const { data } = await api.get(`/university-structure/ancestors/${id}`);
  return data;
}
