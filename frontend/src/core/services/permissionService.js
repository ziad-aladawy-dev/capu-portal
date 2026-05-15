import api from "../api/apiClient";

export async function fetchAllPermissions() {
  return api.get("/permissions");
}

export async function createPermission(data) {
  return api.post("/permissions", data);
}

export async function fetchPermissionAssignment(params = {}) {
  return api.get("/permissions/assignment", params);
}

export async function createPermissionAssignment(data) {
  return api.post("/permissions", data);
}

export async function updatePermissionAssignment(data) {
  return api.put("/permissions/assignment", data);
}

export async function fetchAllRoles(params = {}) {
  return api.get("/roles", params);
}

export async function fetchRoleById(id) {
  return api.get(`/roles/${id}`);
}

export async function createRole(data) {
  return api.post("/roles", data);
}

export async function updateRole(id, data) {
  return api.put(`/roles/${id}`, data);
}

export async function deleteRole(id) {
  return api.delete(`/roles/${id}`);
}
