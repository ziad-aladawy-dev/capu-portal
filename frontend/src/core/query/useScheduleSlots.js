import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as scheduleService from "../services/scheduleService";

const SLOTS_KEY = ["schedule-slots"];

export function useScheduleSlots(offeringId) {
  return useQuery({
    queryKey: ["schedule-slots", offeringId],
    queryFn: () => scheduleService.fetchSlotsForOffering(offeringId),
    enabled: !!offeringId,
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
  });
}

export function useCreateSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => scheduleService.createSlot(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useUpdateSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => scheduleService.updateSlot(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useDeleteSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => scheduleService.deleteSlot(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: SLOTS_KEY });
      const queries = qc.getQueriesData({ queryKey: SLOTS_KEY });
      const previous = queries.map(([key, data]) => ({ key, data }));
      for (const [key, data] of queries) {
        if (!data) continue;
        qc.setQueryData(key, (old) => {
          if (!old) return old;
          return Array.isArray(old) ? old.filter((s) => s.id !== id) : old;
        });
      }
      return { previous };
    },
    onError: (err, id, context) => {
      for (const { key, data } of context?.previous || []) {
        qc.setQueryData(key, data);
      }
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useBatchCreateSlots() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ offeringId, slots }) => scheduleService.batchCreateSlots(offeringId, slots),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useCloseSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => scheduleService.closeSlot(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useOpenSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => scheduleService.openSlot(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: SLOTS_KEY });
    },
  });
}

export function useOfferingsForSchedule(structureNodeId, semesterId) {
  return useQuery({
    queryKey: ["offerings-for-schedule", structureNodeId, semesterId],
    queryFn: () => scheduleService.fetchOfferingsForSchedule(structureNodeId, semesterId),
    enabled: !!structureNodeId && !!semesterId,
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 60 * 1000,
  });
}
