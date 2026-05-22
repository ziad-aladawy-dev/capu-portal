import api from "../api/apiClient";

export async function fetchPermissionTree() {
  return api.get("/authorization/permissions/tree");
}

export async function fetchRolePermissions(roleId) {
  return api.get(`/authorization/roles/${roleId}/permissions`);
}
