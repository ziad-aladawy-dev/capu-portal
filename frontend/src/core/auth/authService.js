import api from "../api/apiClient";
import { queryClient } from "../query/queryClient";

const ACTION_LEVEL_MAP = {
  "None": 0,
  "View": 1,
  "Insert": 2,
  "EditClose": 3,
  "Edit": 3,
  "Open": 4,
  "Delete": 5,
};

function transformApiPermissions(apiPermissions) {
  return apiPermissions.map((p) => {
    const resource = `${p.module}.${p.resource}.${p.action}`.toLowerCase();
    const level = ACTION_LEVEL_MAP[p.action] || 0;
    const scope = p.scope || null;
    return { resource, level, scope };
  });
}

const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

// Students are context-scoped on the backend: GetBootstrapContextAsync returns
// an EMPTY permission list for them by design ("act inside the user's
// StructureNode"). The student portal's route guards still check student.*
// resources, so we synthesize those grants client-side. Real enforcement
// happens server-side per request.
const STUDENT_PORTAL_RESOURCES = [
  "student.dashboard", "student.profile", "student.courses", "student.grades",
  "student.schedule", "student.services", "student.requests",
];

function decodeTokenRole(token) {
  try {
    const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const payload = JSON.parse(atob(base64));
    return payload[ROLE_CLAIM] || null;
  } catch {
    return null;
  }
}

function resolvePermissions(apiPermissions, token) {
  const transformed = apiPermissions ? transformApiPermissions(apiPermissions) : [];
  if (transformed.length === 0 && decodeTokenRole(token) === "Student") {
    return STUDENT_PORTAL_RESOURCES.map((base) => ({ resource: base, level: 5, scope: null }));
  }
  return transformed;
}

export async function login(identifier, password) {
  const { data } = await api.post("/auth/login", { identifier, password });
  api.setToken(data.token);
  if (data.refreshToken) api.setRefreshToken(data.refreshToken);
  // A fresh sign-in must not inherit the previous user's pinned selection or
  // cached query data (e.g. an admin's directory results leaking into a
  // student session).
  queryClient.clear();
  try {
    localStorage.removeItem("capu_pinned_user");
    window.dispatchEvent(new CustomEvent("capu:auth-changed"));
  } catch { /* ignore */ }

  return {
    user: data.user,
    token: data.token,
    refreshToken: data.refreshToken,
    permissions: resolvePermissions(data.permissions, data.token),
    authorizedScopes: data.authorizedScopes || {
      allowedNodeIds: [],
      allowedAcademicYearIds: [],
      allowedSemesterIds: [],
    },
    activeScope: data.activeScope || {
      structural: { nodeId: null },
      temporal: { academicYearId: null, semesterId: null },
    },
    passwordExpiryDate: data.passwordExpiryDate ?? null,
    requiresPasswordChange: data.requiresPasswordChange ?? false,
  };
}

export async function logout() {
  try { await api.post("/auth/logout"); } catch { }
  api.clearTokens();
  queryClient.clear();
  try {
    localStorage.removeItem("capu_pinned_user");
    window.dispatchEvent(new CustomEvent("capu:auth-changed"));
  } catch { /* ignore */ }
}

export async function getCurrentUser() {
  try {
    const { data } = await api.get("/auth/me");
    return {
      ...data.user,
      permissions: resolvePermissions(data.permissions, api.getToken()),
      authorizedScopes: data.authorizedScopes || {
        allowedNodeIds: [],
        allowedAcademicYearIds: [],
        allowedSemesterIds: [],
      },
      activeScope: data.activeScope || {
        structural: { nodeId: null },
        temporal: { academicYearId: null, semesterId: null },
      },
      passwordExpiryDate: data.passwordExpiryDate ?? null,
      requiresPasswordChange: data.requiresPasswordChange ?? false,
    };
  } catch {
    return null;
  }
}

export async function forgotPassword(identifier) {
  const response = await api.post("/auth/forgot-password", { identifier });
  return response.data;
}

export async function resetPassword(token, newPassword) {
  const response = await api.post("/auth/reset-password", { token, newPassword });
  return response.data;
}
