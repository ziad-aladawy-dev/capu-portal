import { useState, useCallback } from "react";
import {
  getAllRequests,
  getStudentRequestById,
  assignRequest,
  updateRequestStatus,
  addComment,
  getAssignedToMe,
  getStudentRequestAttachments
} from "../services/studentServicesService";

export const useStaffRequests = () => {
  const [requests, setRequests] = useState([]);
  const [assignedToMe, setAssignedToMe] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [currentRequest, setCurrentRequest] = useState(null);
  const [attachments, setAttachments] = useState([]);
  const [loadingAttachments, setLoadingAttachments] = useState(false);

  const loadAllRequests = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getAllRequests();
      setRequests(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAssignedToMe = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getAssignedToMe();
      setAssignedToMe(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  const getRequest = useCallback(async (id) => {
    setLoading(true);
    try {
      const data = await getStudentRequestById(id);
      setCurrentRequest(data);
      return data;
    } catch (err) {
      setError(err.message);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const getAttachments = useCallback(async (requestId) => {
    setLoadingAttachments(true);
    try {
      const data = await getStudentRequestAttachments(requestId);
      setAttachments(data);
      return data;
    } catch (err) {
      console.error("Failed to load attachments", err);
      setError(err.message);
      return [];
    } finally {
      setLoadingAttachments(false);
    }
  }, []);

  const assign = async (requestId, staffId) => {
    const updated = await assignRequest(requestId, staffId);
    setRequests(prev => prev.map(r => r.id === requestId ? updated : r));
    setAssignedToMe(prev => prev.map(r => r.id === requestId ? updated : r));
    if (currentRequest?.id === requestId) setCurrentRequest(updated);
    return updated;
  };

  const changeStatus = async (requestId, newStatus, comment = "") => {
    const updated = await updateRequestStatus(requestId, newStatus, comment);
    setRequests(prev => prev.map(r => r.id === requestId ? updated : r));
    setAssignedToMe(prev => prev.map(r => r.id === requestId ? updated : r));
    if (currentRequest?.id === requestId) setCurrentRequest(updated);
    return updated;
  };

  const addCommentToRequest = async (requestId, comment) => {
    const updated = await addComment(requestId, comment);
    setRequests(prev => prev.map(r => r.id === requestId ? updated : r));
    setAssignedToMe(prev => prev.map(r => r.id === requestId ? updated : r));
    if (currentRequest?.id === requestId) setCurrentRequest(updated);
    return updated;
  };

  return {
    requests,
    assignedToMe,
    loading,
    error,
    currentRequest,
    attachments,
    loadingAttachments,
    loadAllRequests,
    loadAssignedToMe,
    getRequest,
    getAttachments,
    assign,
    changeStatus,
    addCommentToRequest
  };
};