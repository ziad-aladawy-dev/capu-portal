import { useState, useEffect, useCallback } from "react";
import {
  getStaffStatistics,
  getMyStudentStatistics,
  getStudentStatisticsById,
} from "../services/studentServicesService";
import { useAuth } from "../../../core/contexts/AuthContext";

export const useStaffStatistics = () => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getStaffStatistics();
      setStats(data);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load staff statistics";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  return { stats, loading, error, refresh: loadStats };
};

export const useStudentStatistics = () => {
  const { user } = useAuth();
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadStats = useCallback(async () => {
    if (!user?.id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await getMyStudentStatistics();
      const r = data?.requestsByStatus || {};
      setStats({
        ...data,
        activeRequests: (r.pending || 0) + (r.underReview || 0) + (r.moreInfoRequired || 0) + (r.paymentPending || 0),
        pendingRequests: r.pending || 0,
        completedRequests: (r.completed || 0) + (r.approved || 0) + (r.readyForPickup || 0),
      });
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load student statistics";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [user?.id]);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  return { stats, loading, error, refresh: loadStats };
};

export const useStudentStatisticsById = (studentId) => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const loadStats = useCallback(async () => {
    if (!studentId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await getStudentStatisticsById(studentId);
      const r = data?.requestsByStatus || {};
      setStats({
        ...data,
        activeRequests: (r.pending || 0) + (r.underReview || 0) + (r.moreInfoRequired || 0) + (r.paymentPending || 0),
        pendingRequests: r.pending || 0,
        completedRequests: (r.completed || 0) + (r.approved || 0) + (r.readyForPickup || 0),
      });
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load student statistics";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [studentId]);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  return { stats, loading, error, refresh: loadStats };
};