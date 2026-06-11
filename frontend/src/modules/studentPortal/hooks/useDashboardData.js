import { useQuery } from "@tanstack/react-query";
import api from "../../../core/api/apiClient";
import * as courseService from "../../../core/services/courseService";
import * as treasuryService from "../../../core/services/treasuryService";
import * as scheduleService from "../../../core/services/scheduleService";
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
    if (!nodeId) nodeId = JSON.parse(localStorage.getItem("capu_selected_scope_node"))?.id || null;
    const sem = JSON.parse(localStorage.getItem("capu_selected_semester"));
    if (!semId) semId = sem?.id || null;
    semesterName = sem?.name || null;
  } catch {}
  return { nodeId, semId, semesterName };
}

async function loadOfferings(nodeId, semId) {
  const resp = await api.get(`/course-offerings/node/${nodeId}/semester/${semId}`);
  return Array.isArray(resp.data) ? resp.data : [];
}

/** Offerings / courses (with titles+credits) / total credits for the active scope. */
export function useAcademicOverview(activeScope) {
  const { nodeId, semId, semesterName } = resolveScopeIds(activeScope);
  return useQuery({
    queryKey: ["dashboard", "academic-overview", nodeId, semId],
    enabled: Boolean(nodeId && semId),
    staleTime: STALE,
    queryFn: async () => {
      const offerings = await loadOfferings(nodeId, semId);
      const courseIds = [...new Set(offerings.map((o) => o.courseId))];
      const results = await Promise.allSettled(courseIds.map((id) => courseService.fetchCourse(id)));
      let totalCredits = 0;
      const courses = [];
      for (const r of results) {
        if (r.status !== "fulfilled" || !r.value) continue;
        if (r.value.creditHours) totalCredits += r.value.creditHours;
        courses.push({
          id: r.value.id,
          code: r.value.code,
          title: r.value.title,
          creditHours: r.value.creditHours ?? 0,
        });
      }
      return {
        offeringCount: offerings.length,
        courseCount: courseIds.length,
        totalCredits,
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

/** Today's classes derived from the active-scope offerings' schedule slots. */
export function useTodaySchedule(activeScope) {
  const { nodeId, semId } = resolveScopeIds(activeScope);
  return useQuery({
    queryKey: ["dashboard", "today-schedule", nodeId, semId],
    enabled: Boolean(nodeId && semId),
    staleTime: STALE,
    queryFn: async () => {
      const offerings = await loadOfferings(nodeId, semId);
      const today = new Date().getDay(); // 0=Sun..6=Sat
      const slotLists = await Promise.allSettled(
        offerings.slice(0, 12).map((o) => scheduleService.fetchSlotsForOffering(o.id ?? o.offeringId))
      );
      const slots = [];
      for (const r of slotLists) {
        if (r.status !== "fulfilled") continue;
        const list = Array.isArray(r.value) ? r.value : r.value?.items || [];
        for (const s of list) {
          const day = s.dayOfWeek ?? s.day;
          if (day === today || day === today + 1 /* 1-indexed schemas */) slots.push(s);
        }
      }
      slots.sort((a, b) => String(a.startTime).localeCompare(String(b.startTime)));
      return slots.slice(0, 6);
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
