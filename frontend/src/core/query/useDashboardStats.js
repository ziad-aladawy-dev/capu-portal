import { useQuery } from "@tanstack/react-query";
import api from "../api/apiClient";
import { fetchStudentStatistics } from "../services/studentService";
import { fetchStaffStatistics } from "../services/staffService";
import { searchCourses } from "../services/courseService";
import { searchAcademicPlans } from "../services/academicPlanService";
import { fetchFaculties, fetchPrograms } from "../services/structureService";
import { fetchTreasuryStats } from "../services/treasuryService";
import { useScopeKeyPart } from "./scopedKeys";

// Aggregate read-side hooks for the admin category dashboards. Every query
// embeds the active scope in its key (the apiClient interceptor stamps scope
// headers on each request) and is gated by `enabled` so a dashboard never
// fires a request the user lacks the module permission for.

const STALE = 60 * 1000;

/** { totalStudents, activeStudents, inactiveStudents } */
export function useStudentStatistics(enabled = true) {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: ["dashboard-stats", "students", scopePart],
    queryFn: () => fetchStudentStatistics(scopePart.scope ? { scopeNodeId: scopePart.scope } : {}),
    enabled,
    staleTime: STALE,
  });
}

/** { totalStaff, activeStaff, inactiveStaff } */
export function useStaffStatistics(enabled = true) {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: ["dashboard-stats", "staff", scopePart],
    queryFn: () => fetchStaffStatistics(scopePart.scope ? { scopeNodeId: scopePart.scope } : {}),
    enabled,
    staleTime: STALE,
  });
}

/** Total catalog course count via the paged search's totalCount. */
export function useCourseCount(enabled = true) {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: ["dashboard-stats", "course-count", scopePart],
    queryFn: () => searchCourses({ Page: 1, PageSize: 1 }),
    select: (data) => data?.totalCount ?? 0,
    enabled,
    staleTime: STALE,
  });
}

/** Total academic plan count via the paged search's totalCount. */
export function useAcademicPlanCount(enabled = true) {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: ["dashboard-stats", "plan-count", scopePart],
    queryFn: () => searchAcademicPlans({ Page: 1, PageSize: 1 }),
    select: (data) => data?.totalCount ?? 0,
    enabled,
    staleTime: STALE,
  });
}

export function useFacultyCount(enabled = true) {
  return useQuery({
    queryKey: ["dashboard-stats", "faculty-count"],
    queryFn: () => fetchFaculties(),
    select: (data) => (Array.isArray(data) ? data.length : 0),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}

export function useProgramCount(enabled = true) {
  return useQuery({
    queryKey: ["dashboard-stats", "program-count"],
    queryFn: () => fetchPrograms(),
    select: (data) => (Array.isArray(data) ? data.length : 0),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}

/** Latest audit log entries (items: createdAtUtc, source, entityName, userName, message). */
export function useRecentAuditLogs(count = 6, enabled = true) {
  return useQuery({
    queryKey: ["dashboard-stats", "recent-audit", count],
    queryFn: async () => {
      const { data } = await api.get("/audit-logs", { params: { pageSize: count } });
      return Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : [];
    },
    enabled,
    staleTime: 30 * 1000,
  });
}

/** GET /payments/fees/stats — totals, fee/order status slices, monthly collected. */
export function useTreasuryStats(enabled = true) {
  return useQuery({
    queryKey: ["dashboard-stats", "treasury"],
    queryFn: () => fetchTreasuryStats(),
    enabled,
    staleTime: STALE,
  });
}

/** Student-services staff stats (services/requests/revenue) without a cross-module import. */
export function useServiceRequestStats(enabled = true) {
  return useQuery({
    queryKey: ["dashboard-stats", "service-requests"],
    queryFn: async () => {
      const { data } = await api.get("/student-services/statistics/staff");
      return data;
    },
    enabled,
    staleTime: STALE,
  });
}
