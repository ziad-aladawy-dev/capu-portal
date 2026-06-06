import { useState, useEffect, useCallback } from "react";
import {
  getWorkflows,
  getWorkflowById,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  addWorkflowStep,
  updateWorkflowStep,
  deleteWorkflowStep,
} from "../services/studentServicesService";

export const useWorkflows = () => {
  const [workflows, setWorkflows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [currentWorkflow, setCurrentWorkflow] = useState(null);

  const loadWorkflows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getWorkflows();
      setWorkflows(Array.isArray(data) ? data : []);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load workflows";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const getWorkflow = useCallback(async (id) => {
    setLoading(true);
    setError(null);
    try {
      const data = await getWorkflowById(id);
      setCurrentWorkflow(data);
      return data;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to load workflow";
      setError(msg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const addWorkflow = async (data) => {
    setError(null);
    try {
      const newWf = await createWorkflow(data);
      await loadWorkflows();
      return newWf;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create workflow";
      setError(msg);
      throw new Error(msg);
    }
  };

  const editWorkflow = async (id, data) => {
    setError(null);
    try {
      await updateWorkflow(id, data);
      await loadWorkflows();
      if (currentWorkflow?.id === id) setCurrentWorkflow({ ...currentWorkflow, ...data });
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update workflow";
      setError(msg);
      throw new Error(msg);
    }
  };

  const removeWorkflow = async (id) => {
    setError(null);
    try {
      await deleteWorkflow(id);
      await loadWorkflows();
      if (currentWorkflow?.id === id) setCurrentWorkflow(null);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to delete workflow";
      setError(msg);
      throw new Error(msg);
    }
  };

  const addStep = async (workflowId, stepData) => {
    setError(null);
    try {
      await addWorkflowStep(workflowId, stepData);
      await loadWorkflows();
      if (currentWorkflow?.id === workflowId) await getWorkflow(workflowId);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to add step";
      setError(msg);
      throw new Error(msg);
    }
  };

  const editStep = async (stepId, stepData) => {
    setError(null);
    try {
      await updateWorkflowStep(stepId, stepData);
      await loadWorkflows();
      if (currentWorkflow) await getWorkflow(currentWorkflow.id);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update step";
      setError(msg);
      throw new Error(msg);
    }
  };

  const removeStep = async (stepId) => {
    setError(null);
    try {
      await deleteWorkflowStep(stepId);
      await loadWorkflows();
      if (currentWorkflow) await getWorkflow(currentWorkflow.id);
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to delete step";
      setError(msg);
      throw new Error(msg);
    }
  };

  useEffect(() => {
    loadWorkflows();
  }, [loadWorkflows]);

  return {
    workflows,
    currentWorkflow,
    loading,
    error,
    getWorkflow,
    addWorkflow,
    editWorkflow,
    removeWorkflow,
    addStep,
    editStep,
    removeStep,
    refresh: loadWorkflows,
  };
};