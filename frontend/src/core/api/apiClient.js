import axios from "axios";

let onUnauthorizedCallback = null;
let isRefreshing = false;
let failedQueue = [];

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
};

apiClient.setToken = (token) => {
  if (token) localStorage.setItem("accessToken", token);
};

apiClient.setRefreshToken = (token) => {
  if (token) localStorage.setItem("refreshToken", token);
};

const processQueue = (error, token = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

apiClient.interceptors.request.use((config) => {
  const token = apiClient.getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  // Auto-attach scope context from localStorage to every request
  try {
    const scopeNode = JSON.parse(localStorage.getItem("capu_selected_scope_node"));
    const academicYear = JSON.parse(localStorage.getItem("capu_selected_academic_year"));
    const semester = JSON.parse(localStorage.getItem("capu_selected_semester"));
    const params = {};
    if (scopeNode?.id) params.ScopeNodeId = scopeNode.id;
    if (academicYear?.id) params.AcademicYearId = academicYear.id;
    if (semester?.id) params.SemesterId = semester.id;
    if (Object.keys(params).length > 0) {
      config.params = { ...config.params, ...params };
    }
  } catch {}
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

      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return apiClient(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = apiClient.getRefreshToken();
      if (!refreshToken) {
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

        processQueue(null, newToken);

        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
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
    }

    return Promise.reject(error);
  }
);

export default apiClient;