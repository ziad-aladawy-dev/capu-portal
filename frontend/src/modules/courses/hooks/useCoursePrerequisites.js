import { useState, useCallback, useMemo, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import * as courseService from "../../../core/services/courseService";

export function useCoursePrerequisites({ excludeId = null, search = "", enabled = true }) {
  const [localSearch, setLocalSearch] = useState(search);

  const { data: allCourses = [], isLoading, refetch } = useQuery({
    queryKey: ["courses", "all", "prerequisites", { excludeId }],
    queryFn: () => courseService.searchCourses({ Page: 1, PageSize: 500 }),
    select: (data) => {
      const items = data?.items || data || [];
      return Array.isArray(items) ? items : [];
    },
    staleTime: 60000,
    gcTime: 10 * 60 * 1000,
    enabled,
  });

  const filteredCourses = useMemo(() => {
    let courses = allCourses;
    if (excludeId) {
      courses = courses.filter((c) => c.id !== excludeId);
    }
    if (localSearch.trim()) {
      const term = localSearch.trim().toLowerCase();
      courses = courses.filter((c) =>
        c.code?.toLowerCase().includes(term) || c.title?.toLowerCase().includes(term)
      );
    }
    return courses;
  }, [allCourses, excludeId, localSearch]);

  const handleSearchChange = useCallback((value) => {
    setLocalSearch(value);
  }, []);

  const clearSearch = useCallback(() => {
    setLocalSearch("");
  }, []);

  return {
    courses: filteredCourses,
    allCourses,
    isLoading,
    search: localSearch,
    handleSearchChange,
    clearSearch,
    refetch,
  };
}