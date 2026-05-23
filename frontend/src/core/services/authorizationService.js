import api from "../api/apiClient";

export async function fetchPermissionTree() {
  const { data } = await api.get("/authorization/permissions/tree");
  return data;
}

export async function fetchRolePermissions(roleId) {
  const { data } = await api.get(`/authorization/roles/${roleId}/permissions`);
  return data;
}
