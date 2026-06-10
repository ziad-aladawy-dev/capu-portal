import { useQuery } from "@tanstack/react-query";
import * as gradesService from "../services/gradesService";

export function useGradeHistory() {
  return useQuery({
    queryKey: ["grades", "history"],
    queryFn: () => gradesService.fetchGradeHistory(),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useSemesterGrades(semesterId) {
  return useQuery({
    queryKey: ["grades", "history", semesterId],
    queryFn: () => gradesService.fetchSemesterGrades(semesterId),
    enabled: !!semesterId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useGradeSummary() {
  return useQuery({
    queryKey: ["grades", "summary"],
    queryFn: () => gradesService.fetchGradeSummary(),
    staleTime: 5 * 60 * 1000,
  });
}
