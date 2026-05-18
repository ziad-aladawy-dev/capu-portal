import apiClient from "../../../core/api/apiClient";

const STUDENTS_BASE = "/api/students";
const STAFF_BASE = "/api/staff";
const STRUCTURE_LOOKUP_BASE = "/api/structure/lookups";

const userService = {
  // ---------------------- Students ----------------------
  getAllStudents: async (params) => {
    const response = await apiClient.get(`${STUDENTS_BASE}/search`, { params });
    return response.data;
  },
  getStudentById: async (id) => {
    const response = await apiClient.get(`${STUDENTS_BASE}/${id}`);
    return response.data;
  },
  createStudent: async (data) => {
    const response = await apiClient.post(STUDENTS_BASE, data);
    return response.data;
  },
  updateStudent: async (id, data) => {
    const response = await apiClient.put(`${STUDENTS_BASE}/${id}`, data);
    return response.data;
  },
  deleteStudent: async (id) => {
    const response = await apiClient.delete(`${STUDENTS_BASE}/${id}`);
    return response.data;
  },
  toggleStudentStatus: async (id) => {
    const response = await apiClient.patch(`${STUDENTS_BASE}/${id}/toggle-status`);
    return response.data;
  },
  exportStudentsExcel: async (params) => {
    const response = await apiClient.get(`${STUDENTS_BASE}/export-excel`, {
      params,
      responseType: 'blob'
    });
    return response.data;
  },
  exportStudentsCsv: async (params) => {
    const response = await apiClient.get(`${STUDENTS_BASE}/export/csv`, {
      params,
      responseType: 'blob'
    });
    return response.data;
  },

  // ---------------------- Staff ----------------------
  getAllStaff: async (params) => {
    const response = await apiClient.get(`${STAFF_BASE}/search`, { params });
    return response.data;
  },
  getStaffById: async (id) => {
    const response = await apiClient.get(`${STAFF_BASE}/${id}`);
    return response.data;
  },
  createStaff: async (data) => {
    const response = await apiClient.post(STAFF_BASE, data);
    return response.data;
  },
  updateStaff: async (id, data) => {
    const response = await apiClient.put(`${STAFF_BASE}/${id}`, data);
    return response.data;
  },
  deleteStaff: async (id) => {
    const response = await apiClient.delete(`${STAFF_BASE}/${id}`);
    return response.data;
  },
  toggleStaffStatus: async (id) => {
    const response = await apiClient.patch(`${STAFF_BASE}/${id}/toggle-status`);
    return response.data;
  },
  exportStaffExcel: async (params) => {
    const response = await apiClient.get(`${STAFF_BASE}/export-excel`, {
      params,
      responseType: 'blob'
    });
    return response.data;
  },
  exportStaffCsv: async (params) => {
    const response = await apiClient.get(`${STAFF_BASE}/export/csv`, {
      params,
      responseType: 'blob'
    });
    return response.data;
  },

  // ---------------------- Statistics ----------------------
  getUserStatistics: async (scopeNodeId = null) => {
    const params = scopeNodeId ? { ScopeNodeId: scopeNodeId } : {};
    const [studentsStats, staffStats] = await Promise.all([
      apiClient.get(`${STUDENTS_BASE}/statistics`, { params }),
      apiClient.get(`${STAFF_BASE}/statistics`, { params })
    ]);
    return {
      totalStudents: studentsStats.data.totalStudents,
      activeStudents: studentsStats.data.activeStudents,
      inactiveStudents: studentsStats.data.inactiveStudents,
      totalStaff: staffStats.data.totalStaff,
      activeStaff: staffStats.data.activeStaff,
      inactiveStaff: staffStats.data.inactiveStaff,
      totalUsers: studentsStats.data.totalStudents + staffStats.data.totalStaff,
      activeUsers: studentsStats.data.activeStudents + staffStats.data.activeStaff,
      inactiveUsers: studentsStats.data.inactiveStudents + staffStats.data.inactiveStaff,
      studentsCount: studentsStats.data.totalStudents,
      staffCount: staffStats.data.totalStaff
    };
  },

  // ---------------------- Structure Lookups ----------------------
  getFaculties: async () => {
    const response = await apiClient.get(`${STRUCTURE_LOOKUP_BASE}/faculties`);
    return response.data;
  },
  getDepartments: async (facultyId) => {
    const response = await apiClient.get(`${STRUCTURE_LOOKUP_BASE}/${facultyId}/children/Department`);
    return response.data;
  },
  getPrograms: async (facultyId) => {
    if (!facultyId) return [];
    const response = await apiClient.get(`${STRUCTURE_LOOKUP_BASE}/faculties/${facultyId}/programs`);
    return response.data;
  },
  getLevels: async (programId) => {
    if (!programId) return [];
    const response = await apiClient.get(`${STRUCTURE_LOOKUP_BASE}/${programId}/children/Level`);
    return response.data;
  },
  getUniversities: async () => {
    const response = await apiClient.get(`${STRUCTURE_LOOKUP_BASE}/systems`);
    return response.data;
  },
  getRoles: async () => {
    // Mock roles; replace with actual endpoint if exists
    return [
      { id: "Professor", name: "Professor" },
      { id: "AssistantProfessor", name: "Assistant Professor" },
      { id: "TeachingAssistant", name: "Teaching Assistant" },
      { id: "Instructor", name: "Instructor" },
      { id: "AdminStaff", name: "Admin Staff" },
      { id: "HR", name: "HR" },
      { id: "SystemAdmin", name: "System Admin" },
      { id: "AcademicAdmin", name: "Academic Admin" }
    ];
  },

  // ---------------------- Helpers (for UI) ----------------------
  checkEmailUnique: async (email, userType) => {
    // Not implemented in backend; return true for now
    return { isUnique: true };
  },
  checkNationalIdUnique: async (nationalId, userType) => {
    return { isUnique: true };
  },
  generateStaffCode: async (universityId) => {
    return { staffCode: `EMP-${Date.now()}` };
  },
  activateUser: async (userId, userType) => {
    if (userType === "Student") {
      await userService.toggleStudentStatus(userId);
    } else {
      await userService.toggleStaffStatus(userId);
    }
    return { success: true };
  },
  deactivateUser: async (userId, userType, reason) => {
    if (userType === "Student") {
      await userService.toggleStudentStatus(userId);
    } else {
      await userService.toggleStaffStatus(userId);
    }
    return { success: true };
  },
  softDeleteUser: async (userId, reason) => {
    // Not a separate endpoint; use delete
    return { success: true };
  },
  restoreUser: async (userId) => {
    return { success: true };
  },
  resetUserPassword: async (userId, userType, newPassword) => {
    return { success: true };
  }
};

export default userService;