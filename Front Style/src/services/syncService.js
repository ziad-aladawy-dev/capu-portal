import axiosInstance from './axiosConfig';

class SyncService {
  // Sync full manifest (students, staff, courses)
  async syncManifest(manifest) {
    try {
      const response = await axiosInstance.post('/Manifest/sync', manifest);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to sync manifest');
    }
  }

  // Sync only students
  async syncStudents(manifest) {
    try {
      const response = await axiosInstance.post('/Sync/trigger/students', manifest);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to sync students');
    }
  }

  // Sync only staff
  async syncStaff(manifest) {
    try {
      const response = await axiosInstance.post('/Sync/trigger/staff', manifest);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to sync staff');
    }
  }

  // Sync only courses
  async syncCourses(manifest) {
    try {
      const response = await axiosInstance.post('/Sync/trigger/courses', manifest);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to sync courses');
    }
  }

  // Get sync status
  async getSyncStatus() {
    try {
      const response = await axiosInstance.get('/Sync/status');
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to get sync status');
    }
  }

  // Get sync summary
  async getSyncSummary(fromDate, toDate) {
    try {
      const params = { fromDate, toDate };
      const response = await axiosInstance.get('/Manifest/summary', { params });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to get sync summary');
    }
  }
}

export default new SyncService();