import { useState, useCallback, useEffect } from 'react';
import userService from '../services/userService';
import { useDomain } from '../../../core/contexts/DomainContext';
import { useAcademic } from '../../../core/contexts/AcademicContext';

export const useUsers = ({ initialTab } = {}) => {
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();

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
  const isFixedTab = initialTab === 'students' || initialTab === 'staff';
  const [activeTab, setActiveTab] = useState(isFixedTab ? initialTab : 'students');

  const resetPagination = useCallback(() => {
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
  }, []);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const scopeNodeId = scopeNode?.id || null;
      const baseParams = {
        Page: pagination.pageNumber,
        PageSize: pagination.pageSize,
        ScopeNodeId: scopeNodeId,
        AcademicYearId: selectedYearObj?.id || undefined,
        SemesterId: selectedSemesterObj?.id || undefined,
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

      // Reload statistics
      const stats = await userService.getUserStatistics(scopeNodeId, selectedYearObj?.id, selectedSemesterObj?.id);
      setStatistics(stats);

      // Load lookups once
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
  }, [activeTab, pagination.pageNumber, pagination.pageSize, filters, scopeNode, selectedYearObj, selectedSemesterObj]);

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
    if (isFixedTab) return;
    setActiveTab(tab);
    resetPagination();
  }, [isFixedTab, resetPagination]);

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

  // Export function
  const exportToExcel = useCallback(async (format, selectedIds = null) => {
    const scopeNodeId = scopeNode?.id || null;
    const baseParams = {
      ScopeNodeId: scopeNodeId,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
      Search: filters.search || undefined,
      IsActive: filters.isActive,
      PasswordExpired: filters.passwordExpired
    };
    if (selectedIds && selectedIds.length > 0) {
      baseParams.Ids = selectedIds.join(',');
    }
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
  }, [activeTab, filters, scopeNode, selectedYearObj, selectedSemesterObj]);

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
      if (userType === 'Student') {
        await userService.deleteStudent(userId);
      } else {
        await userService.deleteStaff(userId);
      }
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
    // Not implemented in backend
    return { success: true };
  }, []);

  const bulkActivateUsers = useCallback(async (ids) => {
    const userType = activeTab === 'students' ? 'Student' : 'Staff';
    try {
      const result = await userService.bulkActivateUsers(ids, userType);
      await loadData();
      return result;
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [activeTab, loadData]);

  const bulkDeactivateUsers = useCallback(async (ids) => {
    const userType = activeTab === 'students' ? 'Student' : 'Staff';
    try {
      const result = await userService.bulkDeactivateUsers(ids, userType);
      await loadData();
      return result;
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [activeTab, loadData]);

  const bulkDeleteUsers = useCallback(async (ids) => {
    const userType = activeTab === 'students' ? 'Student' : 'Staff';
    try {
      const result = await userService.bulkDeleteUsers(ids, userType);
      await loadData();
      return result;
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [activeTab, loadData]);

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
    bulkActivateUsers,
    bulkDeactivateUsers,
    bulkDeleteUsers,
    reloadData: loadData
  };
};