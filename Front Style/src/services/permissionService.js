import axiosInstance from './axiosConfig';

class PermissionService {
  // Get permissions tree (modules + permission types)
  async getPermissionsTree(staffId = null) {
    try {
      const url = staffId 
        ? `/Permission/staff/${staffId}/tree`
        : '/Permission/tree';
      const response = await axiosInstance.get(url);
      return response.data;
    } catch (error) {
      console.error('Failed to load permissions tree:', error);
      return { modules: [], faculties: [], programs: [] };
    }
  }

  // Get raw permissions for a staff member
  async getStaffPermissions(staffId) {
    try {
      const response = await axiosInstance.get(`/Permission/staff/${staffId}/permissions`);
      return response.data || [];
    } catch (error) {
      console.error('Failed to load staff permissions:', error);
      return [];
    }
  }

  // Get grouped permissions (each permission type with its scopes)
  async getStaffPermissionsGrouped(staffId) {
    try {
      const response = await axiosInstance.get(`/Permission/staff/${staffId}/permissions/grouped`);
      return response.data || [];
    } catch (error) {
      console.error('Failed to load grouped permissions:', error);
      return [];
    }
  }

  // Get all modules
  async getModules() {
    try {
      const response = await axiosInstance.get('/Permission/modules');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load modules:', error);
      return [];
    }
  }

  // Get all permission types
  async getPermissionTypes() {
    try {
      const response = await axiosInstance.get('/Permission/permission-types');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load permission types:', error);
      return [];
    }
  }

  // Get all staff members
  async getAllStaff(universityId = null) {
    try {
      const params = universityId ? { universityId } : {};
      const response = await axiosInstance.get('/Permission/staff', { params });
      return response.data || [];
    } catch (error) {
      console.error('Failed to load staff:', error);
      return [];
    }
  }

  // ============ Management Methods ============

  // Add single permission scope
  async addPermission(staffId, data) {
    try {
      const response = await axiosInstance.post(`/Permission/staff/${staffId}/permissions`, data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to add permission');
    }
  }

  // Remove permission scope
  async removePermission(permissionId) {
    try {
      const response = await axiosInstance.delete(`/Permission/permissions/${permissionId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to remove permission');
    }
  }

  // Update all permissions for a staff member (bulk)
  async updateStaffPermissionsBulk(staffId, permissions) {
    try {
      const response = await axiosInstance.post(`/Permission/staff/${staffId}/permissions/bulk`, permissions);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to update permissions');
    }
  }

  // ============ Lookup Methods ============

  // Get faculties for dropdown
  async getFaculties() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/faculties/lookup');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load faculties:', error);
      return [];
    }
  }

  // Get programs for dropdown
  async getPrograms() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/programs/lookup');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load programs:', error);
      return [];
    }
  }
}

export default new PermissionService();