import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";
import {
  getServices,
  getAllServices,
  getServiceById,
  createService,
  updateService,
  deleteService,
  toggleServiceStatus,
} from "../services/studentServicesService";

const SERVICES_KEY = "ss-services";

export const useServices = () => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const listQuery = useQuery({
    queryKey: [SERVICES_KEY, scopePart],
    queryFn: async () => {
      const data = await getServices();
      return Array.isArray(data) ? data : [];
    },
    staleTime: 60_000,
  });

  const createMutation = useMutation({
    mutationFn: (payload) => createService(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [SERVICES_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }) => updateService(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [SERVICES_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => deleteService(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [SERVICES_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: (id) => toggleServiceStatus(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [SERVICES_KEY] });
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
    },
  });

  return {
    services: listQuery.data ?? [],
    loading: listQuery.isLoading,
    error: listQuery.error?.message ?? null,
    refresh: () => queryClient.invalidateQueries({ queryKey: [SERVICES_KEY] }),
    addService: createMutation.mutateAsync,
    editService: (id, payload) => updateMutation.mutateAsync({ id, payload }),
    removeService: deleteMutation.mutateAsync,
    toggleStatus: toggleMutation.mutateAsync,
  };
};

export const useAllServices = () => {
  const scopePart = useScopeKeyPart();
  return useQuery({
    queryKey: [SERVICES_KEY, "all", scopePart],
    queryFn: async () => {
      const data = await getAllServices();
      return Array.isArray(data) ? data : [];
    },
    staleTime: 60_000,
  });
};

export const useServiceDetail = (id) => {
  return useQuery({
    queryKey: [SERVICES_KEY, "detail", String(id)],
    enabled: Boolean(id),
    queryFn: () => getServiceById(id),
    staleTime: 60_000,
  });
};
