import axiosInstance from './axiosConfig';

class FacultyService {
  // Get faculties lookup (for dropdowns)
  async getFacultiesLookup() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/faculties/lookup');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load faculties:', error);
      return [];
    }
  }
  
  // Get programs by faculty
  async getProgramsByFaculty(facultyId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/faculties/${facultyId}/programs`);
      return response.data || [];
    } catch (error) {
      console.error('Failed to load programs:', error);
      return [];
    }
  }
  
  // Get all programs lookup
  async getProgramsLookup() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/programs/lookup');
      return response.data || [];
    } catch (error) {
      console.error('Failed to load programs:', error);
      return [];
    }
  }
}

export default new FacultyService();