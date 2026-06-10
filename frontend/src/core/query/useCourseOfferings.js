import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as courseOfferingService from "../services/courseOfferingService";

const OFFERINGS_KEY = ["course-offerings"];

export function useCourseOfferings(params = {}) {
  return useQuery({
    queryKey: ["course-offerings", params],
    queryFn: () => courseOfferingService.searchCourseOfferings({
      Page: params.page || 1,
      PageSize: params.pageSize || 20,
      StructureNodeId: params.structureNodeId,
      SemesterId: params.semesterId,
      Search: params.search || undefined,
      Status: params.status !== undefined && params.status !== "" ? Number(params.status) : undefined,
    }),
    select: (data) => {
      const items = data?.items || data || [];
      return {
        items: Array.isArray(items) ? items : [],
        totalCount: data?.totalCount || 0,
        totalPages: data?.totalPages || 1,
      };
    },
    enabled: !!params.structureNodeId && !!params.semesterId,
  });
}

export function useCreateCourseOffering() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => courseOfferingService.createCourseOffering(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
}

export function useUpdateCourseOffering() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => courseOfferingService.updateCourseOffering(id, body),
    onMutate: async ({ id, ...body }) => {
      await qc.cancelQueries({ queryKey: OFFERINGS_KEY });
      const queries = qc.getQueriesData({ queryKey: OFFERINGS_KEY });
      const previous = queries.map(([key, data]) => ({ key, data }));
      for (const [key, data] of queries) {
        if (!data) continue;
        qc.setQueryData(key, (old) => {
          if (!old) return old;
          const items = Array.isArray(old.items) ? old.items : [];
          return {
            ...old,
            items: items.map((o) => (o.id === id ? { ...o, ...body } : o)),
          };
        });
      }
      return { previous };
    },
    onError: (err, vars, context) => {
      for (const { key, data } of context?.previous || []) {
        qc.setQueryData(key, data);
      }
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
}

export function useToggleOfferingLifecycle() {
  const qc = useQueryClient();
  const closeMut = useMutation({
    mutationFn: (id) => courseOfferingService.closeCourseOffering(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
  const openMut = useMutation({
    mutationFn: (id) => courseOfferingService.openCourseOffering(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
  return { closeMut, openMut };
}

export function useBulkPublishOfferings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids) => courseOfferingService.bulkPublishOfferings(ids),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
}

export function useBulkCancelOfferings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ ids, reason }) => courseOfferingService.bulkCancelOfferings(ids, reason),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: OFFERINGS_KEY });
    },
  });
}
