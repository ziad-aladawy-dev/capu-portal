import { useQuery } from "@tanstack/react-query";
import * as notificationService from "../../../core/services/notificationService";
import { getStudentRequests } from "../../studentServices/services/studentServicesService";
import { REQUEST_STATUS } from "../constants/requestStatus";

// Statuses a student still has to care about — drive the Requests tab badge.
const OPEN_STATUSES = new Set([
  REQUEST_STATUS.Pending,
  REQUEST_STATUS.UnderReview,
  REQUEST_STATUS.MoreInfoRequired,
  REQUEST_STATUS.PaymentPending,
  REQUEST_STATUS.ReadyForPickup,
]);

/**
 * Badge counts for portal navigation (bottom nav + drawer).
 * Cheap polling cadence — SignalR push replaces this in a later phase.
 */
export function useNavBadges() {
  const { data: unreadCount = 0 } = useQuery({
    queryKey: ["portal", "badges", "notifications"],
    queryFn: async () => {
      const list = await notificationService.fetchUnreadNotifications();
      return Array.isArray(list) ? list.length : 0;
    },
    staleTime: 60 * 1000,
    refetchInterval: 120 * 1000,
    retry: 0,
  });

  const { data: openRequests = 0 } = useQuery({
    queryKey: ["portal", "badges", "requests"],
    queryFn: async () => {
      const list = await getStudentRequests();
      return Array.isArray(list) ? list.filter((r) => OPEN_STATUSES.has(r.status)).length : 0;
    },
    staleTime: 60 * 1000,
    refetchInterval: 120 * 1000,
    retry: 0,
  });

  return {
    notifications: unreadCount,
    requests: openRequests,
  };
}
