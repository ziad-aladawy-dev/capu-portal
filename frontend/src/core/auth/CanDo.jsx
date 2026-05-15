import { usePermission } from "./usePermission";

function CanDo({ resource, minLevel = 1, fallback = null, children }) {
  const { can } = usePermission();

  if (!can(resource, minLevel)) {
    return fallback;
  }

  return children;
}

export default CanDo;
