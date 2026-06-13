import { useQuery } from "@tanstack/react-query";
import * as studentService from "../services/studentService";

export function useStudentServiceRequests(studentId, options = {}) {
  return useQuery({
    queryKey: ["student-service-requests", studentId],
    queryFn: () => studentService.fetchStudentServiceRequests(studentId),
    select: (data) => (Array.isArray(data) ? data : data?.items || []),
    enabled: !!studentId,
    ...options,
  });
}
