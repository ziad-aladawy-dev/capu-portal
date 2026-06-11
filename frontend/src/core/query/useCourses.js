import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as courseService from "../services/courseService";

const COURSES_KEY = ["courses"];

export function useCourses(params = {}) {
  return useQuery({
    queryKey: ["courses", params],
    queryFn: () => courseService.searchCourses({
      Page: params.page || 1,
      PageSize: params.pageSize || 20,
      Search: params.search || undefined,
      Category: params.category !== undefined && params.category !== "" ? Number(params.category) : undefined,
      IsActive: params.isActive !== undefined ? params.isActive : undefined,
    }),
    select: (data) => {
      const items = data?.items || data || [];
      return {
        items: Array.isArray(items) ? items : [],
        totalCount: data?.totalCount || 0,
        totalPages: data?.totalPages || 1,
      };
    },
  });
}

export function useActiveCourses() {
  return useQuery({
    queryKey: ["courses", "active"],
    queryFn: () => courseService.fetchActiveCourses(),
    select: (data) => {
      const list = Array.isArray(data) ? data : [];
      return list;
    },
    staleTime: 5 * 60 * 1000,
  });
}

// ---- Prerequisite graph ----------------------------------------------------

const PREREQ_KEY = ["course-prerequisites"];

// Whole catalog edge list — powers dependency badges, client-side cycle
// previews and catalog health without per-course calls.
export function useAllPrerequisitePairs() {
  return useQuery({
    queryKey: PREREQ_KEY,
    queryFn: () => courseService.fetchAllPrerequisitePairs(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 60 * 1000,
  });
}

export function useCoursePrerequisites(courseId) {
  return useQuery({
    queryKey: ["course-prerequisites", courseId],
    queryFn: () => courseService.fetchCoursePrerequisites(courseId),
    enabled: !!courseId,
    select: (data) => (Array.isArray(data) ? data : []),
  });
}

export function useSetCoursePrerequisites() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, prerequisiteCourseIds }) =>
      courseService.setCoursePrerequisites(courseId, prerequisiteCourseIds),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PREREQ_KEY });
    },
  });
}

export function useCreateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => courseService.createCourse(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}

export function useUpdateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => courseService.updateCourse(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}

export function useDeleteCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => courseService.deleteCourse(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}

export function useBulkDeleteCourses() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids) => courseService.bulkDeleteCourses(ids),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}

export function useCloseCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => courseService.closeCourse(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: COURSES_KEY });
      const previous = qc.getQueryData(COURSES_KEY);
      qc.setQueryData(COURSES_KEY, (old) => {
        if (!old) return old;
        return old.map((c) => (c.id === id ? { ...c, isClosed: true, isActive: false } : c));
      });
      return { previous };
    },
    onError: (err, id, context) => {
      qc.setQueryData(COURSES_KEY, context?.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}

export function useOpenCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => courseService.openCourse(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: COURSES_KEY });
      const previous = qc.getQueryData(COURSES_KEY);
      qc.setQueryData(COURSES_KEY, (old) => {
        if (!old) return old;
        return old.map((c) => (c.id === id ? { ...c, isClosed: false, isActive: true } : c));
      });
      return { previous };
    },
    onError: (err, id, context) => {
      qc.setQueryData(COURSES_KEY, context?.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: COURSES_KEY });
    },
  });
}
