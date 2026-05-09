import { useState, useCallback, useEffect } from 'react';
import userService from '../../../services/userService';

export const useUsers = () => {
  const [students, setStudents] = useState([]);
  const [staff, setStaff] = useState([]);
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 1
  });
  const [filters, setFilters] = useState({
    searchTerm: '',
    userTypes: [],
    roleIds: [],
    facultyIds: [],
    programIds: [],
    isActive: undefined,
    includeDeleted: false,
    sortBy: 'createdAt',
    sortDirection: 'desc'
  });
  const [statistics, setStatistics] = useState(null);
  const [roles, setRoles] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [activeTab, setActiveTab] = useState('students');

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      if (activeTab === 'students') {
        const studentParams = {
          pageNumber: pagination.pageNumber,
          pageSize: pagination.pageSize,
          ...filters,
          userTypes: ['Student']
        };
        const response = await userService.getAllStudents(studentParams);
        setStudents(response.items || []);
        setPagination({
          pageNumber: response.pageNumber,
          pageSize: response.pageSize,
          totalCount: response.totalCount,
          totalPages: response.totalPages
        });
      } else if (activeTab === 'staff') {
        const staffParams = {
          pageNumber: pagination.pageNumber,
          pageSize: pagination.pageSize,
          ...filters
        };
        const response = await userService.getAllStaff(staffParams);
        setStaff(response.items || []);
        setPagination({
          pageNumber: response.pageNumber,
          pageSize: response.pageSize,
          totalCount: response.totalCount,
          totalPages: response.totalPages
        });
      }
      
      // Load statistics once
      if (!statistics) {
        const stats = await userService.getUserStatistics();
        setStatistics(stats);
      }
      
      // Load roles, faculties once
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
  }, [activeTab, pagination.pageNumber, pagination.pageSize, filters, statistics]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const loadStaffOnly = async () => {
    setLoading(true);
    try {
      const response = await userService.getAllStaff({
        pageNumber: pagination.pageNumber,
        pageSize: pagination.pageSize,
        searchTerm: filters.searchTerm,
        isActive: true,
        includeDeleted: false
      });
      setUsers(response.items || []);
      setPagination({
        pageNumber: response.pageNumber,
        pageSize: response.pageSize,
        totalCount: response.totalCount,
        totalPages: response.totalPages
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const updateFilters = useCallback((newFilters) => {
    setFilters(prev => ({ ...prev, ...newFilters }));
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
  }, []);

  const changePage = useCallback((page) => {
    setPagination(prev => ({ ...prev, pageNumber: page }));
  }, []);

  const changePageSize = useCallback((size) => {
    setPagination(prev => ({ ...prev, pageSize: size, pageNumber: 1 }));
  }, []);

  const changeTab = useCallback((tab) => {
    setActiveTab(tab);
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
  }, []);

  const fetchDepartments = useCallback(async (facultyId) => {
    if (!facultyId) {
      setDepartments([]);
      return;
    }
    try {
      const depts = await userService.getDepartments(facultyId);
      setDepartments(depts);
    } catch (err) {
      console.error('Failed to load departments:', err);
      setDepartments([]);
    }
  }, []);

  const activateUser = useCallback(async (userId, userType) => {
    try {
      const result = await userService.activateUser(userId, userType);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const deactivateUser = useCallback(async (userId, userType, reason) => {
    try {
      const result = await userService.deactivateUser(userId, userType, reason);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const softDeleteUser = useCallback(async (userId, userType, reason) => {
    try {
      const result = await userService.softDeleteUser(userId, reason);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const restoreUser = useCallback(async (userId, userType) => {
    try {
      const result = await userService.restoreUser(userId);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const resetUserPassword = useCallback(async (userId, userType, newPassword) => {
    try {
      const result = await userService.resetUserPassword(userId, userType, newPassword);
      await loadData();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [loadData]);

  const exportToExcel = useCallback(async () => {
    try {
      const data = activeTab === 'students' ? students : staff;
      // Generate CSV
      const headers = ['ID', 'National ID', 'Code', 'Name', 'Email', 'Status'];
      const rows = data.map(item => [
        item.id,
        item.nationalId,
        activeTab === 'students' ? item.studentCode : item.staffCode,
        item.fullNameEn,
        item.email,
        item.isActive ? 'Active' : 'Inactive'
      ]);
      
      const csvContent = [headers, ...rows].map(row => row.join(',')).join('\n');
      const blob = new Blob([csvContent], { type: 'text/csv' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${activeTab}_export_${new Date().toISOString().split('T')[0]}.csv`;
      a.click();
      window.URL.revokeObjectURL(url);
      
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }, [activeTab, students, staff]);

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
    activeTab,
    updateFilters,
    changePage,
    changePageSize,
    changeTab,
    fetchDepartments,
    activateUser,
    deactivateUser,
    softDeleteUser,
    restoreUser,
    resetUserPassword,
    exportToExcel,
    reloadData: loadData
  };
};