const API_BASE = import.meta.env.VITE_API_BASE || "http://localhost:5256/api";

const TOKEN_KEY = "capu_token";
const REFRESH_KEY = "capu_refresh_token";

function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token);
}

function clearTokens() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
}

let onUnauthorized = null;

export function setOnUnauthorized(handler) {
  onUnauthorized = handler;
}

async function request(endpoint, options = {}) {
  const { method = "GET", body, params, headers: customHeaders, skipAuth = false } = options;

  const url = new URL(`${API_BASE}${endpoint}`, window.location.origin);

  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    });
  }

  const token = getToken();

  const headers = { ...customHeaders };

  if (!skipAuth) {
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  if (body && !(body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  try {
    const response = await fetch(url.toString(), {
      method,
      headers,
      body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
    });

    if (response.status === 401) {
      clearTokens();
      if (onUnauthorized) onUnauthorized();
      throw new ApiError("Unauthorized", 401);
    }

    if (response.status === 204) {
      return null;
    }

    const data = await response.json();

    if (!response.ok) {
      throw new ApiError(
        data.title || data.detail || data.message || `Request failed with status ${response.status}`,
        response.status,
        data
      );
    }

    return data;
  } catch (err) {
    if (err instanceof ApiError) throw err;
    throw new ApiError(err.message || "Network error", 0);
  }
}

class ApiError extends Error {
  constructor(message, status, data = null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
  }
}

const api = {
  get: (endpoint, params) => request(endpoint, { params }),
  post: (endpoint, body) => request(endpoint, { method: "POST", body }),
  put: (endpoint, body) => request(endpoint, { method: "PUT", body }),
  patch: (endpoint, body) => request(endpoint, { method: "PATCH", body }),
  delete: (endpoint) => request(endpoint, { method: "DELETE" }),
  getToken,
  setToken,
  clearTokens,
  ApiError,
  setOnUnauthorized,
};

export default api;
