import { usePermission } from "./usePermission";

function PermissionGate({ resource, minLevel = 1, fallback = null, children }) {
  const { can } = usePermission();

  if (!can(resource, minLevel)) {
    return fallback;
  }

  return children;
}

export function PermissionGateWithLevel({ resource, render, children }) {
  const { getLevel } = usePermission();
  const level = getLevel(resource);

  if (render) {
    return render(level);
  }

  return typeof children === "function" ? children(level) : children;
}

export default PermissionGate;
