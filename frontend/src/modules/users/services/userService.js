import api from "../../../core/api/apiClient";
import * as staffApi from "../../../core/services/staffService";
import * as studentApi from "../../../core/services/studentService";
import * as structureApi from "../../../core/services/structureService";
import * as permissionApi from "../../../core/services/permissionService";

function toFrontendPagination(apiResult) {
  return {
    items: apiResult.items || [],
    pageNumber: apiResult.page,
    pageSize: apiResult.pageSize,
    totalCount: apiResult.totalCount,
    totalPages: apiResult.totalPages,
  };
}

function staffDtoToFrontend(s) {
  return {
    id: s.id,
    nationalId: s.nationalId,
    staffCode: s.employeeCode,
    fullNameEn: s.name,
    fullNameAr: s.name,
    email: s.email,
    phone: s.phoneNumber,
    facultyName: s.facultyName || s.structureNodeName,
    staffRoleId: s.role,
    staffRoleName: s.role,
    position: s.jobTitle,
    universityName: s.structureNodeName,
    isActive: s.isActive,
    isDeleted: false,
    dateOfBirth: s.birthDate,
    createdAt: s.createdAt,
    passwordExpiryDate: s.passwordExpiry,
    isPasswordExpired: s.passwordStatus === "Expired",
    lastLoginAt: null,
    updatedAt: s.createdAt,
  };
}

function studentDtoToFrontend(s) {
  return {
    id: s.id,
    nationalId: s.nationalId,
    studentCode: s.studentCode,
    fullNameEn: s.name,
    fullNameAr: s.name,
    email: s.email,
    phone: s.phoneNumber,
    facultyName: s.facultyName || s.structureNodeName,
    programName: s.programName,
    levelName: s.levelName,
    levelId: s.structureNodeId,
    isActive: s.isActive,
    isDeleted: false,
    dateOfBirth: s.birthDate,
    createdAt: s.createdAt,
    passwordExpiryDate: s.passwordExpiry,
    isPasswordExpired: s.passwordStatus === "Expired",
    lastLoginAt: null,
    updatedAt: s.createdAt,
    gpa: 0,
    enrollmentDate: s.createdAt,
    status: s.isActive ? "Active" : "Inactive",
  };
}

function toStaffApiParams(params) {
  if (!params) return {};
  return {
    search: params.searchTerm || undefined,
    isActive: params.isActive,
    role: params.roleIds?.length ? params.roleIds[0] : undefined,
    structureNodeId: params.departmentIds?.length ? params.departmentIds[0] : undefined,
    page: params.pageNumber || 1,
    pageSize: params.pageSize || 10,
  };
}

function toStudentApiParams(params) {
  if (!params) return {};
  return {
    search: params.searchTerm || undefined,
    isActive: params.isActive,
    facultyId: params.facultyIds?.length ? params.facultyIds[0] : undefined,
    levelId: params.programIds?.length ? params.programIds[0] : undefined,
    scopeNodeId: params.departmentIds?.length ? params.departmentIds[0] : undefined,
    page: params.pageNumber || 1,
    pageSize: params.pageSize || 10,
  };
}

function handleApiError(err) {
  if (err instanceof api.ApiError) throw err;
  throw new api.ApiError(err.message || "Request failed", 0);
}

const userService = {
  async getAllStudents(params) {
    try {
      const result = await studentApi.fetchAllStudents(toStudentApiParams(params));
      return toFrontendPagination({
        ...result,
        items: (result.items || []).map(studentDtoToFrontend),
      });
    } catch (err) { handleApiError(err); }
  },

  async getAllStaff(params) {
    try {
      const result = await staffApi.fetchAllStaff(toStaffApiParams(params));
      return toFrontendPagination({
        ...result,
        items: (result.items || []).map(staffDtoToFrontend),
      });
    } catch (err) { handleApiError(err); }
  },

  async getStudentById(id) {
    try {
      const result = await studentApi.fetchStudentById(id);
      return studentDtoToFrontend(result);
    } catch (err) {
      if (err.status === 404) return null;
      handleApiError(err);
    }
  },

  async getStaffById(id) {
    try {
      const result = await staffApi.fetchStaffById(id);
      return staffDtoToFrontend(result);
    } catch (err) {
      if (err.status === 404) return null;
      handleApiError(err);
    }
  },

  async getFaculties() {
    try {
      const result = await structureApi.fetchFaculties();
      return (result || []).map((f) => ({
        id: f.id,
        name: f.name,
        nameEn: f.name,
      }));
    } catch (err) { handleApiError(err); }
  },

  async getDepartments(facultyId) {
    try {
      const result = facultyId
        ? await structureApi.fetchChildNodes(facultyId)
        : await structureApi.fetchDepartments();
      return (result || []).map((d) => ({
        id: d.id,
        facultyId: d.parentId,
        name: d.name,
        nameEn: d.name,
      }));
    } catch (err) { handleApiError(err); }
  },

  async getLevels(parentId) {
    try {
      const result = parentId
        ? await structureApi.fetchChildNodes(parentId)
        : await structureApi.fetchLevels();
      return (result || []).map((l) => ({
        id: l.id,
        name: l.name,
        nameEn: l.name,
      }));
    } catch (err) { handleApiError(err); }
  },

  async getRoles() {
    try {
      const result = await permissionApi.fetchAllRoles({ pageSize: 100 });
      return (result.items || []).map((r) => ({
        id: r.id,
        name: r.name,
        nameEn: r.name,
      }));
    } catch (err) { handleApiError(err); }
  },

  async getUniversities() {
    try {
      const result = await structureApi.fetchStructureRoots();
      return (result || []).map((r) => ({
        id: r.id,
        name: r.name,
        nameEn: r.name,
      }));
    } catch (err) { handleApiError(err); }
  },

  async getUserStatistics() {
    try {
      const [staffStats, studentStats] = await Promise.all([
        staffApi.fetchStaffStatistics(),
        studentApi.fetchStudentStatistics(),
      ]);
      return {
        totalUsers: (staffStats?.totalStaff || 0) + (studentStats?.totalStudents || 0),
        totalStudents: studentStats?.totalStudents || 0,
        totalStaff: staffStats?.totalStaff || 0,
        activeUsers: (staffStats?.activeStaff || 0) + (studentStats?.activeStudents || 0),
        inactiveUsers: (staffStats?.inactiveStaff || 0) + (studentStats?.inactiveStudents || 0),
      };
    } catch (err) { handleApiError(err); }
  },

  async createStudent(data) {
    try {
      const body = {
        studentCode: data.studentCode || "",
        name: data.fullNameEn || data.fullNameAr,
        nationalId: data.nationalId,
        email: data.email,
        password: data.password,
        confirmPassword: data.password,
        phoneNumber: data.phone || null,
        birthDate: data.dateOfBirth || null,
        structureNodeId: data.levelId || null,
      };
      const result = await studentApi.createStudent(body);
      return { success: true, id: result.id, data: result };
    } catch (err) { handleApiError(err); }
  },

  async createStaff(data) {
    try {
      const body = {
        employeeCode: data.staffCode || "",
        name: data.fullNameEn || data.fullNameAr,
        nationalId: data.nationalId,
        email: data.email,
        password: data.password,
        confirmPassword: data.password,
        phoneNumber: data.phone || null,
        birthDate: null,
        role: data.staffRoleName || data.staffRoleId || null,
        jobTitle: data.position || null,
        structureNodeId: data.universityId || null,
      };
      const result = await staffApi.createStaff(body);
      return { success: true, id: result.id, data: result };
    } catch (err) { handleApiError(err); }
  },

  async updateStudent(id, data) {
    try {
      const body = {
        name: data.fullNameEn || data.fullNameAr,
        nationalId: data.nationalId,
        email: data.email,
        phoneNumber: data.phone || null,
        birthDate: data.dateOfBirth || null,
        structureNodeId: data.structureNodeId || data.levelId || null,
        isActive: data.isActive,
      };
      const result = await studentApi.updateStudent(id, body);
      return { success: true, id, data: result };
    } catch (err) { handleApiError(err); }
  },

  async updateStaff(id, data) {
    try {
      const body = {
        name: data.fullNameEn || data.fullNameAr,
        nationalId: data.nationalId,
        email: data.email,
        phoneNumber: data.phone || null,
        birthDate: null,
        role: data.staffRoleName || data.staffRoleId || null,
        jobTitle: data.position || null,
        structureNodeId: data.structureNodeId || data.universityId || null,
        isActive: data.isActive,
      };
      const result = await staffApi.updateStaff(id, body);
      return { success: true, id, data: result };
    } catch (err) { handleApiError(err); }
  },

  async activateUser(id, userType) {
    try {
      if (userType === "Student") {
        await studentApi.toggleStudentStatus(id);
      } else {
        await staffApi.toggleStaffStatus(id);
      }
      return { success: true };
    } catch (err) { handleApiError(err); }
  },

  async deactivateUser(id, userType, reason) {
    try {
      if (userType === "Student") {
        await studentApi.toggleStudentStatus(id);
      } else {
        await staffApi.toggleStaffStatus(id);
      }
      return { success: true };
    } catch (err) { handleApiError(err); }
  },

  async softDeleteUser(id, reason, userType) {
    try {
      if (userType === "Student") {
        await studentApi.deleteStudent(id);
      } else {
        await staffApi.deleteStaff(id);
      }
      return { success: true };
    } catch (err) { handleApiError(err); }
  },

  async restoreUser(id) {
    return { success: true };
  },

  async resetUserPassword() {
    return { success: true };
  },

  async checkEmailUnique(email) {
    return true;
  },

  async checkNationalIdUnique(nationalId) {
    return true;
  },

  async generateStaffCode() {
    return `EMP-${Date.now()}`;
  },
};

export default userService;
