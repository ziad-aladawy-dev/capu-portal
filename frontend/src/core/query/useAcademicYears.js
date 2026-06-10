import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as academicService from "../services/academicService";

const YEARS_KEY = ["academic-years"];

export function useAcademicYears() {
  return useQuery({
    queryKey: YEARS_KEY,
    queryFn: () => academicService.fetchAcademicYears(),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
  });
}

export function useAcademicYear(id) {
  return useQuery({
    queryKey: ["academic-year", id],
    queryFn: () => academicService.fetchAcademicYear(id),
    enabled: !!id,
  });
}

export function useCreateAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => academicService.createAcademicYear(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useUpdateAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => academicService.updateAcademicYear(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useDeleteAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicService.deleteAcademicYear(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useCloseAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicService.closeAcademicYear(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: YEARS_KEY });
      const previous = qc.getQueryData(YEARS_KEY);
      qc.setQueryData(YEARS_KEY, (old) => {
        const items = Array.isArray(old?.items || old) ? (old.items || old) : [];
        return items.map((y) => (y.id === id ? { ...y, isClosed: true } : y));
      });
      return { previous };
    },
    onError: (err, id, context) => {
      qc.setQueryData(YEARS_KEY, context?.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useReopenAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicService.reopenAcademicYear(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: YEARS_KEY });
      const previous = qc.getQueryData(YEARS_KEY);
      qc.setQueryData(YEARS_KEY, (old) => {
        const items = Array.isArray(old?.items || old) ? (old.items || old) : [];
        return items.map((y) => (y.id === id ? { ...y, isClosed: false } : y));
      });
      return { previous };
    },
    onError: (err, id, context) => {
      qc.setQueryData(YEARS_KEY, context?.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useSetCurrentAcademicYear() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => academicService.resolveCurrentAcademicYear(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: YEARS_KEY });
    },
  });
}

export function useSemesters(academicYearId) {
  return useQuery({
    queryKey: ["semesters", academicYearId],
    queryFn: () => academicService.fetchSemesters(academicYearId),
    enabled: !!academicYearId,
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
  });
}

export function useCreateSemester() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => academicService.createSemester(body),
    onSuccess: (data, variables) => {
      qc.invalidateQueries({ queryKey: ["semesters", variables.academicYearId] });
    },
  });
}

export function useUpdateSemester() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => academicService.updateSemester(id, body),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ["semesters"] });
    },
  });
}

export function useDeleteSemester() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, academicYearId }) => academicService.deleteSemester(id),
    onSuccess: (data, variables) => {
      qc.invalidateQueries({ queryKey: ["semesters", variables.academicYearId] });
    },
  });
}

export function useCloseSemester() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicService.closeSemester(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ["semesters"] });
    },
  });
}

export function useOpenSemester() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => academicService.openSemester(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ["semesters"] });
    },
  });
}
