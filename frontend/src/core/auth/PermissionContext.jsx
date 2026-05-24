import { createContext, useMemo } from "react";
import { useAuth } from "./useAuth";

const PermissionContext = createContext(null);

function hasRequiredLevel(userLevel, minLevel) {
  return userLevel >= minLevel;
}

function canView(level) {
  return level >= 1;
}

function canInsert(level) {
  return level >= 2;
}

function canEditClose(level) {
  return level >= 3;
}

function canOpen(level) {
  return level >= 4;
}

function canDelete(level) {
  return level >= 5;
}

function PermissionProvider({ children }) {
  const { permissions } = useAuth();

  const permissionMap = useMemo(() => {
    const map = {};
    if (permissions && Array.isArray(permissions)) {
      for (const p of permissions) {
        map[p.resource] = p.level;
      }
    }
    return map;
  }, [permissions]);

  const getLevel = (resource) => {
    return permissionMap[resource] || 0;
  };

  const can = (resource, minLevel = 1) => {
    if (!permissions || permissions.length === 0) {
      return false;
    }
    return hasRequiredLevel(getLevel(resource), minLevel);
  };

  const value = {
    can,
    getLevel,
    permissions: permissionMap,
  };

  return (
    <PermissionContext.Provider value={value}>
      {children}
    </PermissionContext.Provider>
  );
}

export { PermissionContext, PermissionProvider };
