import { useState, useEffect, useCallback } from "react";
import { getPagedStaffRequests } from "../services/studentServicesService";

export const useStaffRequestsPaged = (initialPageSize = 10) => {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: initialPageSize,
    totalCount: 0,
    totalPages: 1
  });
  const [search, setSearch] = useState("");
  const [sortBy, setSortBy] = useState("requestnumber");
  const [ascending, setAscending] = useState(false);

  const loadRequests = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getPagedStaffRequests(
        pagination.page,
        pagination.pageSize,
        search,
        sortBy,
        ascending
      );
      setRequests(data.items || []);
      setPagination(prev => ({
        ...prev,
        totalCount: data.totalCount,
        totalPages: data.totalPages
      }));
    } catch (err) {
      console.error("Failed to load paged requests:", err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [pagination.page, pagination.pageSize, search, sortBy, ascending]);

  useEffect(() => {
    loadRequests();
  }, [loadRequests]);

  const changePage = (newPage) => {
    if (newPage >= 1 && newPage <= pagination.totalPages) {
      setPagination(prev => ({ ...prev, page: newPage }));
    }
  };

  const changePageSize = (newSize) => {
    setPagination(prev => ({ ...prev, pageSize: newSize, page: 1 }));
  };

  const applySearch = (newSearch) => {
    setSearch(newSearch);
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  const applySort = (newSortBy) => {
    if (newSortBy === sortBy) {
      setAscending(prev => !prev);
    } else {
      setSortBy(newSortBy);
      setAscending(false);
    }
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  return {
    requests,
    loading,
    error,
    pagination,
    search,
    sortBy,
    ascending,
    changePage,
    changePageSize,
    applySearch,
    applySort,
    refresh: loadRequests
  };
};