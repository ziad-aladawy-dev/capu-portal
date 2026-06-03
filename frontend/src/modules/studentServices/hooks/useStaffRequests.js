import { useState, useCallback } from "react";
import {
  getAllRequests,
  getStudentRequestById,
  assignRequest,
  updateRequestStatus,
  addComment,
  getAssignedToMe,
} from "../services/studentServicesService";

export const useStaffRequests = () => {
  const [requests, setRequests] = useState([]);
  const [assignedToMe, setAssignedToMe] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [currentRequest, setCurrentRequest] = useState(null);

  const loadAllRequests = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAllRequests();
      setRequests(Array.isArray(data) ? data : []);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load requests";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAssignedToMe = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAssignedToMe();
      setAssignedToMe(Array.isArray(data) ? data : []);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load assigned requests";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const getRequest = useCallback(async (id) => {
    setLoading(true);
    setError(null);
    try {
      const data = await getStudentRequestById(id);
      setCurrentRequest(data);
      return data;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load request";
      setError(msg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const assign = async (requestId, staffId) => {
    setError(null);
    try {
      const updated = await assignRequest(requestId, staffId);
      setRequests(prev => prev.map(r => (r.id === requestId ? updated : r)));
      setAssignedToMe(prev => prev.map(r => (r.id === requestId ? updated : r)));
      if (currentRequest?.id === requestId) setCurrentRequest(updated);
      return updated;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to assign";
      setError(msg);
      throw new Error(msg);
    }
  };

  const changeStatus = async (requestId, newStatus, comment = "") => {
    setError(null);
    try {
      const updated = await updateRequestStatus(requestId, newStatus, comment);
      setRequests(prev => prev.map(r => (r.id === requestId ? updated : r)));
      setAssignedToMe(prev => prev.map(r => (r.id === requestId ? updated : r)));
      if (currentRequest?.id === requestId) setCurrentRequest(updated);
      return updated;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update status";
      setError(msg);
      throw new Error(msg);
    }
  };

  const addCommentToRequest = async (requestId, comment) => {
    setError(null);
    try {
      const updated = await addComment(requestId, comment);
      setRequests(prev => prev.map(r => (r.id === requestId ? updated : r)));
      setAssignedToMe(prev => prev.map(r => (r.id === requestId ? updated : r)));
      if (currentRequest?.id === requestId) setCurrentRequest(updated);
      return updated;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to add comment";
      setError(msg);
      throw new Error(msg);
    }
  };

  return {
    requests,
    assignedToMe,
    loading,
    error,
    currentRequest,
    loadAllRequests,
    loadAssignedToMe,
    getRequest,
    assign,
    changeStatus,
    addCommentToRequest,
  };
};