import {
  useInfiniteQuery, useQuery, useMutation, useQueryClient,
} from "@tanstack/react-query";
import {
  searchNotifications, fetchUnreadNotifications,
  markNotificationRead, markAllNotificationsRead,
} from "../../../core/services/notificationService";

const PAGE_SIZE = 20;
const UNREAD_POLL_MS = 60_000;

/** filter: "all" | "unread" | "read" → backend IsRead query param. */
function isReadParam(filter) {
  if (filter === "unread") return false;
  if (filter === "read") return true;
  return undefined;
}

export function useNotificationsInfinite(filter = "all") {
  return useInfiniteQuery({
    queryKey: ["notifications", "search", filter],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      searchNotifications({ page: pageParam, pageSize: PAGE_SIZE, isRead: isReadParam(filter) }),
    getNextPageParam: (last) =>
      last && last.page < last.totalPages ? last.page + 1 : undefined,
  });
}

/** Unread count, polled for near-real-time badge (no notifications WebSocket exists). */
export function useUnreadCount() {
  return useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: async () => {
      const data = await fetchUnreadNotifications();
      return Array.isArray(data) ? data.length : data?.items?.length || 0;
    },
    refetchInterval: UNREAD_POLL_MS,
    refetchOnWindowFocus: true,
  });
}

function useNotificationInvalidate() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ["notifications", "search"] });
    qc.invalidateQueries({ queryKey: ["notifications", "unread-count"] });
  };
}

export function useMarkRead() {
  const invalidate = useNotificationInvalidate();
  return useMutation({ mutationFn: markNotificationRead, onSuccess: invalidate });
}

export function useMarkAllRead() {
  const invalidate = useNotificationInvalidate();
  return useMutation({ mutationFn: markAllNotificationsRead, onSuccess: invalidate });
}
