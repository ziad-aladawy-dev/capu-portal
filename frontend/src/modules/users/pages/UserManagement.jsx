import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Users } from "lucide-react";

import FacultyPageHeader from "../components/FacultyPageHeader";
import { useUsers } from "../hooks/useUsers";
import StudentTable from "../components/StudentTable";
import StaffTable from "../components/StaffTable";
import UserFilters from "../components/UserFilters";
import UserStats from "../components/UserStats";

import "../styles/users.css";

const UserManagement = ({ initialTab = "students", hideTabs = false }) => {
  const navigate = useNavigate();

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
    exportToExcel,
  } = useUsers(initialTab);

  const [pageSize, setLocalPageSize] = useState(pagination.pageSize);

  const handlePageSizeChange = (e) => {
    const size = parseInt(e.target.value);
    setLocalPageSize(size);
    changePageSize(size);
  };

  const handleUserAction = async (userId, action, reason = null) => {
    const userType = activeTab === "students" ? "Student" : "Staff";
    let result;

    switch (action) {
      case "activate":
        result = await activateUser(userId, userType);
        break;

      case "deactivate":
        result = await deactivateUser(userId, userType, reason);
        break;

      case "soft-delete":
        result = await softDeleteUser(userId, userType, reason);
        break;

      case "restore":
        result = await restoreUser(userId, userType);
        break;

      default:
        return;
    }

    if (result.success) {
      alert("Operation completed successfully");
    } else {
      alert(`Error: ${result.error}`);
    }
  };

  const handleResetPassword = async (userId, newPassword) => {
    const userType = activeTab === "students" ? "Student" : "Staff";

    const result = await resetUserPassword(userId, userType, newPassword);

    if (result.success) {
      alert("Password reset successfully");
    } else {
      alert(`Error: ${result.error}`);
    }
  };

  const handleViewDetails = (id) => {
    const base = activeTab === "students" ? "/admin/students" : "/admin/staff";
    navigate(`${base}/${id}`);
  };

  const handleEdit = (id, type) => {
    if (type === "student") {
      navigate(`/admin/students/edit/${id}`);
    } else {
      navigate(`/admin/staff/edit/${id}`);
    }
  };

  const handlePermissions = (id) => {
    navigate(`/admin/permissions?userId=${id}`);
  };

  const handleAddUser = () => {
    if (activeTab === "students") {
      navigate("/admin/students/add");
    } else {
      navigate("/admin/staff/add");
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
    if (!exportButtonRef) return {};

    const rect = exportButtonRef.getBoundingClientRect();

    return {
      position: "fixed",
      top: rect.bottom + window.scrollY + 6,
      left: rect.right - 120,
      zIndex: 1000,
    };
  };

  const getCurrentItems = () => {
    return activeTab === "students" ? students : staff;
  };

  const usersWithSequentialIds = getCurrentItems().map((user, index) => ({
    ...user,
    displayId:
      (pagination.pageNumber - 1) * pagination.pageSize + index + 1,
    userType: activeTab === "students" ? "Student" : "Staff",
    fullName: user.fullNameEn,
    fullNameAr: user.fullNameAr,
    code: activeTab === "students" ? user.studentCode : user.staffCode,
    levelId: user.levelId,
    levelName: user.levelName,
    programName: user.programName,
    facultyName: user.facultyName,
    staffRoleName: user.staffRoleName,
    universityName: user.universityName,
  }));

  const firstItem =
    pagination.totalCount === 0
      ? 0
      : (pagination.pageNumber - 1) * pagination.pageSize + 1;

  const lastItem = Math.min(
    pagination.pageNumber * pagination.pageSize,
    pagination.totalCount
  );

  return (
    <div className="users-page">
      <FacultyPageHeader
        title="User Management"
        icon={Users}
        onAdd={handleAddUser}
        onExport={handleExportButtonClick}
        showActions={true}
      />

      {showExportMenu && (
        <>
          <div
            className="users-export-backdrop"
            onClick={() => setShowExportMenu(false)}
          />

          <div className="users-export-menu" style={getMenuPosition()}>
            <button onClick={handleExport}>Excel</button>
            <button onClick={handleExport}>CSV</button>
          </div>
        </>
      )}

      <UserStats statistics={statistics} loading={loading} />

      {!hideTabs && (
        <div className="users-tabs">
          <button
            className={`users-tab ${
              activeTab === "students" ? "active" : ""
            }`}
            onClick={() => changeTab("students")}
          >
            Students
          </button>

          <button
            className={`users-tab ${
              activeTab === "staff" ? "active" : ""
            }`}
            onClick={() => changeTab("staff")}
          >
            Staff
          </button>
        </div>
      )}

      <UserFilters
        filters={filters}
        roles={roles}
        faculties={faculties}
        departments={departments}
        userType={activeTab === "students" ? "Student" : "Staff"}
        onFilterChange={updateFilters}
        onFetchDepartments={fetchDepartments}
      />

      <div className="users-table-section">
        {activeTab === "students" ? (
          <StudentTable
            students={usersWithSequentialIds}
            loading={loading}
            error={error}
            pagination={pagination}
            onPageChange={changePage}
            onAction={handleUserAction}
            onResetPassword={handleResetPassword}
            onViewDetails={handleViewDetails}
            onEdit={(id) => handleEdit(id, "student")}
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
            onEdit={(id) => handleEdit(id, "staff")}
            onPermissions={handlePermissions}
          />
        )}
      </div>

      <div className="users-pagination-footer">
        <div className="page-size-control">
          <label>Show</label>

          <select value={pageSize} onChange={handlePageSizeChange}>
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>

          <span>entries per page</span>
        </div>

        <div className="page-results-text">
          Showing {firstItem} - {lastItem} of {pagination.totalCount} results
        </div>
      </div>
    </div>
  );
};

export default UserManagement;