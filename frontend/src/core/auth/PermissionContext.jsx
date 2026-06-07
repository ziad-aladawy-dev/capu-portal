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
        const dot = p.resource.lastIndexOf('.');
        if (dot !== -1) {
          const baseKey = p.resource.substring(0, dot);
          map[baseKey] = Math.max(map[baseKey] || 0, p.level);
        }
      }
    }
    return map;
  }, [permissions]);

  const getLevel = (resource) => {
    let level = permissionMap[resource] || 0;
    if (level > 0) return level;
    const dot = resource.lastIndexOf('.');
    if (dot !== -1) {
      const baseKey = resource.substring(0, dot);
      level = permissionMap[baseKey] || 0;
    }
    return level;
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
