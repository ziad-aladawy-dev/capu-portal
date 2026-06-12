import { useQuery } from "@tanstack/react-query";
import api from "../../../core/api/apiClient";
import * as treasuryService from "../../../core/services/treasuryService";
import { fetchUnreadNotifications } from "../../../core/services/notificationService";
import { getAvailableServicesForStudent } from "../../studentServices/services/studentServicesService";

const STALE = 30_000;

/**
 * One-call dashboard bootstrap (GET /api/student/dashboard). Returns student
 * info, academic summary, registered counts, unread notifications and fee
 * balance together — the per-widget hooks below stay as a fallback for API
 * versions that predate the endpoint (404/error → consumers re-enable them).
 */
export function useAggregatedDashboard() {
  return useQuery({
    queryKey: ["dashboard", "aggregate"],
    staleTime: STALE,
    retry: 0,
    queryFn: async () => {
      const { data } = await api.get("/student/dashboard");
      return data;
    },
  });
}

function resolveScopeIds(activeScope) {
  let nodeId = activeScope?.structural?.nodeId;
  let semId = activeScope?.temporal?.semesterId;
  let semesterName = null;
  try {
    if (!nodeId) nodeId = JSON.parse(sessionStorage.getItem("capu_selected_scope_node"))?.id || null;
    const sem = JSON.parse(sessionStorage.getItem("capu_selected_semester"));
    if (!semId) semId = sem?.id || null;
    semesterName = sem?.name || null;
  } catch { /* scope stash unreadable — fall back to nulls */ }
  return { nodeId, semId, semesterName };
}

/** The student's OWN enrolled courses (titles + credits) via the self-scoped
 *  GET /courses/registered (RegisteredCourses.View is an implicit student
 *  grant). The old offerings+courses catalog fan-out 403'd for students. */
export function useAcademicOverview(activeScope) {
  const { semId, semesterName } = resolveScopeIds(activeScope);
  return useQuery({
    queryKey: ["dashboard", "academic-overview", semId ?? "all"],
    staleTime: STALE,
    queryFn: async () => {
      const { data } = await api.get("/courses/registered");
      const registered = (Array.isArray(data) ? data : [])
        .filter((c) => !semId || c.semesterId === semId);
      const courses = registered.map((c) => ({
        id: c.id,
        code: c.courseCode,
        title: c.courseTitle,
        creditHours: c.creditHours ?? 0,
      }));
      return {
        offeringCount: registered.length,
        courseCount: courses.length,
        totalCredits: courses.reduce((s, c) => s + c.creditHours, 0),
        semesterName,
        courses,
      };
    },
  });
}

/** Open (still in-flight) service requests, newest first. */
export function useOpenRequests() {
  return useQuery({
    queryKey: ["dashboard", "open-requests"],
    staleTime: STALE,
    queryFn: async () => {
      try {
        const { getStudentRequests } = await import(
          "../../studentServices/services/studentServicesService"
        );
        const list = await getStudentRequests();
        const OPEN = new Set([2, 3, 4, 7, 10]); // Pending, UnderReview, MoreInfoRequired, PaymentPending, ReadyForPickup
        return (Array.isArray(list) ? list : [])
          .filter((r) => OPEN.has(r.status))
          .sort((a, b) => new Date(b.createdAt ?? 0) - new Date(a.createdAt ?? 0));
      } catch {
        return [];
      }
    },
  });
}

/** Available student services. */
export function useAvailableServices(userId) {
  return useQuery({
    queryKey: ["dashboard", "available-services", userId],
    enabled: Boolean(userId),
    staleTime: STALE,
    queryFn: async () => {
      const data = await getAvailableServicesForStudent(userId);
      return Array.isArray(data) ? data : [];
    },
  });
}

/** GPA + academic standing. Endpoint is untyped server-side, so read defensively. */
export function useGradesSummary({ enabled = true } = {}) {
  return useQuery({
    queryKey: ["dashboard", "grades-summary"],
    staleTime: STALE,
    retry: false,
    enabled,
    queryFn: async () => {
      try {
        const { data } = await api.get("/grades/summary");
        return data || null;
      } catch {
        return null; // grades may not be available for every student
      }
    },
  });
}

/** Outstanding balance + totals derived from Treasury fees and orders. */
export function useFinancialSnapshot(studentId) {
  return useQuery({
    queryKey: ["dashboard", "financial", studentId],
    enabled: Boolean(studentId),
    staleTime: STALE,
    queryFn: async () => {
      let fees;
      let orders;
      try {
        [fees, orders] = await Promise.all([
          treasuryService.fetchUnpaidFees(studentId),
          treasuryService.fetchOrdersForStudent(studentId),
        ]);
        fees = Array.isArray(fees) ? fees : [];
        orders = Array.isArray(orders) ? orders : [];
      } catch {
        // the student role may not hold the payments permission
        fees = [];
        orders = [];
      }
      const outstanding = fees.reduce((s, f) => s + Number(f.totalAmount ?? 0), 0);
      const paid = orders
        .filter((o) => o.status === treasuryService.ORDER_STATUS.Paid)
        .reduce((s, o) => s + Number(o.totalAmount ?? 0), 0);
      const total = outstanding + paid;
      return { total, paid, outstanding, invoiceCount: fees.length + orders.length };
    },
  });
}

/** Today's classes from the self-scoped student schedule aggregate
 *  (GET /student/schedule — students hold no catalog grants, so the old
 *  offerings fan-out 403'd; dayOfWeek is 0=Sun..6=Sat, same as JS getDay). */
export function useTodaySchedule(activeScope) {
  const { semId } = resolveScopeIds(activeScope);
  return useQuery({
    queryKey: ["dashboard", "today-schedule", semId ?? "all"],
    staleTime: STALE,
    queryFn: async () => {
      const { data } = await api.get("/student/schedule", {
        params: semId ? { semesterId: semId } : undefined,
      });
      const today = new Date().getDay();
      return (Array.isArray(data) ? data : [])
        .filter((s) => s.dayOfWeek === today)
        .map((s) => ({ ...s, room: s.location }))
        .sort((a, b) => String(a.startTime).localeCompare(String(b.startTime)))
        .slice(0, 6);
    },
  });
}

/** Unread notifications (also feeds the bell count). */
export function useUnreadNotifications({ enabled = true } = {}) {
  return useQuery({
    queryKey: ["dashboard", "notifications-unread"],
    staleTime: STALE,
    refetchInterval: 60_000,
    enabled,
    queryFn: async () => {
      try {
        const data = await fetchUnreadNotifications();
        return Array.isArray(data) ? data : data?.items || [];
      } catch {
        return [];
      }
    },
  });
}
