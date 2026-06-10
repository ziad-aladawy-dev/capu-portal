import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as courseService from "../../../core/services/courseService";
import { COURSE_CATEGORIES } from "../../../core/services/courseService";

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
    placeholderData: (previousData) => previousData,
    staleTime: 30000,
    gcTime: 5 * 60 * 1000,
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
    gcTime: 10 * 60 * 1000,
  });
}

export function useAllCoursesForPrerequisites() {
  return useQuery({
    queryKey: ["courses", "all", "prerequisites"],
    queryFn: () => courseService.searchCourses({ Page: 1, PageSize: 500 }),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 60000,
    gcTime: 10 * 60 * 1000,
    enabled: false,
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

export function useCourseCategories() {
  return useQuery({
    queryKey: ["courses", "categories"],
    queryFn: async () => COURSE_CATEGORIES,
    staleTime: Infinity,
    gcTime: Infinity,
  });
}