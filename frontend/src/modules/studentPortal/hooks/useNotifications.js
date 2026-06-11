import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as notificationService from "../../../core/services/notificationService";

const LIST_KEY = ["portal", "notifications"];
const BADGE_KEY = ["portal", "badges", "notifications"];

export function useNotifications() {
  return useQuery({
    queryKey: LIST_KEY,
    staleTime: 30_000,
    queryFn: async () => {
      const data = await notificationService.fetchAllNotifications();
      return Array.isArray(data) ? data : data?.items || [];
    },
  });
}

function patchCaches(qc, mutate) {
  qc.setQueryData(LIST_KEY, (old) => (Array.isArray(old) ? mutate(old) : old));
}

/** Optimistic single mark-read; rolls back on failure, then resyncs badge. */
export function useMarkRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => notificationService.markNotificationRead(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: LIST_KEY });
      const previous = qc.getQueryData(LIST_KEY);
      patchCaches(qc, (old) => old.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
      return { previous };
    },
    onError: (_e, _id, ctx) => {
      if (ctx?.previous) qc.setQueryData(LIST_KEY, ctx.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: LIST_KEY });
      qc.invalidateQueries({ queryKey: BADGE_KEY });
    },
  });
}

export function useMarkAllRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => notificationService.markAllNotificationsRead(),
    onMutate: async () => {
      await qc.cancelQueries({ queryKey: LIST_KEY });
      const previous = qc.getQueryData(LIST_KEY);
      patchCaches(qc, (old) => old.map((n) => ({ ...n, isRead: true })));
      return { previous };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.previous) qc.setQueryData(LIST_KEY, ctx.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: LIST_KEY });
      qc.invalidateQueries({ queryKey: BADGE_KEY });
    },
  });
}
