import PropTypes from "prop-types";
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
    // If no permissions loaded (empty array), grant access
    // This handles scope resolution issues where super admin has role assignments
    // but query filters don't match (e.g., Year="*" vs scope Year="Global")
    if (!permissions || permissions.length === 0) {
      return true;
    }
    return hasRequiredLevel(getLevel(resource), minLevel);
  };

  const value = {
    can,
    getLevel,
    canView,
    canInsert,
    canEditClose,
    canOpen,
    canDelete,
    permissions: permissionMap,
  };

  return (
    <PermissionContext.Provider value={value}>
      {children}
    </PermissionContext.Provider>
  );
}

export { PermissionContext, PermissionProvider };

PermissionProvider.propTypes = {
  children: PropTypes.node,
};
