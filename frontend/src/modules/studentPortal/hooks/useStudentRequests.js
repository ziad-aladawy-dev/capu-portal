import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getStudentRequests,
  getStudentRequestById,
  getStudentRequestAttachments,
  getPendingRequestForService,
  createDraftRequest,
  saveStepData,
  submitRequest,
  processPayment,
} from "../../studentServices/services/studentServicesService";

// Scoped like the notifications hooks: ["portal", "requests", ...].
const ROOT = ["portal", "requests"];
const BADGES_KEY = ["portal", "badges"];

export const requestKeys = {
  all: ROOT,
  list: () => [...ROOT, "list"],
  detail: (id) => [...ROOT, "detail", String(id)],
  attachments: (id) => [...ROOT, "attachments", String(id)],
  pending: (serviceId) => [...ROOT, "pending", String(serviceId)],
};

/** All of the signed-in student's requests. */
export function useMyRequests() {
  return useQuery({
    queryKey: requestKeys.list(),
    staleTime: 30_000,
    queryFn: async () => {
      const data = await getStudentRequests();
      return Array.isArray(data) ? data : [];
    },
  });
}

/** One request by id (assumes the student-access fix on GET /requests/{id}). */
export function useStudentRequest(id) {
  return useQuery({
    queryKey: requestKeys.detail(id),
    enabled: Boolean(id),
    staleTime: 15_000,
    queryFn: () => getStudentRequestById(id),
  });
}

/** Attachments of a request (assumes student access on the attachments endpoint). */
export function useRequestAttachments(id) {
  return useQuery({
    queryKey: requestKeys.attachments(id),
    enabled: Boolean(id),
    staleTime: 15_000,
    queryFn: async () => {
      const data = await getStudentRequestAttachments(id);
      return Array.isArray(data) ? data : [];
    },
  });
}

/**
 * Open (Draft / PaymentPending) request for a service, if any.
 * Backend returns 200 with an empty body when there is none — normalize to null.
 */
export function usePendingRequestForService(serviceId, options = {}) {
  return useQuery({
    queryKey: requestKeys.pending(serviceId),
    enabled: Boolean(serviceId) && (options.enabled ?? true),
    staleTime: 10_000,
    queryFn: async () => {
      const data = await getPendingRequestForService(serviceId);
      return data && data.id ? data : null;
    },
  });
}

/** Writes the fresh request DTO into the caches every mutation returns. */
function useRequestCachePatch() {
  const qc = useQueryClient();
  return (updated) => {
    if (!updated?.id) return;
    qc.setQueryData(requestKeys.detail(updated.id), updated);
    qc.setQueryData(requestKeys.list(), (old) =>
      Array.isArray(old)
        ? old.some((r) => r.id === updated.id)
          ? old.map((r) => (r.id === updated.id ? updated : r))
          : [updated, ...old]
        : old
    );
  };
}

export function useCreateDraft() {
  const qc = useQueryClient();
  const patch = useRequestCachePatch();
  return useMutation({
    mutationFn: (serviceId) => createDraftRequest(serviceId),
    onSuccess: (draft) => {
      patch(draft);
      qc.invalidateQueries({ queryKey: requestKeys.list() });
      if (draft?.serviceId) qc.invalidateQueries({ queryKey: requestKeys.pending(draft.serviceId) });
    },
  });
}

export function useSaveStep() {
  const patch = useRequestCachePatch();
  return useMutation({
    mutationFn: ({ requestId, stepKey, data }) => saveStepData(requestId, stepKey, data),
    onSuccess: patch,
  });
}

export function useSubmitRequest() {
  const qc = useQueryClient();
  const patch = useRequestCachePatch();
  return useMutation({
    mutationFn: (requestId) => submitRequest(requestId),
    onSuccess: (updated) => {
      patch(updated);
      qc.invalidateQueries({ queryKey: requestKeys.list() });
      // Nav badge counts open requests — refresh it immediately.
      qc.invalidateQueries({ queryKey: BADGES_KEY });
      if (updated?.serviceId) qc.invalidateQueries({ queryKey: requestKeys.pending(updated.serviceId) });
    },
  });
}

export function useProcessPayment() {
  const qc = useQueryClient();
  const patch = useRequestCachePatch();
  return useMutation({
    mutationFn: ({ requestId, method }) => processPayment(requestId, method),
    onSuccess: (updated) => {
      patch(updated);
      qc.invalidateQueries({ queryKey: requestKeys.list() });
      qc.invalidateQueries({ queryKey: BADGES_KEY });
      if (updated?.serviceId) qc.invalidateQueries({ queryKey: requestKeys.pending(updated.serviceId) });
    },
  });
}
