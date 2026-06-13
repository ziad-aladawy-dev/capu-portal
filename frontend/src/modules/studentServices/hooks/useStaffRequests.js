import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";
import {
  getAllRequests,
  getStudentRequestById,
  assignRequest,
  updateRequestStatus,
  addComment,
  closeRecord,
  openRecord,
  getAssignedToMe,
  getStudentRequestAttachments,
} from "../services/studentServicesService";

const REQUESTS_KEY = "ss-requests";

export const useStaffRequests = () => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const allQuery = useQuery({
    queryKey: [REQUESTS_KEY, "all", scopePart],
    queryFn: async () => {
      const data = await getAllRequests();
      return Array.isArray(data) ? data : [];
    },
    staleTime: 30_000,
  });

  const assignedQuery = useQuery({
    queryKey: [REQUESTS_KEY, "assigned-to-me", scopePart],
    queryFn: async () => {
      const data = await getAssignedToMe();
      return Array.isArray(data) ? data : [];
    },
    staleTime: 30_000,
  });

  const assignMutation = useMutation({
    mutationFn: ({ requestId, staffId }) => assignRequest(requestId, staffId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const statusMutation = useMutation({
    mutationFn: ({ requestId, newStatus, comment }) =>
      updateRequestStatus(requestId, newStatus, comment),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const commentMutation = useMutation({
    mutationFn: ({ requestId, comment }) => addComment(requestId, comment),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY] });
    },
  });

  const closeMutation = useMutation({
    mutationFn: (requestId) => closeRecord(requestId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const openMutation = useMutation({
    mutationFn: (requestId) => openRecord(requestId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  return {
    requests: allQuery.data ?? [],
    assignedToMe: assignedQuery.data ?? [],
    loading: allQuery.isLoading,
    error: allQuery.error?.message ?? null,
    loadAllRequests: () => queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY, "all"] }),
    loadAssignedToMe: () => queryClient.invalidateQueries({ queryKey: [REQUESTS_KEY, "assigned-to-me"] }),
    assign: (requestId, staffId) => assignMutation.mutateAsync({ requestId, staffId }),
    changeStatus: (requestId, newStatus, comment) =>
      statusMutation.mutateAsync({ requestId, newStatus, comment }),
    addCommentToRequest: (requestId, comment) =>
      commentMutation.mutateAsync({ requestId, comment }),
    closeRecord: (requestId) => closeMutation.mutateAsync(requestId),
    openRecord: (requestId) => openMutation.mutateAsync(requestId),
  };
};

export const useStaffRequestDetail = (id) => {
  return useQuery({
    queryKey: [REQUESTS_KEY, "detail", String(id)],
    enabled: Boolean(id),
    queryFn: () => getStudentRequestById(id),
    staleTime: 15_000,
  });
};

export const useStaffRequestAttachments = (requestId) => {
  return useQuery({
    queryKey: [REQUESTS_KEY, "attachments", String(requestId)],
    enabled: Boolean(requestId),
    queryFn: () => getStudentRequestAttachments(requestId),
    staleTime: 60_000,
  });
};
