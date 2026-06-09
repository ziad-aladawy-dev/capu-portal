import { useState, useEffect, useMemo, useCallback, useRef } from "react";

export function useServerTable({ fetchFn, queryKey, pageSize = 20 }) {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({
    pageNumber: 1,
    pageSize,
    totalCount: 0,
    totalPages: 1,
  });
  const [refreshKey, setRefreshKey] = useState(0);
  const cancelledRef = useRef(false);

  useEffect(() => {
    cancelledRef.current = false;
  }, []);

  const triggerRefresh = useCallback(() => setRefreshKey(k => k + 1), []);

  const key = useMemo(
    () => JSON.stringify({ ...queryKey, page: pagination.pageNumber, size: pagination.pageSize, refresh: refreshKey }),
    [queryKey, pagination.pageNumber, pagination.pageSize, refreshKey]
  );

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const q = JSON.parse(key);
        const params = {
          Page: q.page,
          PageSize: q.size,
          ...Object.fromEntries(
            Object.entries(q).filter(
              ([k, v]) =>
                !["page", "size", "refresh"].includes(k) &&
                v !== undefined &&
                v !== null &&
                v !== ""
            )
          ),
        };
        const result = await fetchFn(params);
        if (cancelled) return;
        setData(result.items || []);
        setPagination(prev => ({
          ...prev,
          totalCount: result.totalCount || 0,
          totalPages: result.totalPages || 1,
        }));
      } catch (err) {
        if (cancelled) return;
        setError(err.message || "Failed to load data");
        setData([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [key, fetchFn]);

  const goToPage = useCallback((n) => {
    setPagination(prev => ({ ...prev, pageNumber: n }));
  }, []);

  const setPageSize = useCallback((size) => {
    setPagination(prev => ({ ...prev, pageNumber: 1, pageSize: size }));
  }, []);

  return {
    data,
    loading,
    error,
    pagination,
    goToPage,
    setPageSize,
    triggerRefresh,
    setData,
  };
}
