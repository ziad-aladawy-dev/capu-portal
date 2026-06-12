import axios from "axios";
import * as Sentry from "@sentry/react";

let onUnauthorizedCallback = null;
let isRefreshing = false;

const REFRESH_LOCK_KEY = "capu_refresh_lock";
const REFRESH_LOCK_TTL = 10000; // 10 seconds

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "http://localhost:5256/api",
  headers: {
    "Content-Type": "application/json",
  },
});

apiClient.setOnUnauthorized = (callback) => {
  onUnauthorizedCallback = callback;
};

apiClient.getToken = () => {
  return localStorage.getItem("accessToken");
};

apiClient.getRefreshToken = () => {
  return localStorage.getItem("refreshToken");
};

apiClient.clearTokens = () => {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
  localStorage.removeItem(REFRESH_LOCK_KEY);
};

apiClient.setToken = (token) => {
  if (token) localStorage.setItem("accessToken", token);
};

apiClient.setRefreshToken = (token) => {
  if (token) localStorage.setItem("refreshToken", token);
};


// Listen for token updates or errors from other tabs
if (typeof window !== "undefined") {
  window.addEventListener("storage", (e) => {
    if (e.key === "accessToken" && e.newValue) {
      isRefreshing = false;
    } else if (e.key === "capu_refresh_error" && e.newValue) {
      isRefreshing = false;
      const error = new Error("Token refresh failed in another tab");
    }
  });
}

apiClient.interceptors.request.use((config) => {
  if (config.skipScope) return config;
  const token = apiClient.getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  // Attach language header for i18n
  const lang = localStorage.getItem("i18nextLng") || "ar";
  config.headers["Accept-Language"] = lang;
  // Auto-attach scope context from storage to every request
  // Both query params AND headers are sent: query-params support existing
  // backend DTOs (StudentQueryRequest.ScopeNodeId etc.), while headers
  // support the IRequestContext interface (X-StructureNode-Id etc.) used
  // by permission and effective-scope services.
  try {
    const scopeNode = JSON.parse(sessionStorage.getItem("capu_selected_scope_node"));
    const academicYear = JSON.parse(sessionStorage.getItem("capu_selected_academic_year"));
    const semester = JSON.parse(sessionStorage.getItem("capu_selected_semester"));
    const params = {};
    if (scopeNode?.id) {
      params.ScopeNodeId = scopeNode.id;
      config.headers["X-StructureNode-Id"] = scopeNode.id;
    }
    if (academicYear?.id) {
      params.AcademicYearId = academicYear.id;
      config.headers["X-AcademicYear-Id"] = academicYear.id;
    }
    if (semester?.id) {
      params.SemesterId = semester.id;
      config.headers["X-Semester-Id"] = semester.id;
    }
    if (Object.keys(params).length > 0) {
      config.params = { ...config.params, ...params };
    }
    return config;
  } catch {
    // storage items may be absent or invalid
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      const path = window.location.pathname;
      if (path === "/admin/login" || path === "/student/login" || path === "/") {
        return Promise.reject(error);
      }

      // Session was revoked server-side (logged out elsewhere, password changed,
      // or refresh-token replay). The refresh token is revoked too, so a refresh
      // would only fail — log out immediately and surface the reason.
      if (error.response?.data?.reason === "session_revoked") {
        Sentry.captureMessage("Session revoked by server", {
          level: "info",
          extra: { path: window.location.pathname }
        });
        apiClient.clearTokens();
        if (onUnauthorizedCallback) {
          onUnauthorizedCallback("revoked");
        } else {
          window.location.href = "/admin/login?session=revoked";
        }
        return Promise.reject(error);
      }


      originalRequest._retry = true;

      // Fallback for browsers without navigator.locks
      const doRefresh = async () => {
        isRefreshing = true;
        localStorage.removeItem("capu_refresh_error");

        // Double-check if another tab refreshed the token while we were waiting for the lock
        const currentToken = apiClient.getToken();
        if (currentToken && currentToken !== originalRequest.headers.Authorization?.replace("Bearer ", "")) {
          isRefreshing = false;
          originalRequest.headers.Authorization = `Bearer ${currentToken}`;
          return apiClient(originalRequest);
        }

        const refreshToken = apiClient.getRefreshToken();
        if (!refreshToken) {
          Sentry.captureMessage("Refresh token missing from storage", { level: "warning" });
          apiClient.clearTokens();
          if (onUnauthorizedCallback) {
            onUnauthorizedCallback();
          } else {
            window.location.href = "/admin/login";
          }
          isRefreshing = false;
          return Promise.reject(error);
        }

        try {
          const { data } = await axios.post(
            `${apiClient.defaults.baseURL}/auth/refresh`,
            { refreshToken }
          );

          const newToken = data.token || data.accessToken;
          const newRefreshToken = data.refreshToken;

          apiClient.setToken(newToken);
          if (newRefreshToken) apiClient.setRefreshToken(newRefreshToken);


          originalRequest.headers.Authorization = `Bearer ${newToken}`;
          return apiClient(originalRequest);
        } catch (refreshError) {
          Sentry.captureException(refreshError, {
            tags: { type: "auth_refresh_failure" }
          });
          localStorage.setItem("capu_refresh_error", Date.now().toString());
                    apiClient.clearTokens();
          if (onUnauthorizedCallback) {
            onUnauthorizedCallback();
          } else {
            window.location.href = "/admin/login";
          }
          return Promise.reject(refreshError);
        } finally {
          isRefreshing = false;
        }
      };

      if (typeof navigator !== "undefined" && navigator.locks) {
        return navigator.locks.request(REFRESH_LOCK_KEY, { mode: "exclusive" }, async () => {
          return doRefresh();
        });
      } else {
        // Fallback for Safari <15.4 etc: Check a rudimentary lock
        const lockValue = localStorage.getItem(REFRESH_LOCK_KEY);
        const now = Date.now();
        if (lockValue && now - parseInt(lockValue) < REFRESH_LOCK_TTL) {
          isRefreshing = true;
          // Simple delay fallback since the queue was problematic
          return new Promise((resolve) => setTimeout(resolve, 1000)).then(() => {
             return apiClient(originalRequest);
          });
        }

        localStorage.setItem(REFRESH_LOCK_KEY, now.toString());
        try {
          return await doRefresh();
        } finally {
          localStorage.removeItem(REFRESH_LOCK_KEY);
        }
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;