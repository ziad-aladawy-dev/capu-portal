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
    return { resource, level };
  });
}

export async function login(identifier, password) {
  const response = await api.post("/auth/login", { identifier, password });
  api.setToken(response.token);

  return {
    user: response.user,
    token: response.token,
    permissions: response.permissions ? transformApiPermissions(response.permissions) : [],
    authorizedScopes: response.authorizedScopes || {
      allowedNodeIds: [],
      allowedAcademicYearIds: [],
      allowedSemesterIds: [],
    },
    activeScope: response.activeScope || {
      structural: { nodeId: null },
      temporal: { academicYearId: null, semesterId: null },
    },
  };
}

export async function logout() {
  try { await api.post("/auth/logout"); } catch { }
  api.clearTokens();
}

export async function getCurrentUser() {
  try {
    const response = await api.get("/auth/me");
    return {
      ...response.user,
      permissions: response.permissions ? transformApiPermissions(response.permissions) : [],
      authorizedScopes: response.authorizedScopes || {
        allowedNodeIds: [],
        allowedAcademicYearIds: [],
        allowedSemesterIds: [],
      },
      activeScope: response.activeScope || {
        structural: { nodeId: null },
        temporal: { academicYearId: null, semesterId: null },
      },
    };
  } catch {
    return null;
  }
}
