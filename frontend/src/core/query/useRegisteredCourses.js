import { useQuery } from "@tanstack/react-query";
import * as courseService from "../services/courseService";

export function useRegisteredCourses() {
  return useQuery({
    queryKey: ["courses", "registered"],
    queryFn: () => courseService.fetchRegisteredCourses(),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 60 * 1000,
  });
}

export function useRegistrationHistory() {
  return useQuery({
    queryKey: ["courses", "registration-history"],
    queryFn: () => courseService.fetchRegistrationHistory(),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 60 * 1000,
  });
}

export function useCourseAttempts(courseId) {
  return useQuery({
    queryKey: ["courses", "attempts", courseId],
    queryFn: () => courseService.fetchCourseAttempts(courseId),
    enabled: !!courseId,
  });
}
