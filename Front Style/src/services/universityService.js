import axiosInstance from './axiosConfig';

class UniversityService {
  // Get university tree structure
  async getUniversityTree(universityId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/tree/${universityId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load university structure');
    }
  }

  // Get all system types
  async getSystemTypes() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/system-types');
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load system types');
    }
  }

  // Get faculties by university
  async getFacultiesByUniversity(universityId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/universities/${universityId}/faculties`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load faculties');
    }
  }

  // Get programs by faculty
  async getProgramsByFaculty(facultyId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/faculties/${facultyId}/programs`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load programs');
    }
  }

  // Get levels by program
  async getLevelsByProgram(programId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/programs/${programId}/levels`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load levels');
    }
  }

  async getAllUniversities() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/universities');
      return response.data;
    } catch (error) {
      console.error('Get all universities error:', error);
      throw new Error(error.userMessage || 'Failed to load universities');
    }
  }

  async addUniversity(data) {
    try {
      const response = await axiosInstance.post('/UniversityStructure/universities', data);
      return response.data;
    } catch (error) {
      console.error('Add university error:', error);
      throw new Error(error.userMessage || 'Failed to add university');
    }
  }

  // Add faculty
  async addFaculty(data) {
    try {
      const response = await axiosInstance.post('/UniversityStructure/faculties', data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to add faculty');
    }
  }

  // Add program
  async addProgram(data) {
    try {
      const response = await axiosInstance.post('/UniversityStructure/programs', data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to add program');
    }
  }

  // Add level
  async addLevel(data) {
    try {
      const response = await axiosInstance.post('/UniversityStructure/levels', data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to add level');
    }
  }

  // Delete faculty
  async deleteFaculty(facultyId) {
    try {
      const response = await axiosInstance.delete(`/UniversityStructure/faculties/${facultyId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to delete faculty');
    }
  }

  // Delete program
  async deleteProgram(programId) {
    try {
      const response = await axiosInstance.delete(`/UniversityStructure/programs/${programId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to delete program');
    }
  }

  // Delete level
  async deleteLevel(levelId) {
    try {
      const response = await axiosInstance.delete(`/UniversityStructure/levels/${levelId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to delete level');
    }
  }
}

export default new UniversityService();