import { useState, useCallback } from "react";
import * as scheduleService from "../../../core/services/scheduleService";
import * as courseOfferingService from "../../../core/services/courseOfferingService";

export function useScheduleSlots() {
  const [slots, setSlots] = useState([]);
  const [offerings, setOfferings] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchOfferings = useCallback(async (nodeId, semesterId) => {
    if (!nodeId || !semesterId) return;
    try {
      const data = await courseOfferingService.fetchOfferingsForNodeSemester(nodeId, semesterId);
      setOfferings(Array.isArray(data) ? data : []);
    } catch {
      setOfferings([]);
    }
  }, []);

  const loadSlots = useCallback(async (offeringId) => {
    if (!offeringId) {
      setSlots([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const data = await scheduleService.fetchSlotsForOffering(offeringId);
      setSlots(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load slots");
      setSlots([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const createSlot = useCallback(async (body) => {
    try {
      const result = await scheduleService.createSlot(body);
      return result;
    } catch (err) {
      setError(err.message || "Failed to create slot");
      throw err;
    }
  }, []);

  const updateSlot = useCallback(async (id, body) => {
    try {
      const result = await scheduleService.updateSlot(id, body);
      return result;
    } catch (err) {
      setError(err.message || "Failed to update slot");
      throw err;
    }
  }, []);

  const removeSlot = useCallback(async (id) => {
    try {
      await scheduleService.deleteSlot(id);
    } catch (err) {
      setError(err.message || "Failed to delete slot");
      throw err;
    }
  }, []);

  return {
    slots,
    offerings,
    loading,
    error,
    fetchOfferings,
    loadSlots,
    createSlot,
    updateSlot,
    removeSlot,
    setError,
  };
}
