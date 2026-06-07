import { useContext } from "react";
import { PermissionContext } from "./PermissionContext";

export function usePermission() {
  const context = useContext(PermissionContext);

  if (!context) {
    throw new Error("usePermission must be used within a PermissionProvider");
  }

  return context;
}

export function useCanDo(resource, minLevel = 1) {
  const { can } = usePermission();
  return can(resource, minLevel);
}

export function useResourceLevel(resource) {
  const { getLevel } = usePermission();
  return getLevel(resource);
}
