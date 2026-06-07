import { useMemo } from "react";
import { useDomain } from "../contexts/DomainContext";
import { getNodeCapabilities, getContextualActions } from "../../modules/university/utils/nodeTypeRegistry";

export function useScopeAwareUI() {
  const { scopeNode } = useDomain();

  return useMemo(() => {
    const type = scopeNode?.type;
    const capabilities = type ? getNodeCapabilities(type) : [];

    const hasCapability = (cap) => capabilities.includes(cap);

    const preferredUserTab = hasCapability("hasStaff") && !hasCapability("hasStudents")
      ? "staff"
      : hasCapability("hasStudents") && !hasCapability("hasStaff")
        ? "student"
        : "all";

    const relevantActions = type ? getContextualActions(type) : [];

    return {
      scopeNode,
      capabilities,
      hasCapability,
      preferredUserTab,
      relevantActions,
    };
  }, [scopeNode]);
}
