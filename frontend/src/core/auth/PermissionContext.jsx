import { createContext, useCallback, useMemo } from "react";
import { useAuth } from "./useAuth";
import { useDomain } from "../contexts/DomainContext";
import { useAcademic } from "../contexts/AcademicContext";

const PermissionContext = createContext(null);

function hasRequiredLevel(userLevel, minLevel) {
  return userLevel >= minLevel;
}

function matchScope(p, scopeNode, selectedYearObj, selectedSemesterObj) {
  if (!p.scope) return true;

  // Structural: pass when no filter, global, or matches selected node
  const structuralOk =
    !scopeNode ||
    !p.scope ||
    p.scope.isGlobalStructural ||
    (p.scope.structureNodeId && p.scope.structureNodeId === scopeNode.id);

  // Temporal year: pass when no filter, global, or matches selected year
  const yearOk =
    !selectedYearObj ||
    !p.scope ||
    p.scope.isGlobalYear ||
    (p.scope.academicYearId && p.scope.academicYearId === selectedYearObj.id);

  // Temporal semester: pass when no filter, global, or matches selected semester
  const semOk =
    !selectedSemesterObj ||
    !p.scope ||
    p.scope.isGlobalSemester ||
    (p.scope.semesterId && p.scope.semesterId === selectedSemesterObj.id);

  return structuralOk && yearOk && semOk;
}

function PermissionProvider({ children }) {
  const { permissions } = useAuth();
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();

  const permissionMap = useMemo(() => {
    const map = {};
    if (permissions && Array.isArray(permissions)) {
      for (const p of permissions) {
        if (!matchScope(p, scopeNode, selectedYearObj, selectedSemesterObj)) continue;
        map[p.resource] = p.level;
        const dot = p.resource.lastIndexOf('.');
        if (dot !== -1) {
          const baseKey = p.resource.substring(0, dot);
          map[baseKey] = Math.max(map[baseKey] || 0, p.level);
        }
      }
    }
    return map;
  }, [permissions, scopeNode, selectedYearObj, selectedSemesterObj]);

  // Stable identities: consumers (Sidebar menu, route guards) memoize on
  // `can`, so it must only change when the underlying permission map does.
  const getLevel = useCallback((resource) => {
    let level = permissionMap[resource] || 0;
    if (level > 0) return level;
    const dot = resource.lastIndexOf('.');
    if (dot !== -1) {
      const baseKey = resource.substring(0, dot);
      level = permissionMap[baseKey] || 0;
    }
    return level;
  }, [permissionMap]);

  const hasAnyPermissions = !!permissions && permissions.length > 0;

  const can = useCallback((resource, minLevel = 1) => {
    if (!hasAnyPermissions) {
      return false;
    }
    return hasRequiredLevel(getLevel(resource), minLevel);
  }, [hasAnyPermissions, getLevel]);

  const value = useMemo(() => ({
    can,
    getLevel,
    permissions: permissionMap,
  }), [can, getLevel, permissionMap]);

  return (
    <PermissionContext.Provider value={value}>
      {children}
    </PermissionContext.Provider>
  );
}

export { PermissionContext, PermissionProvider };
