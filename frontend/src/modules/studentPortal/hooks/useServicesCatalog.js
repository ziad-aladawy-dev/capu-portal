import { useQuery } from "@tanstack/react-query";
import { getAvailableServicesForStudent } from "../../studentServices/services/studentServicesService";

/** Services the signed-in student is eligible to request (scope-filtered server-side). */
export function useServicesCatalog(studentId) {
  return useQuery({
    queryKey: ["student-services", "catalog", studentId],
    enabled: Boolean(studentId),
    staleTime: 60_000,
    queryFn: () => getAvailableServicesForStudent(studentId),
  });
}
