import { useMemo } from "react";
import { useLocation } from "react-router-dom";
import { useStickySelection } from "../contexts/StickySelectionContext";
import { getCurrentRouteInfo } from "../router/routeRegistry";
import { PAGE_TYPES } from "../manifests/manifestTypes";

export function useUserScope() {
  const location = useLocation();
  const { selected, select, clear, isActive } = useStickySelection();

  const routeInfo = useMemo(() => getCurrentRouteInfo(location.pathname), [location.pathname]);
  const pageType = routeInfo?.pageType || PAGE_TYPES.MANAGEMENT;

  return {
    scopedUser: isActive ? selected : null,
    isScoped: isActive,
    clearScope: clear,
    scopeToUser: select,
    pageType,
    isEntityPage: pageType === PAGE_TYPES.ENTITY,
    isManagementPage: pageType === PAGE_TYPES.MANAGEMENT,
  };
}
