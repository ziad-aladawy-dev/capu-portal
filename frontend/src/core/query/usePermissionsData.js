import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as permissionService from "../services/permissionService";
import * as authorizationService from "../services/authorizationService";
import { useAuth } from "../auth/useAuth";
import { useScopeKeyPart } from "./scopedKeys";

// Roles and the permission catalog are global entities; effective user
// permissions vary with the active scope headers, so only those keys carry
// the scope part.
export const rolesKey = ["roles"];
export const permissionTreeKey = ["permission-tree"];
export const rolePermissionsKey = (roleId) => ["role-permissions", roleId];
export const roleMembersKey = (roleId) => ["role-members", roleId];
export const userPermissionTreeKey = (userId, scopePart) => ["user-permission-tree", userId, scopePart];
export const permissionAssignmentKey = (userId) => ["permission-assignment", userId];

export function useRoles(params = { pageSize: 200 }) {
  return useQuery({
    queryKey: [...rolesKey, params],
    queryFn: () => permissionService.fetchAllRoles(params),
    select: (data) => data?.items || [],
  });
}

export function usePermissionTree() {
  return useQuery({
    queryKey: permissionTreeKey,
    queryFn: () => authorizationService.fetchPermissionTree(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 5 * 60 * 1000,
  });
}

export function useRolePermissions(roleId, { enabled = true } = {}) {
  return useQuery({
    queryKey: rolePermissionsKey(roleId),
    queryFn: () => permissionService.fetchRolePermissions(roleId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!roleId && enabled,
  });
}

export function useRoleMembers(roleId, { enabled = true } = {}) {
  return useQuery({
    queryKey: roleMembersKey(roleId),
    queryFn: () => permissionService.fetchRoleMembers(roleId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!roleId && enabled,
  });
}

export function useUserPermissionTree(userId, { enabled = true } = {}) {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: userPermissionTreeKey(userId, scopePart),
    queryFn: () => authorizationService.fetchUserPermissionTree(userId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!userId && enabled,
  });
}

export function usePermissionAssignment(userId, { enabled = true } = {}) {
  return useQuery({
    queryKey: permissionAssignmentKey(userId),
    queryFn: () => permissionService.fetchPermissionAssignment({ userId, alwaysActive: true }),
    enabled: !!userId && enabled,
  });
}

export function useCreateRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => permissionService.createRole(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesKey }),
  });
}

export function useUpdateRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => permissionService.updateRole(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesKey }),
  });
}

export function useDeleteRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => permissionService.deleteRole(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesKey }),
  });
}

// Editing a role's permissions changes the effective permissions of every
// member, including possibly the current admin — refresh our own session
// permissions so the sidebar/menu react immediately (Scenario C).
export function useUpdateRolePermissions() {
  const qc = useQueryClient();
  const { refreshPermissions } = useAuth();
  return useMutation({
    mutationFn: ({ roleId, resources }) =>
      permissionService.updateRolePermissions(roleId, { resources }),
    onSuccess: (_data, { roleId }) => {
      qc.invalidateQueries({ queryKey: rolePermissionsKey(roleId) });
      qc.invalidateQueries({ queryKey: ["user-permission-tree"] });
      refreshPermissions?.();
    },
  });
}

export function useAddRoleMember() {
  const qc = useQueryClient();
  const { user, refreshPermissions } = useAuth();
  return useMutation({
    mutationFn: ({ roleId, staffId }) => permissionService.addRoleMember(roleId, staffId),
    onSuccess: (_data, { roleId, staffId }) => {
      qc.invalidateQueries({ queryKey: roleMembersKey(roleId) });
      qc.invalidateQueries({ queryKey: ["user-permission-tree", staffId] });
      if (staffId === user?.id) refreshPermissions?.();
    },
  });
}

export function useRemoveRoleMember() {
  const qc = useQueryClient();
  const { user, refreshPermissions } = useAuth();
  return useMutation({
    mutationFn: ({ roleId, staffId }) => permissionService.removeRoleMember(roleId, staffId),
    onSuccess: (_data, { roleId, staffId }) => {
      qc.invalidateQueries({ queryKey: roleMembersKey(roleId) });
      qc.invalidateQueries({ queryKey: ["user-permission-tree", staffId] });
      if (staffId === user?.id) refreshPermissions?.();
    },
  });
}

export function useUpdatePermissionAssignment() {
  const qc = useQueryClient();
  const { user, refreshPermissions } = useAuth();
  return useMutation({
    mutationFn: (body) => permissionService.updatePermissionAssignment(body),
    onSuccess: (_data, body) => {
      qc.invalidateQueries({ queryKey: ["user-permission-tree", body.userId] });
      qc.invalidateQueries({ queryKey: permissionAssignmentKey(body.userId) });
      if (body.userId === user?.id) refreshPermissions?.();
    },
  });
}
