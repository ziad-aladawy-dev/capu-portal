import axios from "axios";

let onUnauthorizedCallback = null;

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "http://localhost:5256",
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

apiClient.clearTokens = () => {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
};

apiClient.interceptors.request.use((config) => {
  const token = apiClient.getToken();

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      apiClient.clearTokens();

      if (onUnauthorizedCallback) {
        onUnauthorizedCallback();
      } else {
        window.location.href = "/admin/login";
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;