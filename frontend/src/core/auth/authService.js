import apiClient from "../api/apiClient";

const AUTH_BASE = "/api/auth";

const decodeToken = (token) => {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => {
      return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));
    return JSON.parse(jsonPayload);
  } catch (e) {
    console.error("Failed to decode token", e);
    return {};
  }
};

const authService = {
  login: async (credentials) => {
    const response = await apiClient.post(`${AUTH_BASE}/login`, {
      identifier: credentials.nationalId,
      password: credentials.password,
    });
    const { token, user, permissions, activeScope } = response.data;
    
    const decoded = decodeToken(token);
    const roleClaim = decoded["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || decoded["role"] || "";
    
    const userWithRole = {
      ...user,
      role: roleClaim,
      name: user.name ? (() => {
        try {
          const parsed = JSON.parse(user.name);
          return parsed;
        } catch { return { ar: user.name, en: user.name }; }
      })() : { ar: "", en: "" }
    };
    
    localStorage.setItem("accessToken", token);
    localStorage.setItem("user", JSON.stringify(userWithRole));
    localStorage.setItem("permissions", JSON.stringify(permissions));
    localStorage.setItem("activeScope", JSON.stringify(activeScope));
    
    return { ...response.data, user: userWithRole };
  },

  logout: async () => {
    try {
      await apiClient.post(`${AUTH_BASE}/logout`);
    } catch (err) {
      console.error("Logout API error:", err);
    } finally {
      localStorage.removeItem("accessToken");
      localStorage.removeItem("user");
      localStorage.removeItem("permissions");
      localStorage.removeItem("activeScope");
    }
  },

  refreshToken: async () => {
    const response = await apiClient.post(`${AUTH_BASE}/refresh`);
    const { token } = response.data;
    localStorage.setItem("accessToken", token);
    return token;
  },

  changePassword: async (currentPassword, newPassword) => {
    await apiClient.post(`${AUTH_BASE}/change-password`, {
      currentPassword,
      newPassword,
    });
  },

  getCurrentUser: () => {
    const userStr = localStorage.getItem("user");
    if (!userStr) return null;
    try {
      return JSON.parse(userStr);
    } catch {
      return null;
    }
  },

  getPermissions: () => {
    const permsStr = localStorage.getItem("permissions");
    if (!permsStr) return [];
    try {
      return JSON.parse(permsStr);
    } catch {
      return [];
    }
  },

  getActiveScope: () => {
    const scopeStr = localStorage.getItem("activeScope");
    if (!scopeStr) return null;
    try {
      return JSON.parse(scopeStr);
    } catch {
      return null;
    }
  },

  isAuthenticated: () => {
    const token = localStorage.getItem("accessToken");
    return !!token;
  },

  hasPermission: (moduleName, resourceName, action) => {
    const permissions = authService.getPermissions();
    return permissions.some(p => p.module === moduleName && p.resource === resourceName && p.action === action);
  },

  hasAnyPermission: (requiredPermissions) => {
    const permissions = authService.getPermissions();
    return requiredPermissions.some(req => 
      permissions.some(p => p.module === req.module && p.resource === req.resource && p.action === req.action)
    );
  }
};

export default authService;