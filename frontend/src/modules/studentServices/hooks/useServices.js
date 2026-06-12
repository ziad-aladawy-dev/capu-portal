import { useState, useEffect, useCallback } from "react";
import { getServices, createService, updateService, deleteService, toggleServiceStatus } from "../services/studentServicesService";

export const useServices = () => {
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // No leading setLoading(true): `loading` starts true and the mount effect
  // must not setState synchronously (react-hooks/set-state-in-effect).
  const loadServices = useCallback(async () => {
    try {
      const data = await getServices();
      setServices(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  const addService = async (data) => {
    const newService = await createService(data);
    await loadServices();
    return newService;
  };

  const editService = async (id, data) => {
    await updateService(id, data);
    await loadServices();
  };

  const removeService = async (id) => {
    await deleteService(id);
    await loadServices();
  };

  const toggleStatus = async (id) => {
    await toggleServiceStatus(id);
    await loadServices();
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect -- async loader; setState only after await
  useEffect(() => { loadServices(); }, [loadServices]);

  return { services, loading, error, addService, editService, removeService, toggleStatus, refresh: loadServices };
};