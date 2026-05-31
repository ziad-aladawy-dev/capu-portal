import React, { useState, useRef, useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Users } from "lucide-react";
import FacultyPageHeader from "../components/FacultyPageHeader";
import { useUsers } from "../hooks/useUsers";
import StudentTable from "../components/StudentTable";
import StaffTable from "../components/StaffTable";
import UserFilters from "../components/UserFilters";
import UserStats from "../components/UserStats";
import "../styles/users.css";

const UserManagement = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const roleFromUrl = searchParams.get("role");
  const [showExportMenu, setShowExportMenu] = useState(false);
  const exportButtonRef = useRef(null);

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
  } = useUsers();

  useEffect(() => {
    if (roleFromUrl === "Student") {
      if (activeTab !== "students") changeTab("students");
    } else if (roleFromUrl === "Staff") {
      if (activeTab !== "staff") changeTab("staff");
    }
  }, [roleFromUrl]);

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
      alert(t("operation_completed"));
    } else {
      alert(`${t("error")}: ${result.error}`);
    }
  };

  const handleViewDetails = (id) => navigate(`/admin/users/${id}`);
  const handleEdit = (id) => {
    if (activeTab === 'students') navigate(`/admin/users/edit-student/${id}`);
    else navigate(`/admin/users/edit-staff/${id}`);
  };
  const handleAddUser = () => {
    if (activeTab === 'students') navigate("/admin/users/add-student");
    else navigate("/admin/users/add-staff");
  };

  const handleExportClick = (format) => {
    exportToExcel(format);
    setShowExportMenu(false);
  };

  const getPageTitle = () => {
    if (roleFromUrl === "Student") return t("student_management");
    if (roleFromUrl === "Staff") return t("staff_management");
    return t("user_management");
  };

  const showTabs = !roleFromUrl;

  const firstItem = pagination.totalCount === 0 ? 0 : (pagination.pageNumber - 1) * pagination.pageSize + 1;
  const lastItem = Math.min(pagination.pageNumber * pagination.pageSize, pagination.totalCount);

  return (
    <div className="users-page">
      <FacultyPageHeader
        title={getPageTitle()}
        icon={Users}
        onAdd={handleAddUser}
        onExport={() => setShowExportMenu(!showExportMenu)}
        showActions={true}
        exportButtonRef={exportButtonRef}
      />
      {showExportMenu && (
        <>
          <div className="users-export-backdrop" onClick={() => setShowExportMenu(false)} />
          <div className="users-export-menu" style={{ position: 'fixed', top: exportButtonRef.current?.getBoundingClientRect().bottom + window.scrollY + 6, left: exportButtonRef.current?.getBoundingClientRect().right - 120, zIndex: 1000 }}>
            <button onClick={() => handleExportClick('excel')}>{t("excel_format")}</button>
            <button onClick={() => handleExportClick('csv')}>{t("csv_format")}</button>
          </div>
        </>
      )}
      <UserStats statistics={statistics} loading={loading} />

      {showTabs && (
        <div className="users-tabs">
          <button className={`users-tab ${activeTab === 'students' ? 'active' : ''}`} onClick={() => changeTab('students')}>
            {t("students")}
          </button>
          <button className={`users-tab ${activeTab === 'staff' ? 'active' : ''}`} onClick={() => changeTab('staff')}>
            {t("staff")}
          </button>
        </div>
      )}
      
      <UserFilters
        filters={filters}
        roles={roles}
        faculties={faculties}
        departments={departments}
        levels={levels}
        activeTab={activeTab}
        onFilterChange={updateFilters}
        onFetchPrograms={fetchPrograms}
        onFetchLevels={fetchLevels}
      />
      <div className="users-table-section">
        {activeTab === 'students' ? (
          <StudentTable
            students={students}
            loading={loading}
            error={error}
            pagination={pagination}
            onPageChange={changePage}
            onViewDetails={handleViewDetails}
            onEdit={handleEdit}
          />
        ) : (
          <StaffTable
            staff={staff}
            loading={loading}
            error={error}
            pagination={pagination}
            onPageChange={changePage}
            onViewDetails={handleViewDetails}
            onEdit={handleEdit}
          />
        )}
      </div>
      <div className="users-pagination-footer">
        <div className="page-size-control">
          <label>{t("show")}</label>
          <select value={pageSize} onChange={handlePageSizeChange}>
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
          <span>{t("entries_per_page")}</span>
        </div>
        <div className="page-results-text">
          {t("showing_results", { first: firstItem, last: lastItem, total: pagination.totalCount })}
        </div>
      </div>
    </div>
  );
};

export default UserManagement;