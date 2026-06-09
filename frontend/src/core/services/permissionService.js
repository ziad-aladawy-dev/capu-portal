import api from "../api/apiClient";

export async function fetchAllPermissions() {
  const { data } = await api.get("/permissions");
  return data;
}

export async function createPermission(body) {
  const { data } = await api.post("/permissions", body);
  return data;
}

export async function fetchPermissionAssignment(params = {}) {
  try {
    const { data } = await api.get("/permissions/assignment", { params });
    return data;
  } catch (err) {
    if (err.response?.status === 404) {
      return { roleAssignments: [], permissionOverrides: [] };
    }
    throw err;
  }
}

export async function createPermissionAssignment(body) {
  const { data } = await api.post("/permissions", body);
  return data;
}

export async function updatePermissionAssignment(body) {
  const { data } = await api.put("/permissions/assignment", body);
  return data;
}

export async function fetchAllRoles(params = {}) {
  const { data } = await api.get("/roles", { params });
  return data;
}

export async function fetchRoleById(id) {
  const { data } = await api.get(`/roles/${id}`);
  return data;
}

export async function createRole(body) {
  const { data } = await api.post("/roles", body);
  return data;
}

export async function updateRole(id, body) {
  const { data } = await api.put(`/roles/${id}`, body);
  return data;
}

export async function deleteRole(id) {
  const { data } = await api.delete(`/roles/${id}`);
  return data;
}

export async function fetchRoleMembers(roleId) {
  const { data } = await api.get(`/roles/${roleId}/members`);
  return data;
}

export async function addRoleMember(roleId, staffId) {
  const { data } = await api.post(`/roles/${roleId}/members`, { roleId, staffId });
  return data;
}

export async function removeRoleMember(roleId, staffId) {
  const { data } = await api.delete(`/roles/${roleId}/members/${staffId}`);
  return data;
}

export async function fetchRolePermissions(roleId) {
  const { data } = await api.get(`/authorization/roles/${roleId}/permissions`);
  return data;
}

export async function updateRolePermissions(roleId, body) {
  const { data } = await api.put(`/authorization/roles/${roleId}/permissions`, body);
  return data;
}
