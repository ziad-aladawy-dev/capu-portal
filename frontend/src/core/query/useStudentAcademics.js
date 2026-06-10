import { useQuery } from "@tanstack/react-query";
import * as svc from "../services/studentAcademicsService";

/**
 * Admin/advisor-scoped academic reads for a specific student.
 * retry:false so a 404 (endpoint not yet deployed) surfaces immediately
 * and the Advisor Hub can render its "endpoint pending" state instead of spinning.
 */
export function useStudentTranscript(studentId) {
  return useQuery({
    queryKey: ["student-academics", studentId, "transcript"],
    queryFn: () => svc.fetchStudentTranscript(studentId),
    enabled: !!studentId,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useStudentGradeSummary(studentId) {
  return useQuery({
    queryKey: ["student-academics", studentId, "grade-summary"],
    queryFn: () => svc.fetchStudentGradeSummary(studentId),
    enabled: !!studentId,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function useStudentGradeHistory(studentId) {
  return useQuery({
    queryKey: ["student-academics", studentId, "grade-history"],
    queryFn: () => svc.fetchStudentGradeHistory(studentId),
    enabled: !!studentId,
    retry: false,
    staleTime: 5 * 60 * 1000,
    select: (data) => (Array.isArray(data) ? data : []),
  });
}

export function useStudentRegistered(studentId) {
  return useQuery({
    queryKey: ["student-academics", studentId, "registered"],
    queryFn: () => svc.fetchStudentRegistered(studentId),
    enabled: !!studentId,
    retry: false,
    staleTime: 60 * 1000,
    select: (data) => (Array.isArray(data) ? data : []),
  });
}
