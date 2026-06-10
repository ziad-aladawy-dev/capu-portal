import { createContext, useCallback, useEffect, useReducer } from "react";
import { useNavigate } from "react-router-dom";
import * as authService from "./authService";
import api from "../api/apiClient";

const AuthContext = createContext(null);

const initialState = {
  user: null,
  permissions: [],
  authorizedScopes: null,
  activeScope: null,
  isAuthenticated: false,
  isLoading: true,
  error: null,
  passwordExpiryDate: null,
  requiresPasswordChange: false,
};

function authReducer(state, action) {
  switch (action.type) {
    case "AUTH_START":
      return { ...state, isLoading: true, error: null };
    case "AUTH_SUCCESS":
      return {
        ...state,
        user: action.payload.user,
        permissions: action.payload.permissions || [],
        authorizedScopes: action.payload.authorizedScopes || null,
        activeScope: action.payload.activeScope || null,
        isAuthenticated: true,
        isLoading: false,
        error: null,
        passwordExpiryDate: action.payload.passwordExpiryDate ?? null,
        requiresPasswordChange: action.payload.requiresPasswordChange ?? false,
      };
    case "AUTH_FAILURE":
      return {
        ...state,
        user: null,
        permissions: [],
        authorizedScopes: null,
        activeScope: null,
        isAuthenticated: false,
        isLoading: false,
        error: action.payload,
      };
    case "AUTH_LOGOUT":
      return {
        ...state,
        user: null,
        permissions: [],
        authorizedScopes: null,
        activeScope: null,
        isAuthenticated: false,
        isLoading: false,
        error: null,
      };
    case "AUTH_LOADED":
      return { ...state, isLoading: false };
    default:
      return state;
  }
}

function AuthProvider({ children }) {
  const [state, dispatch] = useReducer(authReducer, initialState);
  const navigate = useNavigate();

  const handleLogout = useCallback((reason) => {
    authService.logout().finally(() => {
      dispatch({ type: "AUTH_LOGOUT" });
      const loginPath = window.location.pathname.startsWith("/student") ? "/student/login" : "/admin/login";
      const query = typeof reason === "string" && reason ? `?session=${reason}` : "";
      navigate(`${loginPath}${query}`);
    });
  }, [navigate]);

  useEffect(() => {
    api.setOnUnauthorized(handleLogout);
  }, [handleLogout]);

  useEffect(() => {
    const token = api.getToken();
    if (!token) {
      dispatch({ type: "AUTH_LOADED" });
      return;
    }

    authService
      .getCurrentUser()
      .then((result) => {
        if (result) {
          dispatch({
            type: "AUTH_SUCCESS",
            payload: {
              user: result,
              permissions: result.permissions || [],
              authorizedScopes: result.authorizedScopes,
              activeScope: result.activeScope,
              passwordExpiryDate: result.passwordExpiryDate,
              requiresPasswordChange: result.requiresPasswordChange,
            },
          });
        } else {
          api.clearTokens();
          dispatch({ type: "AUTH_LOADED" });
        }
      })
      .catch(() => {
        api.clearTokens();
        dispatch({ type: "AUTH_LOADED" });
      });
  }, []);

  const login = useCallback(async (identifier, password) => {
    dispatch({ type: "AUTH_START" });
    try {
      const result = await authService.login(identifier, password);
      dispatch({
        type: "AUTH_SUCCESS",
        payload: {
          user: result.user,
          permissions: result.permissions || [],
          authorizedScopes: result.authorizedScopes,
          activeScope: result.activeScope,
          passwordExpiryDate: result.passwordExpiryDate,
          requiresPasswordChange: result.requiresPasswordChange,
        },
      });
      return result;
    } catch (err) {
      dispatch({
        type: "AUTH_FAILURE",
        payload: err.message || "Login failed",
      });
      throw err;
    }
  }, []);

  const logout = useCallback((reason) => {
    handleLogout(reason);
  }, [handleLogout]);

  const value = {
    ...state,
    login,
    logout,
    user: state.user,
    permissions: state.permissions,
    authorizedScopes: state.authorizedScopes,
    activeScope: state.activeScope,
    passwordExpiryDate: state.passwordExpiryDate,
    requiresPasswordChange: state.requiresPasswordChange,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export { AuthContext, AuthProvider };
