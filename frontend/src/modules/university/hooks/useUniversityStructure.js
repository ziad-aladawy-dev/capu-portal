import { useState, useEffect, useCallback } from "react";
import { universityStructureService } from "../services/universityStructureService";

export const useUniversityStructure = () => {
  const [treeData, setTreeData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedNode, setSelectedNode] = useState(null);

  const fetchTree = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await universityStructureService.getTree();
      setTreeData(data);
    } catch (err) {
      setError(err.message || "Failed to load structure");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTree();
  }, [fetchTree]);

  const createNode = async (request) => {
    try {
      await universityStructureService.createNode(request);
      await fetchTree();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create node";
      return { success: false, error: msg };
    }
  };

  const updateNode = async (id, request) => {
    try {
      await universityStructureService.updateNode(id, request);
      await fetchTree();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update node";
      return { success: false, error: msg };
    }
  };

  const deleteNode = async (id) => {
    try {
      await universityStructureService.deleteNode(id);
      await fetchTree();
      if (selectedNode?.id === id) setSelectedNode(null);
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to delete node";
      return { success: false, error: msg };
    }
  };

  const reorderNode = async (id, newOrder) => {
    try {
      await universityStructureService.reorderNode(id, newOrder);
      await fetchTree();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to reorder node";
      return { success: false, error: msg };
    }
  };

  const moveNode = async (id, newParentId, order) => {
    try {
      await universityStructureService.moveNode(id, newParentId, order);
      await fetchTree();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to move node";
      return { success: false, error: msg };
    }
  };

  return {
    treeData,
    loading,
    error,
    selectedNode,
    setSelectedNode,
    fetchTree,
    createNode,
    updateNode,
    deleteNode,
    reorderNode,
    moveNode,
  };
};