import api from "../api/apiClient";

export async function fetchAllPermissions() {
  return api.get("/api/permissions");
}

export async function createPermission(data) {
  return api.post("/api/permissions", data);
}

export async function fetchPermissionAssignment(params = {}) {
  return api.get("/api/permissions/assignment", params);
}

export async function createPermissionAssignment(data) {
  return api.post("/api/permissions", data);
}

export async function updatePermissionAssignment(data) {
  return api.put("/api/permissions/assignment", data);
}

export async function fetchAllRoles(params = {}) {
  return api.get("/api/roles", params);
}

export async function fetchRoleById(id) {
  return api.get(`/api/roles/${id}`);
}

export async function createRole(data) {
  return api.post("/api/roles", data);
}

export async function updateRole(id, data) {
  return api.put(`/api/roles/${id}`, data);
}

export async function deleteRole(id) {
  return api.delete(`/api/roles/${id}`);
}

export async function fetchPermissionTree() {
  return api.get("/api/authorization/permissions/tree");
}

export async function fetchRolePermissions(roleId) {
  return api.get(`/api/authorization/roles/${roleId}/permissions`);
}
