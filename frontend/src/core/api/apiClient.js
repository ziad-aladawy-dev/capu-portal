import axios from "axios";
import i18n from "../i18n/i18n";

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "http://localhost:5256", 
  headers: {
    "Content-Type": "application/json",
  },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const language = i18n.language || localStorage.getItem('i18nextLng') || 'ar';
  config.headers['Accept-Language'] = language === 'en' ? 'en' : 'ar';

  const activeScope = localStorage.getItem("activeScope");
  if (activeScope) {
    try {
      const scope = JSON.parse(activeScope);
      if (scope.structural?.nodeId) {
        config.headers['X-StructureNode-Id'] = scope.structural.nodeId;
      }
      if (scope.temporal?.academicYearId) {
        config.headers['X-AcademicYear-Id'] = scope.temporal.academicYearId;
      }
      if (scope.temporal?.semesterId) {
        config.headers['X-Semester-Id'] = scope.temporal.semesterId;
      }
    } catch (e) {}
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const response = await apiClient.post('/api/auth/refresh');
        const newToken = response.data.token;
        localStorage.setItem("accessToken", newToken);
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("user");
        localStorage.removeItem("permissions");
        localStorage.removeItem("activeScope");
        const user = JSON.parse(localStorage.getItem("user") || "{}");
        const redirectUrl = user?.role === "Student" ? "/student/login" : "/admin/login";
        window.location.href = redirectUrl;
        return Promise.reject(refreshError);
      }
    }
    return Promise.reject(error);
  }
);

export default apiClient;