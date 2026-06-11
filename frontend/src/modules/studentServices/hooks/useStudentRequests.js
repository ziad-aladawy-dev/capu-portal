import { useState, useCallback } from "react";
import {
  getStudentRequests,
  getStudentRequestById,
  createDraftRequest,
  saveStepData,
  submitRequest,
  processPayment,
  getPendingRequestForService
} from "../services/studentServicesService";

export const useStudentRequests = () => {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [currentRequest, setCurrentRequest] = useState(null);

  const loadRequests = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getStudentRequests();
      setRequests(Array.isArray(data) ? data : []);
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
    } catch (err) { setError(err.message); throw err; }
    finally { setLoading(false); }
  }, []);

  const createDraft = useCallback(async (serviceId) => {
    const draft = await createDraftRequest(serviceId);
    setRequests(prev => [draft, ...prev]);
    return draft;
  }, []);

  const saveStep = useCallback(async (requestId, stepKey, data) => {
    const updated = await saveStepData(requestId, stepKey, data);
    if (currentRequest?.id === requestId) setCurrentRequest(updated);
    setRequests(prev => prev.map(r => r.id === requestId ? updated : r));
    return updated;
  }, [currentRequest]);

  const submit = useCallback(async (requestId) => {
    const submitted = await submitRequest(requestId);
    if (currentRequest?.id === requestId) setCurrentRequest(submitted);
    setRequests(prev => prev.map(r => r.id === requestId ? submitted : r));
    return submitted;
  }, [currentRequest]);

  const pay = useCallback(async (requestId, method) => {
    const updated = await processPayment(requestId, method);
    if (currentRequest?.id === requestId) setCurrentRequest(updated);
    setRequests(prev => prev.map(r => r.id === requestId ? updated : r));
    return updated;
  }, [currentRequest]);

  const getPendingForService = useCallback(async (serviceId) => {
    setLoading(true);
    try {
      const data = await getPendingRequestForService(serviceId);
      if (data) setCurrentRequest(data);
      return data;
    } catch (err) { setError(err.message); return null; }
    finally { setLoading(false); }
  }, []);

  return { requests, currentRequest, loading, error, loadRequests, getRequest, createDraft, saveStep, submit, pay, getPendingForService };
};