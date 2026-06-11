import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import userService from "../../modules/users/services/userService";

export const studentKey = (id) => ["student", id];
export const directoryKey = (configId) => ["directory", configId];

export function useStudent(id, { enabled = true } = {}) {
  return useQuery({
    queryKey: studentKey(id),
    queryFn: () => userService.getStudentById(id),
    enabled: !!id && enabled,
  });
}

// The update endpoint returns only a confirmation message, so the detail
// cache must be refetched; directory lists are invalidated so the secondary
// sidebar and tables pick up renames.
export function useUpdateStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }) => userService.updateStudent(id, body),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: studentKey(id) });
      qc.invalidateQueries({ queryKey: ["directory"] });
    },
  });
}

export function useToggleStudentStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => userService.toggleStudentStatus(id),
    onSuccess: (_data, id) => {
      qc.invalidateQueries({ queryKey: studentKey(id) });
      qc.invalidateQueries({ queryKey: ["directory"] });
    },
  });
}

export function useDeleteStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => userService.deleteStudent(id),
    onSuccess: (_data, id) => {
      qc.removeQueries({ queryKey: studentKey(id) });
      qc.invalidateQueries({ queryKey: ["directory"] });
    },
  });
}
