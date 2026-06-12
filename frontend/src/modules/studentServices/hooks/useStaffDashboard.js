import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getStaffStatistics, getRecentRequests } from "../services/studentServicesService";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";

// Staff dashboard read-side. The apiClient interceptor stamps scope headers on
// every request, so the query key embeds the active scope (useScopeKeyPart
// pattern — see core/query/useDashboardStats.js). Mutations elsewhere in the
// module invalidate ["staff-dashboard"] to refresh these numbers.
export const useStaffDashboard = () => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ["staff-dashboard", scopePart],
    queryFn: async () => {
      const [stats, recent] = await Promise.all([
        getStaffStatistics(),
        getRecentRequests(5),
      ]);
      return { stats, recentRequests: Array.isArray(recent) ? recent : [] };
    },
    staleTime: 60 * 1000,
  });

  return {
    stats: query.data?.stats ?? null,
    recentRequests: query.data?.recentRequests ?? [],
    loading: query.isLoading,
    error: query.error?.message ?? null,
    refresh: () => queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] }),
  };
};
