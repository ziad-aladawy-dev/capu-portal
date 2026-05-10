import { useState, useCallback } from "react";

export const usePermissions = (initialPermissions = []) => {
  const [permissions, setPermissions] = useState(initialPermissions);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  /**
   * Update a single permission's level
   */
  const updatePermission = useCallback((module, resource, newLevel) => {
    setPermissions(prev => 
      prev.map(p => 
        p.module === module && p.resource === resource 
          ? { ...p, level: newLevel }
          : p
      )
    );
    setHasUnsavedChanges(true);
  }, []);

  /**
   * Update scope for a specific permission
   */
  const updatePermissionScope = useCallback((module, resource, newScope) => {
    setPermissions(prev =>
      prev.map(p =>
        p.module === module && p.resource === resource
          ? { ...p, scope: newScope }
          : p
      )
    );
    setHasUnsavedChanges(true);
  }, []);

  /**
   * Set all resources in a module to a specific level (bulk action)
   */
  const setModuleLevel = useCallback((module, level) => {
    setPermissions(prev =>
      prev.map(p =>
        p.module === module
          ? { ...p, level }
          : p
      )
    );
    setHasUnsavedChanges(true);
  }, []);

  /**
   * Add a new permission override
   */
  const addPermissionOverride = useCallback((module, resource, level, overrideType = "added") => {
    const newPermission = {
      module,
      resource,
      level,
      isOverride: true,
      overrideType
    };
    setPermissions(prev => [...prev, newPermission]);
    setHasUnsavedChanges(true);
  }, []);

  /**
   * Remove a permission override
   */
  const removePermissionOverride = useCallback((module, resource) => {
    setPermissions(prev =>
      prev.filter(p => !(p.module === module && p.resource === resource && p.isOverride))
    );
    setHasUnsavedChanges(true);
  }, []);

  /**
   * Get all permissions for a specific module
   */
  const getModulePermissions = useCallback((module) => {
    return permissions.filter(p => p.module === module);
  }, [permissions]);

  /**
   * Get the highest permission level in a module
   */
  const getModuleMaxLevel = useCallback((module) => {
    const modulePerms = getModulePermissions(module);
    return modulePerms.length > 0
      ? Math.max(...modulePerms.map(p => p.level))
      : 0;
  }, [getModulePermissions]);

  /**
   * Check if a permission exists and has a certain level
   */
  const hasPermissionLevel = useCallback((module, resource, requiredLevel) => {
    const perm = permissions.find(p => p.module === module && p.resource === resource);
    return perm ? perm.level >= requiredLevel : false;
  }, [permissions]);

  /**
   * Save permissions (typically would call backend API)
   */
  const savePermissions = useCallback(async () => {
    try {
      // TODO: Call backend API to save permissions
      // const response = await api.updatePermissions(permissions);
      // if (response.ok) {
      setHasUnsavedChanges(false);
      // }
      return true;
    } catch (error) {
      console.error("Failed to save permissions:", error);
      return false;
    }
  }, []);

  /**
   * Reset changes (discard unsaved changes)
   */
  const resetChanges = useCallback(() => {
    setPermissions(initialPermissions);
    setHasUnsavedChanges(false);
  }, [initialPermissions]);

  /**
   * Get permission object
   */
  const getPermission = useCallback((module, resource) => {
    return permissions.find(p => p.module === module && p.resource === resource);
  }, [permissions]);

  return {
    permissions,
    hasUnsavedChanges,
    setHasUnsavedChanges,
    updatePermission,
    updatePermissionScope,
    setModuleLevel,
    addPermissionOverride,
    removePermissionOverride,
    getModulePermissions,
    getModuleMaxLevel,
    hasPermissionLevel,
    getPermission,
    savePermissions,
    resetChanges
  };
};
