import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import * as staffService from "../../../../core/services/staffService";
import { getLocalized } from "../../../../core/utils/getLocalized";

// Staff list for the instructor picker. Names arrive as bilingual JSON —
// decode per the active language once here so every consumer gets plain text.
export function useStaffOptions() {
  const { i18n } = useTranslation();
  return useQuery({
    queryKey: ["staff-options", i18n.language],
    queryFn: () => staffService.fetchAllStaff(),
    select: (data) => {
      const items = data?.items || data || [];
      return (Array.isArray(items) ? items : []).map((s) => ({
        id: s.id,
        name: getLocalized(s.name, i18n.language, s.name) || s.employeeCode || s.id,
        jobTitle: getLocalized(s.jobTitle, i18n.language, ""),
      }));
    },
    staleTime: 5 * 60 * 1000,
  });
}
