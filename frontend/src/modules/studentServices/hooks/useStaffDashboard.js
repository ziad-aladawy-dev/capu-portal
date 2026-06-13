import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getStaffStatistics, getRecentRequests, getAssignedToMe } from "../services/studentServicesService";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";

export const useStaffDashboard = () => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ["staff-dashboard", scopePart],
    queryFn: async () => {
      const [stats, recent, assigned] = await Promise.all([
        getStaffStatistics(),
        getRecentRequests(5),
        getAssignedToMe(),
      ]);
      return {
        stats,
        recentRequests: Array.isArray(recent) ? recent : [],
        assignedToMeCount: Array.isArray(assigned) ? assigned.length : 0,
      };
    },
    staleTime: 60_000,
  });

  return {
    stats: query.data?.stats ?? null,
    recentRequests: query.data?.recentRequests ?? [],
    assignedToMeCount: query.data?.assignedToMeCount ?? 0,
    loading: query.isLoading,
    error: query.error?.message ?? null,
    refresh: () => queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] }),
  };
};
