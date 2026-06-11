import { useState, useEffect, useCallback } from "react";
import { getWorkflows, getWorkflowById, createWorkflow, updateWorkflow, deleteWorkflow, addWorkflowStep, updateWorkflowStep, deleteWorkflowStep } from "../services/studentServicesService";

export const useWorkflows = () => {
  const [workflows, setWorkflows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [currentWorkflow, setCurrentWorkflow] = useState(null);

  const loadWorkflows = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getWorkflows();
      setWorkflows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  const getWorkflow = useCallback(async (id) => {
    const data = await getWorkflowById(id);
    setCurrentWorkflow(data);
    return data;
  }, []);

  const addWorkflow = async (data) => {
    const newWf = await createWorkflow(data);
    await loadWorkflows();
    return newWf;
  };

  const editWorkflow = async (id, data) => {
    await updateWorkflow(id, data);
    await loadWorkflows();
    if (currentWorkflow?.id === id) setCurrentWorkflow({ ...currentWorkflow, ...data });
  };

  const removeWorkflow = async (id) => {
    await deleteWorkflow(id);
    await loadWorkflows();
    if (currentWorkflow?.id === id) setCurrentWorkflow(null);
  };

  const addStep = async (workflowId, stepData) => {
    await addWorkflowStep(workflowId, stepData);
    await loadWorkflows();
    if (currentWorkflow?.id === workflowId) await getWorkflow(workflowId);
  };

  const editStep = async (stepId, stepData) => {
    await updateWorkflowStep(stepId, stepData);
    await loadWorkflows();
    if (currentWorkflow) await getWorkflow(currentWorkflow.id);
  };

  const removeStep = async (stepId) => {
    await deleteWorkflowStep(stepId);
    await loadWorkflows();
    if (currentWorkflow) await getWorkflow(currentWorkflow.id);
  };

  useEffect(() => { loadWorkflows(); }, [loadWorkflows]);

  return { workflows, currentWorkflow, loading, error, getWorkflow, addWorkflow, editWorkflow, removeWorkflow, addStep, editStep, removeStep, refresh: loadWorkflows };
};