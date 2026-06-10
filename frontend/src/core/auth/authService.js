import api from "../api/apiClient";

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

export async function login(identifier, password) {
  const { data } = await api.post("/auth/login", { identifier, password });
  api.setToken(data.token);
  if (data.refreshToken) api.setRefreshToken(data.refreshToken);
  // A fresh sign-in must not inherit the previous user's pinned selection.
  try {
    localStorage.removeItem("capu_pinned_user");
    window.dispatchEvent(new CustomEvent("capu:auth-changed"));
  } catch { /* ignore */ }

  return {
    user: data.user,
    token: data.token,
    refreshToken: data.refreshToken,
    permissions: data.permissions ? transformApiPermissions(data.permissions) : [],
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
      permissions: data.permissions ? transformApiPermissions(data.permissions) : [],
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
