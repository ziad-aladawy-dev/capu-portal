import React, { useState, useEffect } from "react";
import { Search, Filter, X } from "lucide-react";

const UserFilters = ({ 
  filters, 
  roles, 
  faculties,
  programs,
  levels,
  activeTab,
  onFilterChange, 
  onFetchPrograms,
  onFetchLevels
}) => {
  const [localFilters, setLocalFilters] = useState({
    search: "",
    isActive: "",
    passwordExpired: "",
    facultyId: "",
    programId: "",
    levelId: "",
    role: "",
    jobTitle: ""
  });
  const [showAdvanced, setShowAdvanced] = useState(false);

  useEffect(() => {
    setLocalFilters({
      search: filters.search || "",
      isActive: filters.isActive === undefined ? "" : filters.isActive.toString(),
      passwordExpired: filters.passwordExpired === undefined ? "" : filters.passwordExpired.toString(),
      facultyId: filters.facultyId || "",
      programId: filters.programId || "",
      levelId: filters.levelId || "",
      role: filters.role || "",
      jobTitle: filters.jobTitle || ""
    });
  }, [filters]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setLocalFilters(prev => ({ ...prev, [name]: value }));
    if (name === "facultyId") {
      setLocalFilters(prev => ({ ...prev, programId: "", levelId: "" }));
      onFetchPrograms(value || null);
    }
    if (name === "programId") {
      setLocalFilters(prev => ({ ...prev, levelId: "" }));
      onFetchLevels(value || null);
    }
  };

  const applyFilters = () => {
    const newFilters = {};
    if (localFilters.search) newFilters.search = localFilters.search;
    if (localFilters.isActive !== "") newFilters.isActive = localFilters.isActive === "true";
    if (localFilters.passwordExpired !== "") newFilters.passwordExpired = localFilters.passwordExpired === "true";
    if (localFilters.facultyId) newFilters.facultyId = localFilters.facultyId;
    if (localFilters.programId) newFilters.programId = localFilters.programId;
    if (localFilters.levelId) newFilters.levelId = localFilters.levelId;
    if (localFilters.role) newFilters.role = localFilters.role;
    if (localFilters.jobTitle) newFilters.jobTitle = localFilters.jobTitle;
    onFilterChange(newFilters);
  };

  const resetFilters = () => {
    setLocalFilters({
      search: "",
      isActive: "",
      passwordExpired: "",
      facultyId: "",
      programId: "",
      levelId: "",
      role: "",
      jobTitle: ""
    });
    onFetchPrograms(null);
    onFetchLevels(null);
    onFilterChange({});
  };

  const clearSearch = () => {
    setLocalFilters(prev => ({ ...prev, search: "" }));
    onFilterChange({ ...filters, search: "" });
  };

  return (
    <section className="users-filter-card">
      <div className="users-filter-row">
        <div className="users-search-box">
          <Search size={17} className="users-search-icon" />
          <input
            type="text"
            name="search"
            value={localFilters.search}
            onChange={handleChange}
            onKeyDown={(e) => e.key === "Enter" && applyFilters()}
            placeholder="Search by name, email, or national ID..."
          />
          {localFilters.search && (
            <button type="button" className="users-clear-search" onClick={clearSearch}>
              <X size={14} />
            </button>
          )}
        </div>
        <button type="button" className="users-filter-btn primary" onClick={applyFilters}>
          Search
        </button>
        <button type="button" className="users-filter-btn soft" onClick={() => setShowAdvanced(!showAdvanced)}>
          <Filter size={16} /> Advanced
        </button>
      </div>

      {showAdvanced && (
        <div className="users-advanced-panel">
          <div className="users-filter-grid">
            <div className="users-filter-field">
              <label>Status</label>
              <select name="isActive" value={localFilters.isActive} onChange={handleChange}>
                <option value="">All</option>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
            <div className="users-filter-field">
              <label>Password Status</label>
              <select name="passwordExpired" value={localFilters.passwordExpired} onChange={handleChange}>
                <option value="">All</option>
                <option value="false">Valid</option>
                <option value="true">Expired</option>
              </select>
            </div>

            {activeTab === 'students' && (
              <>
                <div className="users-filter-field">
                  <label>Faculty</label>
                  <select name="facultyId" value={localFilters.facultyId} onChange={handleChange}>
                    <option value="">All Faculties</option>
                    {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
                  </select>
                </div>
                <div className="users-filter-field">
                  <label>Program</label>
                  <select name="programId" value={localFilters.programId} onChange={handleChange} disabled={!localFilters.facultyId}>
                    <option value="">All Programs</option>
                    {programs.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div className="users-filter-field">
                  <label>Level</label>
                  <select name="levelId" value={localFilters.levelId} onChange={handleChange} disabled={!localFilters.programId}>
                    <option value="">All Levels</option>
                    {levels.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                  </select>
                </div>
              </>
            )}

            {activeTab === 'staff' && (
              <>
                <div className="users-filter-field">
                  <label>Role</label>
                  <select name="role" value={localFilters.role} onChange={handleChange}>
                    <option value="">All Roles</option>
                    {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                </div>
                <div className="users-filter-field">
                  <label>Job Title</label>
                  <input
                    type="text"
                    name="jobTitle"
                    value={localFilters.jobTitle}
                    onChange={handleChange}
                    placeholder="e.g., Head of Department"
                  />
                </div>
              </>
            )}
          </div>
          <div className="users-filter-actions">
            <button type="button" className="users-filter-btn soft" onClick={resetFilters}>Reset</button>
            <button type="button" className="users-filter-btn gold" onClick={applyFilters}>Apply Filters</button>
          </div>
        </div>
      )}
    </section>
  );
};

export default UserFilters;