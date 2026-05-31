import { useState, useEffect, useCallback, useRef } from "react";
import { useTranslation } from "react-i18next";
import { universityStructureService } from "../services/universityStructureService";

export const useUniversityStructure = () => {
  const { i18n } = useTranslation();
  const [treeData, setTreeData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedNode, setSelectedNode] = useState(null);
  const lastSelectedId = useRef(null);

  const fetchTree = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await universityStructureService.getTree();
      setTreeData(data);
      return data;
    } catch (err) {
      setError(err.message || "Failed to load structure");
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  const findNodeById = useCallback((nodes, id) => {
    for (const node of nodes) {
      if (node.id === id) return node;
      if (node.children?.length) {
        const found = findNodeById(node.children, id);
        if (found) return found;
      }
    }
    return null;
  }, []);

  const refreshAndRestore = useCallback(async () => {
    const newTree = await fetchTree();
    if (newTree && lastSelectedId.current) {
      const restoredNode = findNodeById(newTree, lastSelectedId.current);
      if (restoredNode) {
        setSelectedNode(restoredNode);
      } else {
        setSelectedNode(null);
        lastSelectedId.current = null;
      }
    }
  }, [fetchTree, findNodeById]);

  useEffect(() => {
    refreshAndRestore();
  }, [i18n.language, refreshAndRestore]);

  const createNode = async (request) => {
    try {
      const response = await universityStructureService.createNode(request);
      if (response?.id) {
        await refreshAndRestore();
        return { success: true };
      }
      return { success: false, error: "No id returned" };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create node";
      return { success: false, error: msg };
    }
  };

  const updateNode = async (id, request) => {
    try {
      await universityStructureService.updateNode(id, request);
      await refreshAndRestore();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update node";
      return { success: false, error: msg };
    }
  };

  const deleteNode = async (id) => {
    try {
      await universityStructureService.deleteNode(id);
      if (selectedNode?.id === id) {
        setSelectedNode(null);
        lastSelectedId.current = null;
      }
      await refreshAndRestore();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to delete node";
      return { success: false, error: msg };
    }
  };

  const reorderNode = async (id, newOrder) => {
    try {
      await universityStructureService.reorderNode(id, newOrder);
      await refreshAndRestore();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to reorder node";
      return { success: false, error: msg };
    }
  };

  const moveNode = async (id, newParentId, order) => {
    try {
      await universityStructureService.moveNode(id, newParentId, order);
      await refreshAndRestore();
      return { success: true };
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to move node";
      return { success: false, error: msg };
    }
  };

  useEffect(() => {
    if (selectedNode) {
      lastSelectedId.current = selectedNode.id;
    }
  }, [selectedNode]);

  useEffect(() => {
    fetchTree();
  }, [fetchTree, i18n.language]);

  return {
    treeData,
    loading,
    error,
    selectedNode,
    setSelectedNode: (node) => {
      setSelectedNode(node);
      if (node) lastSelectedId.current = node.id;
    },
    fetchTree,
    createNode,
    updateNode,
    deleteNode,
    reorderNode,
    moveNode,
  };
};