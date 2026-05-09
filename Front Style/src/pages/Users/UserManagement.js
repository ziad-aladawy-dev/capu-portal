import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Users, UserPlus, Download, Menu, Table } from 'lucide-react';
import Navbar from '../../components/layout/Navbar/Navbar';
import Sidebar from '../../components/layout/Sidebar/Sidebar';
import FacultyPageHeader from '../../components/layout/FacultyPageHeader/FacultyPageHeader';
import { useUsers } from './hooks/useUsers';
import StudentTable from './components/StudentTable';
import StaffTable from './components/StaffTable';
import UserFilters from './components/UserFilters';
import UserStats from './components/UserStats';
import './UserManagement.css';

const UserManagement = () => {
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [showExportMenu, setShowExportMenu] = useState(false);
  const [exportButtonRef, setExportButtonRef] = useState(null);
  
  const {
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
    exportToExcel
  } = useUsers();

  const [pageSize, setLocalPageSize] = useState(pagination.pageSize);

  const handlePageSizeChange = (e) => {
    const size = parseInt(e.target.value);
    setLocalPageSize(size);
    changePageSize(size);
  };

  const handleUserAction = async (userId, action, reason = null) => {
    const userType = activeTab === 'students' ? 'Student' : 'Staff';
    let result;
    
    switch (action) {
      case 'activate':
        result = await activateUser(userId, userType);
        break;
      case 'deactivate':
        result = await deactivateUser(userId, userType, reason);
        break;
      case 'soft-delete':
        result = await softDeleteUser(userId, userType, reason);
        break;
      case 'restore':
        result = await restoreUser(userId, userType);
        break;
      default:
        return;
    }

    if (result.success) {
      alert('Operation completed successfully');
    } else {
      alert(`Error: ${result.error}`);
    }
  };

  const handleResetPassword = async (userId, newPassword) => {
    const userType = activeTab === 'students' ? 'Student' : 'Staff';
    const result = await resetUserPassword(userId, userType, newPassword);
    
    if (result.success) {
      alert('Password reset successfully');
    } else {
      alert(`Error: ${result.error}`);
    }
  };

  const handleViewDetails = (id) => {
    navigate(`/users/${id}`);
  };

  const handleEdit = (id, type) => {
    if (type === 'student') {
      navigate(`/users/edit-student/${id}`);
    } else {
      navigate(`/users/edit-staff/${id}`);
    }
  };

  const handlePermissions = (id) => {
    navigate(`/permissions?userId=${id}`);
  };

  const handleAddUser = () => {
    if (activeTab === 'students') {
      navigate('/users/add-student');
    } else {
      navigate('/users/add-staff');
    }
  };

  const handleExport = async () => {
    await exportToExcel();
    setShowExportMenu(false);
  };

  const handleExportButtonClick = (e) => {
    setExportButtonRef(e.currentTarget);
    setShowExportMenu(!showExportMenu);
  };

  const getMenuPosition = () => {
    if (exportButtonRef) {
      const rect = exportButtonRef.getBoundingClientRect();
      return {
        position: 'fixed',
        top: rect.bottom + window.scrollY + 5,
        left: rect.right - 120,
        zIndex: 1000
      };
    }
    return {};
  };

  const exportMenuStyle = {
    position: 'fixed',
    background: 'white',
    border: '1px solid #e5e7eb',
    borderRadius: '8px',
    boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
    minWidth: '120px',
    ...getMenuPosition()
  };

  const exportMenuItemStyle = {
    width: '100%',
    padding: '10px 16px',
    textAlign: 'left',
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    fontSize: '13px',
    color: '#1a1f5e',
    transition: 'all 0.3s'
  };

  const pageSizeControlStyle = {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    marginTop: '16px',
    justifyContent: 'flex-end',
    fontSize: '13px',
    color: '#6b7280'
  };

  const pageSizeSelectStyle = {
    padding: '6px 12px',
    border: '2px solid #e5e7eb',
    borderRadius: '6px',
    fontSize: '13px',
    outline: 'none',
    cursor: 'pointer'
  };

  const tabsContainerStyle = {
    display: 'flex',
    gap: '8px',
    marginBottom: '24px',
    borderBottom: '2px solid #e5e7eb',
    paddingBottom: '8px'
  };

  const tabButtonStyle = (isActive) => ({
    padding: '12px 24px',
    background: 'none',
    border: 'none',
    fontSize: '15px',
    fontWeight: isActive ? '700' : '600',
    color: isActive ? '#c9a84c' : '#6b7280',
    cursor: 'pointer',
    position: 'relative',
    transition: 'all 0.3s',
    fontFamily: "'DM Sans', sans-serif"
  });

  const activeTabIndicatorStyle = {
    content: '',
    position: 'absolute',
    bottom: '-10px',
    left: 0,
    right: 0,
    height: '3px',
    background: 'linear-gradient(90deg, #c9a84c, #e0c06a)',
    borderRadius: '3px'
  };

  const getCurrentItems = () => activeTab === 'students' ? students : staff;

  const usersWithSequentialIds = getCurrentItems().map((user, index) => ({
    ...user,
    displayId: ((pagination.pageNumber - 1) * pagination.pageSize) + index + 1,
    userType: activeTab === 'students' ? 'Student' : 'Staff',
    fullName: user.fullNameEn,
    fullNameAr: user.fullNameAr,
    code: activeTab === 'students' ? user.studentCode : user.staffCode,
    levelId: user.levelId,
    levelName: user.levelName,
    programName: user.programName,
    facultyName: user.facultyName,
    staffRoleName: user.staffRoleName,
    universityName: user.universityName
  }));

  return (
    <div className="dashboard-container">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <Navbar onMenuClick={() => setSidebarOpen(true)} />

      <div className="main-content">
        <FacultyPageHeader
          title="User Management"
          icon={Users}
          onAdd={handleAddUser}
          onExport={handleExportButtonClick}
          showActions={true}
        />

        {/* Export Menu */}
        {showExportMenu && (
          <>
            <div 
              style={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 999
              }}
              onClick={() => setShowExportMenu(false)}
            />
            <div style={exportMenuStyle}>
              <button 
                style={exportMenuItemStyle}
                onClick={handleExport}
                onMouseEnter={(e) => e.target.style.background = '#f4f5f7'}
                onMouseLeave={(e) => e.target.style.background = 'none'}
              >
                Excel
              </button>
              <button 
                style={exportMenuItemStyle}
                onClick={handleExport}
                onMouseEnter={(e) => e.target.style.background = '#f4f5f7'}
                onMouseLeave={(e) => e.target.style.background = 'none'}
              >
                CSV
              </button>
            </div>
          </>
        )}

        {/* Statistics */}
        <UserStats statistics={statistics} loading={loading} />

        {/* Tabs */}
        <div style={tabsContainerStyle}>
          <button 
            style={tabButtonStyle(activeTab === 'students')}
            onClick={() => changeTab('students')}
          >
            Students
            {activeTab === 'students' && <div style={activeTabIndicatorStyle} />}
          </button>
          <button 
            style={tabButtonStyle(activeTab === 'staff')}
            onClick={() => changeTab('staff')}
          >
            Staff
            {activeTab === 'staff' && <div style={activeTabIndicatorStyle} />}
          </button>
        </div>

        {/* Filters */}
        <UserFilters
          filters={filters}
          roles={roles}
          faculties={faculties}
          departments={departments}
          userType={activeTab === 'students' ? 'Student' : 'Staff'}
          onFilterChange={updateFilters}
          onFetchDepartments={fetchDepartments}
        />

        {/* Table */}
        {activeTab === 'students' ? (
          <StudentTable
            students={usersWithSequentialIds}
            loading={loading}
            error={error}
            pagination={pagination}
            onPageChange={changePage}
            onAction={handleUserAction}
            onResetPassword={handleResetPassword}
            onViewDetails={handleViewDetails}
            onEdit={(id) => handleEdit(id, 'student')}
            onPermissions={handlePermissions}
          />
        ) : (
          <StaffTable
            staff={usersWithSequentialIds}
            loading={loading}
            error={error}
            pagination={pagination}
            onPageChange={changePage}
            onAction={handleUserAction}
            onResetPassword={handleResetPassword}
            onViewDetails={handleViewDetails}
            onEdit={(id) => handleEdit(id, 'staff')}
            onPermissions={handlePermissions}
          />
        )}

        {/* Page Size Control */}
        <div style={pageSizeControlStyle}>
          <label>Show:</label>
          <select
            value={pageSize}
            onChange={handlePageSizeChange}
            style={pageSizeSelectStyle}
            onFocus={(e) => e.target.style.borderColor = '#c9a84c'}
            onBlur={(e) => e.target.style.borderColor = '#e5e7eb'}
          >
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
          <span>entries per page</span>
          <span style={{ marginLeft: 'auto' }}>
            Showing {((pagination.pageNumber - 1) * pagination.pageSize) + 1} -{' '}
            {Math.min(pagination.pageNumber * pagination.pageSize, pagination.totalCount)} of{' '}
            {pagination.totalCount} results
          </span>
        </div>
      </div>
    </div>
  );
};

export default UserManagement;