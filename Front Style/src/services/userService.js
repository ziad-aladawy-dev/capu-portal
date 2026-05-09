import axiosInstance from './axiosConfig';

class UserService {
  // ============ Student APIs ============
  
  async getAllStudents(params = {}) {
    try {
      const response = await axiosInstance.get('/Users/students', { params });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load students');
    }
  }

  async getStudentById(id) {
    try {
      const response = await axiosInstance.get(`/Users/students/${id}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load student details');
    }
  }

  async createStudent(data) {
    try {
      const response = await axiosInstance.post('/Users/students', data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to create student');
    }
  }

  async updateStudent(id, data) {
    try {
      const response = await axiosInstance.put(`/Users/students/${id}`, data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to update student');
    }
  }

  async deleteStudent(id, permanent = false) {
    try {
      const response = await axiosInstance.delete(`/Users/students/${id}`, { params: { permanent } });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to delete student');
    }
  }

  async restoreStudent(id) {
    try {
      const response = await axiosInstance.post(`/Users/students/${id}/restore`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to restore student');
    }
  }

  // ============ Staff APIs ============
  
  async getAllStaff(params = {}) {
    try {
      const response = await axiosInstance.get('/Users/staff', { params });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load staff');
    }
  }

  async getStaffById(id) {
    try {
      const response = await axiosInstance.get(`/Users/staff/${id}`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load staff details');
    }
  }

  async createStaff(data) {
    try {
      const response = await axiosInstance.post('/Users/staff', data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to create staff');
    }
  }

  async updateStaff(id, data) {
    try {
      const response = await axiosInstance.put(`/Users/staff/${id}`, data);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to update staff');
    }
  }

  async deleteStaff(id, permanent = false) {
    try {
      const response = await axiosInstance.delete(`/Users/staff/${id}`, { params: { permanent } });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to delete staff');
    }
  }

  async restoreStaff(id) {
    try {
      const response = await axiosInstance.post(`/Users/staff/${id}/restore`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to restore staff');
    }
  }

  // ============ Statistics ============
  
  async getUserStatistics() {
    try {
      const response = await axiosInstance.get('/Users/statistics');
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load statistics');
    }
  }

  // ============ User Actions ============
  
  async activateUser(userId, userType) {
    try {
      const response = await axiosInstance.post(`/Users/${userType}/users/${userId}/activate`);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to activate user');
    }
  }

  async deactivateUser(userId, userType, reason = null) {
    try {
      const response = await axiosInstance.post(`/Users/${userType}/users/${userId}/deactivate`, reason);
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to deactivate user');
    }
  }

  async resetUserPassword(userId, userType, newPassword = null) {
    try {
      const response = await axiosInstance.post(`/Users/${userType}/users/${userId}/reset-password`, { newPassword });
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to reset password');
    }
  }

  // ============ Helper APIs ============
  
  async checkEmailUnique(email, userType, excludeId = null) {
    try {
      const response = await axiosInstance.get('/Users/check-email', { params: { email, userType, excludeId } });
      return response.data;
    } catch (error) {
      return { isUnique: false };
    }
  }

  async checkNationalIdUnique(nationalId, userType, excludeId = null) {
    try {
      const response = await axiosInstance.get('/Users/check-national-id', { params: { nationalId, userType, excludeId } });
      return response.data;
    } catch (error) {
      return { isUnique: false };
    }
  }

  async generateStudentCode(levelId) {
    try {
      const response = await axiosInstance.get(`/Users/generate-student-code/${levelId}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to generate student code');
    }
  }

  async generateStaffCode(universityId) {
    try {
      const response = await axiosInstance.get(`/Users/generate-staff-code/${universityId}`);
      return response.data;
    } catch (error) {
      console.error('Generate staff code error:', error);
      // Return a fallback code
      return { staffCode: `STAFF${Date.now()}` };
    }
  }

  // async generateStaffCode(universityId) {
  //   try {
  //     const response = await axiosInstance.get(`/Users/generate-staff-code/${universityId}`);
  //     return response.data;
  //   } catch (error) {
  //     throw new Error('Failed to generate staff code');
  //   }
  // }

  // ============ Existing Methods (for backward compatibility) ============
  
  async getUsers(filters = {}) {
    // This method now splits into students and staff
    const [students, staff] = await Promise.all([
      this.getAllStudents({ pageSize: filters.pageSize || 100, ...filters }),
      this.getAllStaff({ pageSize: filters.pageSize || 100, ...filters })
    ]);
    
    const items = [...(students.items || []), ...(staff.items || [])];
    return {
      items,
      totalCount: items.length,
      pageNumber: 1,
      pageSize: items.length
    };
  }

  async getUserById(id) {
    // Try to get as student first, then as staff
    try {
      return await this.getStudentById(id);
    } catch (e) {
      try {
        return await this.getStaffById(id);
      } catch (e2) {
        throw new Error('User not found');
      }
    }
  }

  async getStaff(params = {}) {
    try {
      const response = await axiosInstance.get('/Users/staff', { params });
      // ✅ معالجة البيانات لإضافة fullName لو مش موجود
      if (response.data && response.data.items) {
        response.data.items = response.data.items.map(item => ({
          ...item,
          fullName: item.fullNameEn || item.fullName || item.nameEn,
          fullNameEn: item.fullNameEn || item.nameEn,
          fullNameAr: item.fullNameAr || item.nameAr
        }));
      }
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Failed to load staff');
    }
  }

  // async getStaff(params = {}) {
  //   try {
  //     // Force userTypes to be Staff only
  //     const queryParams = {
  //       ...params,
  //       userTypes: ['Staff']
  //     };
  //     const response = await axiosInstance.get('/Users/staff', { params: queryParams });
  //     return response.data;
  //   } catch (error) {
  //     throw new Error(error.userMessage || 'Failed to load staff');
  //   }
  // }

  async getRoles() {
    try {
      const response = await axiosInstance.get('/Users/roles');
      return response.data;
    } catch (error) {
      console.error('Failed to load roles:', error);
      // Return default roles as fallback
      return [
        { id: '11111111-1111-1111-1111-111111111111', name: 'Professor', nameEn: 'Professor', nameAr: 'أستاذ' },
        { id: '22222222-2222-2222-2222-222222222222', name: 'AssistantProfessor', nameEn: 'Assistant Professor', nameAr: 'أستاذ مساعد' },
        { id: '33333333-3333-3333-3333-333333333333', name: 'TeachingAssistant', nameEn: 'Teaching Assistant', nameAr: 'معيد' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Instructor', nameEn: 'Instructor', nameAr: 'مدرس' },
        { id: '55555555-5555-5555-5555-555555555555', name: 'AdminStaff', nameEn: 'Admin Staff', nameAr: 'موظف إداري' },
        { id: '66666666-6666-6666-6666-666666666666', name: 'HR', nameEn: 'HR', nameAr: 'موارد بشرية' },
        { id: '77777777-7777-7777-7777-777777777777', name: 'SystemAdmin', nameEn: 'System Admin', nameAr: 'مدير نظام' },
        { id: '88888888-8888-8888-8888-888888888888', name: 'AcademicAdmin', nameEn: 'Academic Admin', nameAr: 'إدارة أكاديمية' }
      ];
    }
  }
  async getFaculties() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/faculties/lookup');
      return response.data;
    } catch (error) {
      console.error('Failed to load faculties:', error);
      return [];
    }
  }

  async getDepartments(facultyId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/faculties/${facultyId}/programs`);
      return response.data;
    } catch (error) {
      console.error('Failed to load departments:', error);
      return [];
    }
  }

  async getLevels(programId) {
    try {
      const response = await axiosInstance.get(`/UniversityStructure/programs/${programId}/levels`);
      return response.data;
    } catch (error) {
      console.error('Failed to load levels:', error);
      return [];
    }
  }

  async getUniversities() {
    try {
      const response = await axiosInstance.get('/UniversityStructure/universities');
      return response.data;
    } catch (error) {
      console.error('Failed to load universities:', error);
      return [];
    }
  }

  async getUserActivity(userId) {
    // Mock activity logs for now
    return [
      { id: 1, action: 'Account created', description: 'User account was created', timestamp: new Date() },
      { id: 2, action: 'Profile updated', description: 'User updated profile information', timestamp: new Date(Date.now() - 7 * 86400000) }
    ];
  }

  async updateUser(id, data) {
    // Determine user type and call appropriate API
    try {
      const student = await this.getStudentById(id);
      return await this.updateStudent(id, data);
    } catch (e) {
      return await this.updateStaff(id, data);
    }
  }

  async softDeleteUser(id, reason = null) {
    try {
      const student = await this.getStudentById(id);
      return await this.deleteStudent(id, false);
    } catch (e) {
      return await this.deleteStaff(id, false);
    }
  }

  async restoreUser(id) {
    try {
      const student = await this.getStudentById(id);
      return await this.restoreStudent(id);
    } catch (e) {
      return await this.restoreStaff(id);
    }
  }
}

export default new UserService();