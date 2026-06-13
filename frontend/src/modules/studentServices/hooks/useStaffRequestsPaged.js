import { useState, useCallback } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useScopeKeyPart } from "../../../core/query/scopedKeys";
import { getPagedStaffRequests } from "../services/studentServicesService";

const REQUESTS_PAGED_KEY = "ss-requests-paged";

export const useStaffRequestsPaged = (initialPageSize = 10) => {
  const scopePart = useScopeKeyPart();
  const queryClient = useQueryClient();

  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: initialPageSize,
  });
  const [search, setSearch] = useState("");
  const [sortBy, setSortBy] = useState("requestnumber");
  const [ascending, setAscending] = useState(false);

  const query = useQuery({
    queryKey: [REQUESTS_PAGED_KEY, scopePart, pagination.page, pagination.pageSize, search, sortBy, ascending],
    queryFn: async () => {
      const data = await getPagedStaffRequests(
        pagination.page,
        pagination.pageSize,
        search,
        sortBy,
        ascending
      );
      return {
        items: data.items || [],
        totalCount: data.totalCount || 0,
        totalPages: data.totalPages || 1,
      };
    },
    staleTime: 15_000,
    placeholderData: (prev) => prev,
  });

  const changePage = useCallback((newPage) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
  }, []);

  const changePageSize = useCallback((newSize) => {
    setPagination({ page: 1, pageSize: newSize });
  }, []);

  const applySearch = useCallback((newSearch) => {
    setSearch(newSearch);
    setPagination((prev) => ({ ...prev, page: 1 }));
  }, []);

  const applySort = useCallback(
    (newSortBy) => {
      if (newSortBy === sortBy) {
        setAscending((prev) => !prev);
      } else {
        setSortBy(newSortBy);
        setAscending(false);
      }
      setPagination((prev) => ({ ...prev, page: 1 }));
    },
    [sortBy]
  );

  return {
    requests: query.data?.items ?? [],
    loading: query.isLoading,
    error: query.error?.message ?? null,
    pagination: {
      page: pagination.page,
      pageSize: pagination.pageSize,
      totalCount: query.data?.totalCount ?? 0,
      totalPages: query.data?.totalPages ?? 1,
    },
    search,
    sortBy,
    ascending,
    changePage,
    changePageSize,
    applySearch,
    applySort,
    refresh: () => queryClient.invalidateQueries({ queryKey: [REQUESTS_PAGED_KEY] }),
  };
};
