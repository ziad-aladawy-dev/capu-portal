import { useQuery } from "@tanstack/react-query";
import { getStaffAssignedRequests } from "../../modules/studentServices/services/studentServicesService";

export function useStaffAssignedWorkflows(staffId, options = {}) {
  return useQuery({
    queryKey: ["staff-assigned-workflows", staffId],
    queryFn: () => getStaffAssignedRequests(staffId),
    select: (data) => (Array.isArray(data) ? data : data?.items || []),
    enabled: !!staffId,
    ...options,
  });
}
