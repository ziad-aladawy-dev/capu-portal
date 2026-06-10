import { useQuery } from "@tanstack/react-query";
import api from "../../../core/api/apiClient";
import * as courseService from "../../../core/services/courseService";
import * as invoiceService from "../../../core/services/invoiceService";
import * as scheduleService from "../../../core/services/scheduleService";
import { fetchUnreadNotifications } from "../../../core/services/notificationService";
import { getAvailableServicesForStudent } from "../../studentServices/services/studentServicesService";

const STALE = 30_000;

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

/** Offerings / course count / total credits for the active scope. */
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
      for (const r of results) {
        if (r.status === "fulfilled" && r.value?.creditHours) totalCredits += r.value.creditHours;
      }
      return {
        offeringCount: offerings.length,
        courseCount: courseIds.length,
        totalCredits,
        semesterName,
      };
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
export function useGradesSummary() {
  return useQuery({
    queryKey: ["dashboard", "grades-summary"],
    staleTime: STALE,
    retry: false,
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

/** Outstanding balance + totals derived from the student's invoices. */
export function useFinancialSnapshot(studentId) {
  return useQuery({
    queryKey: ["dashboard", "financial", studentId],
    enabled: Boolean(studentId),
    staleTime: STALE,
    queryFn: async () => {
      let invoices = [];
      try {
        const data = await invoiceService.fetchInvoicesForStudent(studentId);
        invoices = Array.isArray(data) ? data : data?.items || [];
      } catch {
        invoices = [];
      }
      const total = invoices.reduce((s, i) => s + (i.totalAmount ?? i.amount ?? 0), 0);
      const paid = invoices.reduce((s, i) => s + (i.paidAmount ?? 0), 0);
      const outstanding = invoices.reduce(
        (s, i) => s + (i.outstandingAmount ?? Math.max(0, (i.totalAmount ?? i.amount ?? 0) - (i.paidAmount ?? 0))),
        0
      );
      return { total, paid, outstanding, invoiceCount: invoices.length };
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
export function useUnreadNotifications() {
  return useQuery({
    queryKey: ["dashboard", "notifications-unread"],
    staleTime: STALE,
    refetchInterval: 60_000,
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
