import { useMemo } from "react";
import { useDomain } from "../contexts/DomainContext";
import { useAcademic } from "../contexts/AcademicContext";

// The apiClient interceptor attaches X-StructureNode-Id / X-AcademicYear-Id /
// X-Semester-Id headers to every request, so any response that varies with the
// active scope must embed the scope in its query key — otherwise React Query
// serves a cached answer from a different scope after the user switches.
export function useScopeKeyPart() {
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();
  const scope = scopeNode?.id ?? null;
  const year = selectedYearObj?.id ?? null;
  const sem = selectedSemesterObj?.id ?? null;
  return useMemo(() => ({ scope, year, sem }), [scope, year, sem]);
}
