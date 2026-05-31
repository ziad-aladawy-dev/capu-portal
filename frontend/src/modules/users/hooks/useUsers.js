import { useState, useCallback, useEffect } from 'react';
import userService from '../services/userService';
import { useScope } from '../../../core/contexts/ScopeContext';

export const useUsers = () => {
  const { selectedScope } = useScope();

  const [students, setStudents] = useState([]);
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 1
  });
  
  const [filters, setFilters] = useState({
    search: '',
    isActive: undefined,
    passwordExpired: undefined,
    facultyId: null,
    programId: null,
    levelId: null,
    role: null,
    jobTitle: null,
    structureNodeId: null
  });
  
  const [statistics, setStatistics] = useState(null);
  const [roles, setRoles] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [levels, setLevels] = useState([]);
  const [activeTab, setActiveTab] = useState('students');

  const resetPagination = useCallback(() => {
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
  }, []);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const scopeNodeId = selectedScope?.id || null;
      const baseParams = {
        Page: pagination.pageNumber,
        PageSize: pagination.pageSize,
        ScopeNodeId: scopeNodeId
      };

      if (activeTab === 'students') {
        const studentParams = {
          ...baseParams,
          Search: filters.search || undefined,
          IsActive: filters.isActive,
          PasswordExpired: filters.passwordExpired,
          FacultyId: filters.facultyId || undefined,
          ProgramId: filters.programId || undefined,
          LevelId: filters.levelId || undefined
        };
        const response = await userService.getAllStudents(studentParams);
        setStudents(response.items || []);
        setPagination({
          pageNumber: response.page,
          pageSize: response.pageSize,
          totalCount: response.totalCount,
          totalPages: response.totalPages
        });
      } else {
        const staffParams = {
          ...baseParams,
          Search: filters.search || undefined,
          IsActive: filters.isActive,
          PasswordExpired: filters.passwordExpired,
          Role: filters.role || undefined,
          JobTitle: filters.jobTitle || undefined,
          StructureNodeId: filters.structureNodeId || undefined
        };
        const response = await userService.getAllStaff(staffParams);
        setStaff(response.items || []);
        setPagination({
          pageNumber: response.page,
          pageSize: response.pageSize,
          totalCount: response.totalCount,
          totalPages: response.totalPages
        });
      }

      const stats = await userService.getUserStatistics(scopeNodeId);
      setStatistics(stats);

      if (roles.length === 0) {
        const rolesData = await userService.getRoles();
        setRoles(rolesData);
      }
      if (faculties.length === 0) {
        const facultiesData = await userService.getFaculties();
        setFaculties(facultiesData);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [activeTab, pagination.pageNumber, pagination.pageSize, filters, selectedScope]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const updateFilters = useCallback((newFilters) => {
    setFilters(prev => ({ ...prev, ...newFilters }));
    resetPagination();
  }, [resetPagination]);

  const changePage = useCallback((page) => {
    setPagination(prev => ({ ...prev, pageNumber: page }));
  }, []);

  const changePageSize = useCallback((size) => {
    setPagination({ pageNumber: 1, pageSize: size, totalCount: 0, totalPages: 1 });
  }, []);

  const changeTab = useCallback((tab) => {
    setActiveTab(tab);
    resetPagination();
  }, [resetPagination]);

  const fetchPrograms = useCallback(async (facultyId) => {
    if (!facultyId) {
      setDepartments([]);
      setFilters(prev => ({ ...prev, programId: null, levelId: null }));
      return;
    }
    try {
      const progs = await userService.getPrograms(facultyId);
      setDepartments(progs);
    } catch (err) {
      console.error('Failed to load programs:', err);
      setDepartments([]);
    }
  }, []);

  const fetchLevels = useCallback(async (programId) => {
    if (!programId) {
      setLevels([]);
      setFilters(prev => ({ ...prev, levelId: null }));
      return;
    }
    try {
      const lvls = await userService.getLevels(programId);
      setLevels(lvls);
    } catch (err) {
      console.error('Failed to load levels:', err);
      setLevels([]);
    }
  }, []);

  const exportToExcel = useCallback(async (format) => {
    const scopeNodeId = selectedScope?.id || null;
    const baseParams = {
      ScopeNodeId: scopeNodeId,
      Search: filters.search || undefined,
      IsActive: filters.isActive,
      PasswordExpired: filters.passwordExpired
    };
    let blob;
    let fileName;
    try {
      if (activeTab === 'students') {
        const studentParams = {
          ...baseParams,
          FacultyId: filters.facultyId || undefined,
          ProgramId: filters.programId || undefined,
          LevelId: filters.levelId || undefined
        };
        if (format === 'excel') {
          blob = await userService.exportStudentsExcel(studentParams);
          fileName = `students_${new Date().toISOString().slice(0,19)}.xlsx`;
        } else {
          blob = await userService.exportStudentsCsv(studentParams);
          fileName = `students_${new Date().toISOString().slice(0,19)}.csv`;
        }
      } else {
        const staffParams = {
          ...baseParams,
          Role: filters.role || undefined,
          JobTitle: filters.jobTitle || undefined,
          StructureNodeId: filters.structureNodeId || undefined
        };
        if (format === 'excel') {
          blob = await userService.exportStaffExcel(staffParams);
          fileName = `staff_${new Date().toISOString().slice(0,19)}.xlsx`;
        } else {
          blob = await userService.exportStaffCsv(staffParams);
          fileName = `staff_${new Date().toISOString().slice(0,19)}.csv`;
        }
      }
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      return { success: true };
    } catch (error) {
      console.error('Export failed', error);
      return { success: false, error: error.message };
    }
  }, [activeTab, filters, selectedScope]);

  const importExcel = useCallback(async (file, userType) => {
    try {
      if (userType === 'student') {
        await userService.importStudentsExcel(file);
      } else {
        await userService.importStaffExcel(file);
      }
      await loadData();
      return { success: true };
    } catch (error) {
      console.error('Import failed', error);
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const activateUser = useCallback(async (userId, userType) => {
    try {
      await userService.activateUser(userId, userType);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const deactivateUser = useCallback(async (userId, userType, reason) => {
    try {
      await userService.deactivateUser(userId, userType, reason);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const softDeleteUser = useCallback(async (userId, userType, reason) => {
    try {
      await userService.softDeleteUser(userId, userType, reason);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const restoreUser = useCallback(async (userId, userType) => {
    return activateUser(userId, userType);
  }, [activateUser]);

  const resetUserPassword = useCallback(async (userId, userType, newPassword) => {
    try {
      await userService.resetUserPassword(userId, userType, newPassword);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const getCurrentUsers = () => activeTab === 'students' ? students : staff;

  return {
    users: getCurrentUsers(),
    students,
    staff,
    loading,
    error,
    pagination,
    filters,
    statistics,
    roles,
    faculties,
    departments,
    levels,
    activeTab,
    updateFilters,
    changePage,
    changePageSize,
    changeTab,
    fetchPrograms,
    fetchLevels,
    activateUser,
    deactivateUser,
    softDeleteUser,
    restoreUser,
    resetUserPassword,
    exportToExcel,
    importExcel,
    reloadData: loadData
  };
};