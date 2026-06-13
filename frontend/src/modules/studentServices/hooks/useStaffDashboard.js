import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getStaffStatistics, getRecentRequests, getAssignedToMe, getRequestTrend } from "../services/studentServicesService";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";

export const useStaffDashboard = () => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ["staff-dashboard", scopePart],
    queryFn: async () => {
      const [stats, recent, assigned, trend] = await Promise.all([
        getStaffStatistics(),
        getRecentRequests(10),
        getAssignedToMe(),
        getRequestTrend(30),
      ]);
      return {
        stats,
        recentRequests: Array.isArray(recent) ? recent : [],
        assignedToMeCount: Array.isArray(assigned) ? assigned.length : 0,
        trend: Array.isArray(trend) ? trend : [],
      };
    },
    staleTime: 60_000,
  });

  return {
    stats: query.data?.stats ?? null,
    recentRequests: query.data?.recentRequests ?? [],
    assignedToMeCount: query.data?.assignedToMeCount ?? 0,
    trend: query.data?.trend ?? [],
    loading: query.isLoading,
    error: query.error?.message ?? null,
    refresh: () => queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] }),
  };
};
