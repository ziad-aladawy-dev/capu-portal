import React, { useState, useEffect } from 'react';
import { Search, Filter, X } from 'lucide-react';
import { USER_TYPES_CONFIG } from './userTypeConfig';

const UserFilters = ({ filters, roles, faculties, departments, userType, onFilterChange, onFetchDepartments }) => {
  const [localFilters, setLocalFilters] = useState({
    searchTerm: '',
    roleId: '',
    facultyId: '',
    departmentId: '',
    userType: '',
    isActive: '',
    ...filters
  });

  const [showAdvanced, setShowAdvanced] = useState(false);

  const userTypeOptions = Object.entries(USER_TYPES_CONFIG)
  .sort((a, b) => a[1].order - b[1].order)
  .map(([key, config]) => ({
    value: key,
    label: config.label,
    labelEn: config.labelEn,
    category: config.category
  }));

  const categoryOptions = [
    { value: 'student', label: 'Students' },
    { value: 'staff', label: 'Staff' },
    { value: 'admin', label: 'Admin' },
    { value: 'super_admin', label: 'Super Admin' }
  ];

  useEffect(() => {
    if (localFilters.facultyId && onFetchDepartments) {
      onFetchDepartments(localFilters.facultyId);
    } else if (!localFilters.facultyId && onFetchDepartments) {
      onFetchDepartments(null);
    }
  }, [localFilters.facultyId, onFetchDepartments]);

  // Update local filters when external filters change
  useEffect(() => {
    setLocalFilters(prev => ({
      ...prev,
      searchTerm: filters.searchTerm || '',
      roleId: filters.roleIds?.length ? filters.roleIds[0] : '',
      facultyId: filters.facultyIds?.length ? filters.facultyIds[0] : '',
      departmentId: filters.departmentIds?.length ? filters.departmentIds[0] : '',
      userType: filters.userTypes?.length ? filters.userTypes[0] : '',
      isActive: filters.isActive !== undefined ? filters.isActive.toString() : ''
    }));
  }, [filters]);

  // Handle filter change
  const handleChange = (e) => {
    const { name, value } = e.target;
    setLocalFilters(prev => ({ ...prev, [name]: value }));
    
    // If faculty changes, trigger department loading
    if (name === 'facultyId' && !value) {
      // Clear department selection
      setLocalFilters(prev => ({ ...prev, departmentId: '' }));
    }
  };

  // Apply filters
  const applyFilters = () => {
    const appliedFilters = {
      searchTerm: localFilters.searchTerm,
      roleIds: localFilters.roleId ? [localFilters.roleId] : [],
      facultyIds: localFilters.facultyId ? [localFilters.facultyId] : [],
      departmentIds: localFilters.departmentId ? [localFilters.departmentId] : [],
      userTypes: localFilters.userType ? [localFilters.userType] : [],
      isActive: localFilters.isActive === '' ? undefined : localFilters.isActive === 'true'
    };

    if (localFilters.userType) {
      appliedFilters.userTypes.push(localFilters.userType);
    }

    if (localFilters.userCategory) {
      switch (localFilters.userCategory) {
        case 'student':
          appliedFilters.userTypes.push('Student');
          break;
        case 'staff':
          appliedFilters.userTypes.push('Professor', 'AssistantProfessor', 'TeachingAssistant', 'Instructor');
          break;
        case 'admin':
          appliedFilters.userTypes.push('AdminStaff', 'HR', 'AcademicAdmin');
          break;
        case 'super_admin':
          appliedFilters.userTypes.push('SystemAdmin');
          break;
        default:
          break;
      }
    }
    
    onFilterChange(appliedFilters);
  };

  // Reset filters
  const resetFilters = () => {
    setLocalFilters({
      searchTerm: '',
      roleId: '',
      facultyId: '',
      departmentId: '',
      userType: '',
      isActive: ''
    });
    
    onFilterChange({
      searchTerm: '',
      roleIds: [],
      facultyIds: [],
      departmentIds: [],
      userTypes: [],
      isActive: undefined
    });
  };

  // Apply search on Enter key press
  const handleKeyPress = (e) => {
    if (e.key === 'Enter') {
      applyFilters();
    }
  };

  const filterContainerStyle = {
    background: 'var(--pure-white)',
    borderRadius: '16px',
    padding: '24px',
    marginBottom: '24px',
    boxShadow: '0 4px 20px rgba(15, 23, 41, 0.06)',
    border: '1px solid var(--border-color)'
  };

  const searchRowStyle = {
    display: 'flex',
    gap: '12px',
    alignItems: 'center'
  };

  const searchWrapperStyle = {
    flex: 1,
    position: 'relative'
  };

  const searchIconStyle = {
    position: 'absolute',
    left: '16px',
    top: '50%',
    transform: 'translateY(-50%)',
    color: 'var(--text-muted)'
  };

  const searchInputStyle = {
    width: '100%',
    padding: '14px 16px 14px 48px',
    border: '2px solid var(--border-color)',
    borderRadius: '12px',
    fontSize: '15px',
    fontFamily: "'DM Sans', sans-serif",
    background: 'var(--soft-lavender)',
    color: 'var(--text-primary)',
    outline: 'none'
  };

  const buttonStyle = {
    padding: '14px 24px',
    border: 'none',
    borderRadius: '12px',
    fontSize: '15px',
    fontWeight: '600',
    cursor: 'pointer',
    transition: 'all 0.3s',
    whiteSpace: 'nowrap'
  };

  const advancedStyle = {
    marginTop: '20px',
    padding: '20px',
    background: 'var(--soft-lavender)',
    borderRadius: '12px'
  };

  const gridStyle = {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
    gap: '16px',
    marginBottom: '16px'
  };

  const filterItemStyle = {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px'
  };

  const labelStyle = {
    fontSize: '12px',
    fontWeight: '600',
    color: 'var(--text-muted)',
    textTransform: 'uppercase',
    letterSpacing: '0.5px'
  };

  const selectStyle = {
    padding: '10px 12px',
    border: '2px solid var(--border-color)',
    borderRadius: '8px',
    fontSize: '14px',
    background: 'var(--pure-white)',
    color: 'var(--navy-primary)',
    outline: 'none',
    fontWeight: '500'
  };

  return (
    <div style={filterContainerStyle}>
      <div style={searchRowStyle}>
        <div style={searchWrapperStyle}>
          <Search size={20} style={searchIconStyle} />
          <input
            type="text"
            name="searchTerm"
            value={localFilters.searchTerm}
            onChange={handleChange}
            onKeyPress={handleKeyPress}
            placeholder="Search by name, email, or national ID..."
            style={searchInputStyle}
            onFocus={(e) => e.target.style.borderColor = 'var(--gold)'}
            onBlur={(e) => e.target.style.borderColor = 'var(--border-color)'}
          />
          {localFilters.searchTerm && (
            <button
              onClick={() => {
                setLocalFilters(prev => ({ ...prev, searchTerm: '' }));
                applyFilters();
              }}
              style={{
                position: 'absolute',
                right: '12px',
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                color: 'var(--text-muted)',
                cursor: 'pointer',
                padding: '4px'
              }}
            >
              <X size={16} />
            </button>
          )}
        </div>
        
        <button
          onClick={applyFilters}
          style={{
            ...buttonStyle,
            background: 'linear-gradient(135deg, var(--navy-primary), var(--navy-accent))',
            color: 'white'
          }}
          onMouseEnter={(e) => e.target.style.transform = 'translateY(-2px)'}
          onMouseLeave={(e) => e.target.style.transform = 'translateY(0)'}
        >
          Search
        </button>
        
        <button
          onClick={() => setShowAdvanced(!showAdvanced)}
          style={{
            ...buttonStyle,
            background: 'var(--pure-white)',
            color: 'var(--navy-primary)',
            border: '2px solid var(--border-color)',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Filter size={18} />
          Advanced Filter
        </button>
      </div>

      {showAdvanced && (
        <div style={advancedStyle}>
          <div style={gridStyle}>

          <div style={filterItemStyle}>
              <label style={labelStyle}>User Category</label>
              <select
                name="userCategory"
                value={localFilters.userCategory}
                onChange={handleChange}
                style={selectStyle}
                onFocus={(e) => e.target.style.borderColor = 'var(--gold)'}
                onBlur={(e) => e.target.style.borderColor = 'var(--border-color)'}
              >
                <option value="">All</option>
                {categoryOptions.map(opt => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>User Type</label>
              <select
                name="userType"
                value={localFilters.userType}
                onChange={handleChange}
                style={selectStyle}
                onFocus={(e) => e.target.style.borderColor = 'var(--gold)'}
                onBlur={(e) => e.target.style.borderColor = 'var(--border-color)'}
              >
                <option value="">All Types</option>
                {userTypeOptions.map(opt => (
                  <option key={opt.value} value={opt.value}>{opt.labelEn}</option>
                ))}
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>Role</label>
              <select
                name="roleId"
                value={localFilters.roleId}
                onChange={handleChange}
                style={selectStyle}
                onFocus={(e) => e.target.style.borderColor = 'var(--gold)'}
                onBlur={(e) => e.target.style.borderColor = 'var(--border-color)'}
              >
                <option value="">All Roles</option>
                {roles && roles.map(role => (
                  <option key={role.id} value={role.id}>{role.displayName || role.name}</option>
                ))}
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>User Type</label>
              <select
                name="userType"
                value={localFilters.userType}
                onChange={handleChange}
                style={selectStyle}
              >
                <option value="">All Types</option>
                <option value="Student">Student</option>
                <option value="Instructor">Instructor</option>
                <option value="Admin">Admin</option>
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>Faculty</label>
              <select
                name="facultyId"
                value={localFilters.facultyId}
                onChange={handleChange}
                style={selectStyle}
              >
                <option value="">All Faculties</option>
                {faculties && faculties.map(faculty => (
                  <option key={faculty.id} value={faculty.id}>{faculty.name}</option>
                ))}
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>Department</label>
              <select
                name="departmentId"
                value={localFilters.departmentId}
                onChange={handleChange}
                style={selectStyle}
                disabled={!localFilters.facultyId}
                // disabled={!localFilters.facultyId && departments.length === 0}
              >
                <option value="">All Departments</option>
                {departments && departments.map(dept => (
                  <option key={dept.id} value={dept.id}>{dept.name}</option>
                ))}
              </select>
            </div>

            <div style={filterItemStyle}>
              <label style={labelStyle}>Status</label>
              <select
                name="isActive"
                value={localFilters.isActive}
                onChange={handleChange}
                style={selectStyle}
              >
                <option value="">All</option>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
          </div>

          <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
            <button
              onClick={resetFilters}
              style={{
                padding: '10px 20px',
                background: 'var(--pure-white)',
                color: 'var(--text-muted)',
                border: '2px solid var(--border-color)',
                borderRadius: '8px',
                fontSize: '14px',
                fontWeight: '600',
                cursor: 'pointer'
              }}
            >
              Reset
            </button>
            <button
              onClick={applyFilters}
              style={{
                padding: '10px 20px',
                background: 'linear-gradient(135deg, var(--gold), var(--gold-light))',
                color: 'var(--navy-primary)',
                border: 'none',
                borderRadius: '8px',
                fontSize: '14px',
                fontWeight: '600',
                cursor: 'pointer'
              }}
            >
              Apply Filters
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default UserFilters;