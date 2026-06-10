import { useQuery } from "@tanstack/react-query";
import {
  fetchRegisteredCourses, fetchRegistrationHistory, fetchCourseAttempts,
} from "../../../core/services/registrationService";
import {
  fetchGradeSummary, fetchGradeHistory, fetchTranscript,
} from "../../../core/services/gradeService";

const STALE = 60_000;

export function useRegisteredCourses() {
  return useQuery({
    queryKey: ["academics", "registered-courses"],
    staleTime: STALE,
    queryFn: fetchRegisteredCourses,
  });
}

export function useRegistrationHistory() {
  return useQuery({
    queryKey: ["academics", "registration-history"],
    staleTime: STALE,
    queryFn: fetchRegistrationHistory,
  });
}

export function useCourseAttempts(courseId) {
  return useQuery({
    queryKey: ["academics", "course-attempts", courseId],
    enabled: Boolean(courseId),
    staleTime: STALE,
    queryFn: () => fetchCourseAttempts(courseId),
  });
}

export function useGradeSummary() {
  return useQuery({
    queryKey: ["academics", "grade-summary"],
    staleTime: STALE,
    retry: false,
    queryFn: fetchGradeSummary,
  });
}

export function useGradeHistory() {
  return useQuery({
    queryKey: ["academics", "grade-history"],
    staleTime: STALE,
    queryFn: fetchGradeHistory,
  });
}

export function useTranscript() {
  return useQuery({
    queryKey: ["academics", "transcript"],
    staleTime: STALE,
    retry: false,
    queryFn: fetchTranscript,
  });
}
