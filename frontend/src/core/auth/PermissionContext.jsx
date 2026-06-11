import { createContext, useMemo } from "react";
import { useAuth } from "./useAuth";
import { useDomain } from "../contexts/DomainContext";
import { useAcademic } from "../contexts/AcademicContext";

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
  const { permissions, user } = useAuth();
  const isStudent = (user?.role || "").toLowerCase() === "student";
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
    // Students are context-scoped: they hold no explicit permission entries, but
    // are entitled to their own "student.*" surface. The backend enforces the
    // actual (self-scoped) data access; the client only needs to let the student
    // into their portal. Without this, a student's empty permission list makes
    // every student route fail can() and bounces them to the login with
    // ?session=unauthorized (the "unauthorized login loop").
    if (isStudent && typeof resource === "string" && resource.startsWith("student.")) {
      return true;
    }
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
