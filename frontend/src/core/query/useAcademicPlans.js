import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as academicPlanService from "../services/academicPlanService";

const PLANS_KEY = ["academic-plans"];

export function useAcademicPlans(params = {}) {
  return useQuery({
    queryKey: ["academic-plans", params],
    queryFn: () => academicPlanService.searchAcademicPlans({
      Page: params.page || 1,
      PageSize: params.pageSize || 20,
      StructureNodeId: params.structureNodeId,
      Search: params.search || undefined,
    }),
    select: (data) => {
      const items = data?.items || data || [];
      return {
        items: Array.isArray(items) ? items : [],
        totalCount: data?.totalCount || 0,
        totalPages: data?.totalPages || 1,
      };
    },
    enabled: !!params.structureNodeId,
  });
}

export function useAcademicPlan(id) {
  return useQuery({
    queryKey: ["academic-plan", id],
    queryFn: () => academicPlanService.fetchAcademicPlan(id),
    enabled: !!id,
  });
}

export function usePlanByStructure(structureNodeId) {
  return useQuery({
    queryKey: ["academic-plan", "by-structure", structureNodeId],
    queryFn: () => academicPlanService.fetchPlansForStructure(structureNodeId),
    enabled: !!structureNodeId,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateAcademicPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => academicPlanService.createAcademicPlan(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useUpdateAcademicPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => academicPlanService.updateAcademicPlan(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useDeleteAcademicPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicPlanService.deleteAcademicPlan(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useBulkDeleteAcademicPlans() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids) => academicPlanService.bulkDeleteAcademicPlans(ids),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useCloseAcademicPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicPlanService.closeAcademicPlan(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useOpenAcademicPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicPlanService.openAcademicPlan(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
    },
  });
}

export function useAddPlanCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, ...body }) => academicPlanService.addPlanCourse(planId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["academic-plan"] });
    },
  });
}

export function useRemovePlanCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, courseId }) => academicPlanService.removePlanCourse(planId, courseId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["academic-plan"] });
    },
  });
}

export function useBatchSetPlanCourses() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, courses }) => academicPlanService.batchUpdatePlanCourses(planId, courses),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PLANS_KEY });
      qc.invalidateQueries({ queryKey: ["academic-plan"] });
    },
  });
}
