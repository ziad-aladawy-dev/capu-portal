import { createContext, useContext, useState, useEffect, useCallback } from "react";
import authService from "../auth/authService";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [permissions, setPermissions] = useState([]);
  const [activeScope, setActiveScope] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadStoredData = () => {
      const storedUser = authService.getCurrentUser();
      const storedPerms = authService.getPermissions();
      const storedScope = authService.getActiveScope();
      setUser(storedUser);
      setPermissions(storedPerms);
      setActiveScope(storedScope);
      setLoading(false);
    };
    loadStoredData();
  }, []);

  const login = async (credentials) => {
    const data = await authService.login(credentials);
    setUser(data.user);
    setPermissions(data.permissions);
    setActiveScope(data.activeScope);
    return data;
  };

  const logout = async () => {
    await authService.logout();
    setUser(null);
    setPermissions([]);
    setActiveScope(null);
  };

  const hasPermission = useCallback((module, resource, action) => {
    return authService.hasPermission(module, resource, action);
  }, []);

  const value = {
    user,
    permissions,
    activeScope,
    loading,
    login,
    logout,
    isAuthenticated: !!user,
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => useContext(AuthContext);