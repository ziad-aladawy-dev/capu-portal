import React, { createContext, useState, useCallback } from "react";
import { mockUser, mockToken, mockRoles, mockRolePermissions } from "../lib/mock-data";

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(mockUser);
  const [token, setToken] = useState(mockToken);
  const [isAuthenticated, setIsAuthenticated] = useState(true);
  const [currentRole, setCurrentRole] = useState(mockRoles[0]); // Default: Super Admin
  const [permissions, setPermissions] = useState(
    mockRolePermissions[mockRoles[0].id] || []
  );
  const [moduleVisibility, setModuleVisibility] = useState([
    "Students",
    "Admin",
    "Financial",
    "Registration",
    "Permissions"
  ]); // Which modules this user can see

  const login = useCallback((email, password) => {
    // For now, mock login - always succeeds
    setUser(mockUser);
    setToken(mockToken);
    setIsAuthenticated(true);
  }, []);

  const logout = useCallback(() => {
    setUser(null);
    setToken(null);
    setIsAuthenticated(false);
  }, []);

  const updateUserRole = useCallback((roleId) => {
    const role = mockRoles.find(r => r.id === roleId);
    if (role) {
      setCurrentRole(role);
      setPermissions(mockRolePermissions[roleId] || []);
    }
  }, []);

  const value = {
    user,
    token,
    isAuthenticated,
    currentRole,
    permissions,
    moduleVisibility,
    login,
    logout,
    updateUserRole,
    setPermissions,
    setModuleVisibility
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
