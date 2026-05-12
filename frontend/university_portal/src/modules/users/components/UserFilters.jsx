import React, { useState, useEffect } from "react";
import { Search, Filter, X } from "lucide-react";
import { USER_TYPES_CONFIG } from "./userTypeConfig";

const UserFilters = ({ filters, roles, faculties, departments, onFilterChange, onFetchDepartments }) => {
  const [localFilters, setLocalFilters] = useState({
    searchTerm: "", roleId: "", facultyId: "", departmentId: "", userType: "", userCategory: "", isActive: "", ...filters,
  });
  const [showAdvanced, setShowAdvanced] = useState(false);

  const userTypeOptions = Object.entries(USER_TYPES_CONFIG)
    .sort((a, b) => a[1].order - b[1].order)
    .map(([key, config]) => ({ value: key, labelEn: config.labelEn }));

  const categoryOptions = [
    { value: "student", label: "Students" },
    { value: "staff", label: "Staff" },
    { value: "admin", label: "Admin" },
    { value: "super_admin", label: "Super Admin" },
  ];

  useEffect(() => {
    if (localFilters.facultyId && onFetchDepartments) onFetchDepartments(localFilters.facultyId);
    else if (!localFilters.facultyId && onFetchDepartments) onFetchDepartments(null);
  }, [localFilters.facultyId, onFetchDepartments]);

  useEffect(() => {
    setLocalFilters((prev) => ({
      ...prev,
      searchTerm: filters.searchTerm || "",
      roleId: filters.roleIds?.length ? filters.roleIds[0] : "",
      facultyId: filters.facultyIds?.length ? filters.facultyIds[0] : "",
      departmentId: filters.departmentIds?.length ? filters.departmentIds[0] : "",
      userType: filters.userTypes?.length ? filters.userTypes[0] : "",
      isActive: filters.isActive !== undefined ? filters.isActive.toString() : "",
    }));
  }, [filters]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setLocalFilters((prev) => ({ ...prev, [name]: value, ...(name === "facultyId" && !value ? { departmentId: "" } : {}) }));
  };

  const buildAppliedFilters = () => {
    const appliedFilters = {
      searchTerm: localFilters.searchTerm,
      roleIds: localFilters.roleId ? [localFilters.roleId] : [],
      facultyIds: localFilters.facultyId ? [localFilters.facultyId] : [],
      departmentIds: localFilters.departmentId ? [localFilters.departmentId] : [],
      userTypes: localFilters.userType ? [localFilters.userType] : [],
      isActive: localFilters.isActive === "" ? undefined : localFilters.isActive === "true",
    };

    if (localFilters.userCategory) {
      switch (localFilters.userCategory) {
        case "student": appliedFilters.userTypes.push("Student"); break;
        case "staff": appliedFilters.userTypes.push("Professor", "AssistantProfessor", "TeachingAssistant", "Instructor"); break;
        case "admin": appliedFilters.userTypes.push("AdminStaff", "HR", "AcademicAdmin"); break;
        case "super_admin": appliedFilters.userTypes.push("SystemAdmin"); break;
        default: break;
      }
    }
    return appliedFilters;
  };

  const applyFilters = () => onFilterChange(buildAppliedFilters());
  const resetFilters = () => {
    setLocalFilters({ searchTerm: "", roleId: "", facultyId: "", departmentId: "", userType: "", userCategory: "", isActive: "" });
    onFilterChange({ searchTerm: "", roleIds: [], facultyIds: [], departmentIds: [], userTypes: [], isActive: undefined });
  };
  const clearSearch = () => {
    setLocalFilters((prev) => ({ ...prev, searchTerm: "" }));
    onFilterChange({ ...buildAppliedFilters(), searchTerm: "" });
  };

  return (
    <section className="users-filter-card">
      <div className="users-filter-row">
        <div className="users-search-box">
          <Search size={17} className="users-search-icon" />
          <input type="text" name="searchTerm" value={localFilters.searchTerm} onChange={handleChange} onKeyDown={(e) => e.key === "Enter" && applyFilters()} placeholder="Search by name, email, or national ID..." />
          {localFilters.searchTerm && <button type="button" className="users-clear-search" onClick={clearSearch}><X size={14} /></button>}
        </div>
        <button type="button" className="users-filter-btn primary" onClick={applyFilters}>Search</button>
        <button type="button" className="users-filter-btn soft" onClick={() => setShowAdvanced(!showAdvanced)}><Filter size={16} />Advanced</button>
      </div>

      {showAdvanced && (
        <div className="users-advanced-panel">
          <div className="users-filter-grid">
            <div className="users-filter-field"><label>User Category</label><select name="userCategory" value={localFilters.userCategory || ""} onChange={handleChange}><option value="">All</option>{categoryOptions.map((opt) => <option key={opt.value} value={opt.value}>{opt.label}</option>)}</select></div>
            <div className="users-filter-field"><label>User Type</label><select name="userType" value={localFilters.userType || ""} onChange={handleChange}><option value="">All Types</option>{userTypeOptions.map((opt) => <option key={opt.value} value={opt.value}>{opt.labelEn}</option>)}</select></div>
            <div className="users-filter-field"><label>Role</label><select name="roleId" value={localFilters.roleId || ""} onChange={handleChange}><option value="">All Roles</option>{roles?.map((role) => <option key={role.id} value={role.id}>{role.displayName || role.name}</option>)}</select></div>
            <div className="users-filter-field"><label>Faculty</label><select name="facultyId" value={localFilters.facultyId || ""} onChange={handleChange}><option value="">All Faculties</option>{faculties?.map((faculty) => <option key={faculty.id} value={faculty.id}>{faculty.name}</option>)}</select></div>
            <div className="users-filter-field"><label>Department</label><select name="departmentId" value={localFilters.departmentId || ""} onChange={handleChange} disabled={!localFilters.facultyId}><option value="">All Departments</option>{departments?.map((dept) => <option key={dept.id} value={dept.id}>{dept.name}</option>)}</select></div>
            <div className="users-filter-field"><label>Status</label><select name="isActive" value={localFilters.isActive || ""} onChange={handleChange}><option value="">All</option><option value="true">Active</option><option value="false">Inactive</option></select></div>
          </div>
          <div className="users-filter-actions"><button type="button" className="users-filter-btn soft" onClick={resetFilters}>Reset</button><button type="button" className="users-filter-btn gold" onClick={applyFilters}>Apply Filters</button></div>
        </div>
      )}
    </section>
  );
};

export default UserFilters;
