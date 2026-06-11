import { useState, useEffect, useCallback } from "react";
import { getStaffStatistics, getRecentRequests } from "../services/studentServicesService";

export const useStaffDashboard = () => {
  const [stats, setStats] = useState(null);
  const [recentRequests, setRecentRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadDashboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsData, recentData] = await Promise.all([
        getStaffStatistics(),
        getRecentRequests(5)
      ]);
      setStats(statsData);
      setRecentRequests(recentData);
    } catch (err) {
      console.error("Dashboard loading error:", err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadDashboard();
  }, [loadDashboard]);

  return { stats, recentRequests, loading, error, refresh: loadDashboard };
};