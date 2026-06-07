import { useState, useCallback } from "react";
import {
  getStudentRequests,
  getStudentRequestById,
  createDraftRequest,
  saveStepData,
  submitRequest,
  processPayment,
} from "../../studentServices/services/studentServicesService";

export const useStudentRequests = () => {
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [currentRequest, setCurrentRequest] = useState(null);

  const loadRequests = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getStudentRequests();
      setRequests(Array.isArray(data) ? data : []);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load requests";
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

  const createDraft = useCallback(async (serviceId) => {
    setError(null);
    try {
      const draft = await createDraftRequest(serviceId);
      setRequests(prev => [draft, ...prev]);
      return draft;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create draft";
      setError(msg);
      throw new Error(msg);
    }
  }, []);

  const saveStep = useCallback(async (requestId, stepKey, data) => {
    setError(null);
    try {
      const updated = await saveStepData(requestId, stepKey, data);
      if (currentRequest?.id === requestId) setCurrentRequest(updated);
      setRequests(prev => prev.map(r => (r.id === requestId ? updated : r)));
      return updated;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to save step";
      setError(msg);
      throw new Error(msg);
    }
  }, [currentRequest]);

  const submit = useCallback(async (requestId) => {
    setError(null);
    try {
      const submitted = await submitRequest(requestId);
      if (currentRequest?.id === requestId) setCurrentRequest(submitted);
      setRequests(prev => prev.map(r => (r.id === requestId ? submitted : r)));
      return submitted;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to submit request";
      setError(msg);
      throw new Error(msg);
    }
  }, [currentRequest]);

  const pay = useCallback(async (requestId, paymentMethod) => {
    setError(null);
    try {
      const updated = await processPayment(requestId, paymentMethod);
      if (currentRequest?.id === requestId) setCurrentRequest(updated);
      setRequests(prev => prev.map(r => (r.id === requestId ? updated : r)));
      return updated;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to process payment";
      setError(msg);
      throw new Error(msg);
    }
  }, [currentRequest]);

  return {
    requests,
    currentRequest,
    loading,
    error,
    loadRequests,
    getRequest,
    createDraft,
    saveStep,
    submit,
    pay,
  };
};