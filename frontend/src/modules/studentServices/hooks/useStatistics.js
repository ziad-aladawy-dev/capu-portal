import { useState, useEffect, useCallback } from "react";
import { getStaffStatistics, getMyStudentStatistics, getStudentStatisticsById } from "../services/studentServicesService";
import { useAuth } from "../../../core/contexts/AuthContext";

export const useStaffStatistics = () => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const loadStats = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getStaffStatistics();
      setStats(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => { loadStats(); }, [loadStats]);
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
    try {
      const data = await getMyStudentStatistics();
      setStats(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [user?.id]);
  useEffect(() => { loadStats(); }, [loadStats]);
  return { stats, loading, error, refresh: loadStats };
};

export const useStudentStatisticsById = (studentId) => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const loadStats = useCallback(async () => {
    if (!studentId) return;
    setLoading(true);
    try {
      const data = await getStudentStatisticsById(studentId);
      setStats(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [studentId]);
  useEffect(() => { loadStats(); }, [loadStats]);
  return { stats, loading, error, refresh: loadStats };
};