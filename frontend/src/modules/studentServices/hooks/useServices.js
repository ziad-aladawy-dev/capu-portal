import { useState, useEffect, useCallback } from "react";
import {
  getServices,
  createService,
  updateService,
  deleteService,
  toggleServiceStatus,
} from "../services/studentServicesService";

export const useServices = () => {
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadServices = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getServices();
      setServices(Array.isArray(data) ? data : []);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load services";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const addService = async (data) => {
    setError(null);
    try {
      const newService = await createService(data);
      await loadServices();
      return newService;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create service";
      setError(msg);
      throw new Error(msg);
    }
  };

  const editService = async (id, data) => {
    setError(null);
    try {
      await updateService(id, data);
      await loadServices();
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update service";
      setError(msg);
      throw new Error(msg);
    }
  };

  const removeService = async (id) => {
    setError(null);
    try {
      await deleteService(id);
      await loadServices();
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to delete service";
      setError(msg);
      throw new Error(msg);
    }
  };

  const toggleStatus = async (id) => {
    setError(null);
    try {
      await toggleServiceStatus(id);
      await loadServices();
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to toggle status";
      setError(msg);
      throw new Error(msg);
    }
  };

  useEffect(() => {
    loadServices();
  }, [loadServices]);

  return {
    services,
    loading,
    error,
    addService,
    editService,
    removeService,
    toggleStatus,
    refresh: loadServices,
  };
};