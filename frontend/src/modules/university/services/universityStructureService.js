import apiClient from "../../../core/api/apiClient";

const BASE_URL = "/api/university-structure";

export const universityStructureService = {
  getTree: async () => {
    const response = await apiClient.get(`${BASE_URL}/tree`);
    return response.data;
  },

  getNodeById: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/${id}`);
    return response.data;
  },

  createNode: async (request) => {
    const response = await apiClient.post(BASE_URL, request);
    return response.data;
  },

  updateNode: async (id, request) => {
    const response = await apiClient.put(`${BASE_URL}/${id}`, request);
    return response.data;
  },

  deleteNode: async (id) => {
    const response = await apiClient.delete(`${BASE_URL}/${id}`);
    return response.data;
  },

  reorderNode: async (id, newOrder) => {
    const response = await apiClient.put(`${BASE_URL}/reorder/${id}`, { newOrder });
    return response.data;
  },

  moveNode: async (id, newParentId, order) => {
    const response = await apiClient.put(`${BASE_URL}/move/${id}`, { newParentId, order });
    return response.data;
  },

  getBreadcrumb: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/breadcrumb/${id}`);
    return response.data;
  },

  getAncestors: async (id) => {
    const response = await apiClient.get(`${BASE_URL}/ancestors/${id}`);
    return response.data;
  },
};