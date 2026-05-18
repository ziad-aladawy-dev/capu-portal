import apiClient from "../../../core/api/apiClient";

const BASE_URL = "/api/university-structure";

export const universityStructureService = {
  // Get full tree (nested StructureNodeDto[])
  getTree: async () => {
    const response = await apiClient.get(`${BASE_URL}/tree`);
    return response.data;
  },

  // Get node by id
  getNodeById: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/${id}`);
    return response.data;
  },

  // Create new node
  createNode: async (request) => {
    const response = await apiClient.post(BASE_URL, request);
    return response.data;
  },

  // Update node
  updateNode: async (id, request) => {
    const response = await apiClient.put(`${BASE_URL}/${id}`, request);
    return response.data;
  },

  // Delete node (soft delete with descendants)
  deleteNode: async (id) => {
    const response = await apiClient.delete(`${BASE_URL}/${id}`);
    return response.data;
  },

  // Reorder node among siblings
  reorderNode: async (id, newOrder) => {
    const response = await apiClient.put(`${BASE_URL}/reorder/${id}`, { newOrder });
    return response.data;
  },

  // Move node to new parent (with order)
  moveNode: async (id, newParentId, order) => {
    const response = await apiClient.put(`${BASE_URL}/move/${id}`, { newParentId, order });
    return response.data;
  },

  // Get breadcrumb for node
  getBreadcrumb: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/breadcrumb/${id}`);
    return response.data;
  },

  // Get ancestors chain
  getAncestors: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/ancestors/${id}`);
    return response.data;
  },
};