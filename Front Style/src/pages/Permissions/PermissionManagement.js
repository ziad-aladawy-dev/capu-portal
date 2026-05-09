import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Shield, Save, Users, RefreshCw, Building2, BookOpen,
  X, CheckCircle2, ChevronDown, ChevronRight, Search, Filter
} from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import permissionService from '../../services/permissionService';
import userService from '../../services/userService';
import facultyService from '../../services/facultyService';
import authService from '../../services/authService';
import Sidebar from '../../components/layout/Sidebar/Sidebar';
import TopNav from '../../components/layout/TopNav/TopNav';
import LoadingSpinner from '../../components/UI/LoadingSpinner';
import ErrorMessage from '../../components/UI/ErrorMessage';
import './PermissionManagement.css';

const PermissionManagement = () => {
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  
  // UI States
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [showSuccess, setShowSuccess] = useState(false);
  const [expandedPermissions, setExpandedPermissions] = useState({});
  
  // Filters
  const [faculties, setFaculties] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [selectedFacultyId, setSelectedFacultyId] = useState('');
  const [selectedDepartmentId, setSelectedDepartmentId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  
  // Users
  const [users, setUsers] = useState([]);
  const [filteredUsers, setFilteredUsers] = useState([]);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [selectedUser, setSelectedUser] = useState(null);
  const [loadingUsers, setLoadingUsers] = useState(false);
  const [searchParams] = useSearchParams();

  
  // Permissions Data
  const [modules, setModules] = useState([]);
  const [permissionTypes, setPermissionTypes] = useState([]);
  const [facultiesList, setFacultiesList] = useState([]);
  const [programsList, setProgramsList] = useState([]);
  
  // Current selected module tab
  const [activeModuleTab, setActiveModuleTab] = useState('');
  
  // User Permissions (loaded from API)
  const [userPermissions, setUserPermissions] = useState({});
  const [permissionChanges, setPermissionChanges] = useState({});
  const [hasChanges, setHasChanges] = useState(false);

  // Load initial data
  useEffect(() => {
    loadInitialData();
  }, []);

  // Load users when filters change
  useEffect(() => {
    loadUsers();
  }, [selectedFacultyId, selectedDepartmentId, searchTerm]);

  // Load user permissions when user is selected
  useEffect(() => {
    if (selectedUserId) {
      loadUserPermissions(selectedUserId);
    } else {
      resetPermissionsState();
    }
  }, [selectedUserId]);

  // Load departments when faculty changes
  useEffect(() => {
    if (selectedFacultyId) {
      loadDepartments(selectedFacultyId);
    } else {
      setDepartments([]);
      setSelectedDepartmentId('');
    }
  }, [selectedFacultyId]);

  useEffect(() => {
    const userId = searchParams.get('userId');
    if (userId) {
      setSelectedUserId(userId);
      // Also select this user in the list
      const user = users.find(u => u.id === userId);
      if (user) {
        setSelectedUser(user);
      }
    }
  }, [searchParams, users]);

  const loadInitialData = async () => {
    setLoading(true);
    try {
      const [modulesData, permTypesData, facultiesData, programsData] = await Promise.all([
        permissionService.getModules(),
        permissionService.getPermissionTypes(),
        permissionService.getFaculties(),
        permissionService.getPrograms()
      ]);
      
      setModules(modulesData || []);
      setPermissionTypes(permTypesData || []);
      setFacultiesList(facultiesData || []);
      setProgramsList(programsData || []);
      
      // Set first module as active if available
      if (modulesData && modulesData.length > 0) {
        setActiveModuleTab(modulesData[0].id);
      }
      
      // Also load faculties for filter dropdown
      const facultiesFilterData = await facultyService.getFacultiesLookup();
      setFaculties(facultiesFilterData || []);
    } catch (err) {
      console.error('Failed to load initial data:', err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const loadDepartments = async (facultyId) => {
    if (!facultyId) {
      setDepartments([]);
      return;
    }
    try {
      const programs = await facultyService.getProgramsByFaculty(facultyId);
      setDepartments(programs || []);
    } catch (err) {
      console.error('Failed to load departments:', err);
      setDepartments([]);
    }
  };

  const loadUsers = async () => {
    setLoadingUsers(true);
    try {
      const filters = { pageSize: 100, isActive: true };
      if (selectedFacultyId) filters.facultyIds = [selectedFacultyId];
      if (selectedDepartmentId) filters.departmentIds = [selectedDepartmentId];
      if (searchTerm) filters.searchTerm = searchTerm;
      
      const response = await userService.getStaff(filters);
      const usersList = (response.items || []).map(user => ({
        ...user,
        displayName: user.fullNameEn || user.fullName || user.email?.split('@')[0] || 'User',
        userTypeDisplay: getUserTypeLabel(user.userType || user.roleName)
      }));
      
      setUsers(usersList);
      setFilteredUsers(usersList);
    } catch (err) {
      console.error('Failed to load staff:', err);
      setUsers([]);
      setFilteredUsers([]);
    } finally {
      setLoadingUsers(false);
    }
  };

  // const loadUsers = async () => {
  //   setLoadingUsers(true);
  //   try {
  //     const filters = { pageSize: 100, isActive: true };
  //     if (selectedFacultyId) filters.facultyIds = [selectedFacultyId];
  //     if (selectedDepartmentId) filters.departmentIds = [selectedDepartmentId];
  //     if (searchTerm) filters.searchTerm = searchTerm;
      
  //     // Use getStaff instead of getUsers
  //     const response = await userService.getStaff(filters);
  //     const usersList = response.items || [];
  //     setUsers(usersList);
  //     setFilteredUsers(usersList);
  //   } catch (err) {
  //     console.error('Failed to load staff:', err);
  //     setUsers([]);
  //     setFilteredUsers([]);
  //   } finally {
  //     setLoadingUsers(false);
  //   }
  // };

  const loadUserPermissions = async (userId) => {
    setLoading(true);
    setError(null);
    try {
      // Get user details
      const userData = await userService.getUserById(userId);
      setSelectedUser(userData);
      
      // Get grouped permissions for this user
      const groupedPermissions = await permissionService.getStaffPermissionsGrouped(userId);
      
      // Build permissions map
      const permissionsMap = {};
      for (const module of modules) {
        permissionsMap[module.id] = {
          moduleId: module.id,
          moduleKey: module.moduleKey,
          displayName: module.displayNameEn,
          permissions: {}
        };
        
        for (const permType of permissionTypes) {
          const existing = groupedPermissions.find(p => 
            p.moduleId === module.id && p.permissionTypeId === permType.id
          );
          
          permissionsMap[module.id].permissions[permType.id] = {
            permissionTypeId: permType.id,
            name: permType.nameEn,
            nameAr: permType.nameAr,
            weight: permType.weight,
            isAssigned: !!existing,
            scopes: existing?.scopes || [],
            expiresAt: existing?.expiresAt
          };
        }
      }
      
      setUserPermissions(permissionsMap);
      setPermissionChanges({});
      setHasChanges(false);
      
      // Expand all permissions that have scopes
      const expanded = {};
      for (const module of modules) {
        for (const permType of permissionTypes) {
          const perm = permissionsMap[module.id]?.permissions[permType.id];
          if (perm?.isAssigned && perm.scopes.length > 0) {
            const key = `${module.id}_${permType.id}`;
            expanded[key] = true;
          }
        }
      }
      setExpandedPermissions(expanded);
      
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const resetPermissionsState = () => {
    setUserPermissions({});
    setPermissionChanges({});
    setExpandedPermissions({});
    setSelectedUser(null);
    setHasChanges(false);
  };

  const toggleExpand = (moduleId, permissionTypeId) => {
    const key = `${moduleId}_${permissionTypeId}`;
    setExpandedPermissions(prev => ({ ...prev, [key]: !prev[key] }));
  };

  const getCurrentScopes = (moduleId, permissionTypeId) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    if (permissionChanges[changeKey]?.scopes !== undefined) {
      return permissionChanges[changeKey].scopes;
    }
    return userPermissions[moduleId]?.permissions[permissionTypeId]?.scopes || [];
  };

  const getIsAssigned = (moduleId, permissionTypeId) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    if (permissionChanges[changeKey]?.isAssigned !== undefined) {
      return permissionChanges[changeKey].isAssigned;
    }
    return userPermissions[moduleId]?.permissions[permissionTypeId]?.isAssigned || false;
  };

  const handlePermissionToggle = (moduleId, permissionTypeId, isAssigned) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    const currentScopes = getCurrentScopes(moduleId, permissionTypeId);
    
    setPermissionChanges(prev => ({
      ...prev,
      [changeKey]: {
        ...prev[changeKey],
        isAssigned: isAssigned,
        scopes: isAssigned ? (currentScopes.length > 0 ? currentScopes : [{ facultyId: null, programId: null }]) : []
      }
    }));
    setHasChanges(true);
  };

  const addNewScope = (moduleId, permissionTypeId) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    const currentScopes = [...getCurrentScopes(moduleId, permissionTypeId)];
    
    // Check if there's already an "All" scope
    const hasAllScope = currentScopes.some(s => s.facultyId === null && s.programId === null);
    if (hasAllScope) {
      alert("Cannot add specific scopes when 'All' scope is selected. Remove 'All' scope first.");
      return;
    }
    
    // Add new empty scope
    currentScopes.push({ facultyId: null, programId: null });
    
    setPermissionChanges(prev => ({
      ...prev,
      [changeKey]: {
        ...prev[changeKey],
        isAssigned: true,
        scopes: currentScopes
      }
    }));
    setHasChanges(true);
    
    // Auto expand this permission
    const expandKey = `${moduleId}_${permissionTypeId}`;
    setExpandedPermissions(prev => ({ ...prev, [expandKey]: true }));
  };

  const updateScope = (moduleId, permissionTypeId, scopeIndex, field, value) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    const currentScopes = [...getCurrentScopes(moduleId, permissionTypeId)];
    
    if (currentScopes[scopeIndex]) {
      const newValue = value === '' ? null : value;
      
      if (field === 'facultyId') {
        currentScopes[scopeIndex] = { facultyId: newValue, programId: null };
      } else if (field === 'programId') {
        let facultyId = currentScopes[scopeIndex].facultyId;
        if (newValue && !facultyId) {
          const program = programsList.find(p => p.id === newValue);
          if (program) facultyId = program.facultyId;
        }
        currentScopes[scopeIndex] = { facultyId, programId: newValue };
      }
    }
    
    // Remove duplicates
    const uniqueScopes = [];
    const seen = new Set();
    for (const scope of currentScopes) {
      const key = `${scope.facultyId || 'null'}_${scope.programId || 'null'}`;
      if (!seen.has(key)) {
        seen.add(key);
        uniqueScopes.push(scope);
      }
    }
    
    setPermissionChanges(prev => ({
      ...prev,
      [changeKey]: {
        ...prev[changeKey],
        isAssigned: true,
        scopes: uniqueScopes
      }
    }));
    setHasChanges(true);
  };

  const removeScope = (moduleId, permissionTypeId, scopeIndex) => {
    const changeKey = `${moduleId}_${permissionTypeId}`;
    const currentScopes = [...getCurrentScopes(moduleId, permissionTypeId)];
    currentScopes.splice(scopeIndex, 1);
    
    setPermissionChanges(prev => ({
      ...prev,
      [changeKey]: {
        ...prev[changeKey],
        isAssigned: currentScopes.length > 0,
        scopes: currentScopes
      }
    }));
    setHasChanges(true);
  };

  const getProgramsByFaculty = (facultyId) => {
    return programsList.filter(p => p.facultyId === facultyId);
  };

  const handleSavePermissions = async () => {
    setSaving(true);
    try {
      const permissionsToUpdate = [];
      const allPermissionsMap = new Map();
      
      // Collect all permissions (changed + unchanged)
      for (const module of modules) {
        for (const permType of permissionTypes) {
          const key = `${module.id}_${permType.id}`;
          const isAssigned = getIsAssigned(module.id, permType.id);
          let scopes = getCurrentScopes(module.id, permType.id);
          
          // Remove duplicates
          const uniqueScopes = [];
          const seen = new Set();
          for (const scope of scopes) {
            const scopeKey = `${scope.facultyId || 'null'}_${scope.programId || 'null'}`;
            if (!seen.has(scopeKey)) {
              seen.add(scopeKey);
              uniqueScopes.push(scope);
            }
          }
          
          allPermissionsMap.set(key, {
            moduleId: module.id,
            permissionTypeId: permType.id,
            isAssigned: isAssigned,
            scopes: uniqueScopes
          });
        }
      }
      
      // Convert map to array
      for (const [_, value] of allPermissionsMap) {
        permissionsToUpdate.push(value);
      }
      
      await permissionService.updateStaffPermissionsBulk(selectedUserId, permissionsToUpdate);
      
      setShowSuccess(true);
      setTimeout(() => setShowSuccess(false), 2000);
      setHasChanges(false);
      setPermissionChanges({});
      
      // Reload permissions
      await loadUserPermissions(selectedUserId);
    } catch (err) {
      console.error('Save error:', err);
      alert(`Failed to save permissions: ${err.message}`);
    } finally {
      setSaving(false);
    }
  };

  const clearFilters = () => {
    setSelectedFacultyId('');
    setSelectedDepartmentId('');
    setSearchTerm('');
  };

  const getUserTypeLabel = (userType) => {
    const types = {
      'SystemAdmin': 'System Admin',
      'AcademicSupervisor': 'Academic Supervisor',
      'FinanceOfficer': 'Finance Officer',
      'Registrar': 'Registrar',
      'Professor': 'Professor'
    };
    return types[userType] || 'Staff';
  };

  const getFacultyName = (facultyId) => {
    const faculty = facultiesList.find(f => f.id === facultyId);
    return faculty ? faculty.nameEn : '';
  };

  const getProgramName = (programId) => {
    const program = programsList.find(p => p.id === programId);
    return program ? program.nameEn : '';
  };

  const currentModule = modules.find(m => m.id === activeModuleTab);
  const currentModulePermissions = currentModule ? userPermissions[currentModule.id] : null;

  if (loading && !userPermissions) {
    return <LoadingSpinner fullPage message="Loading permissions data..." />;
  }

  return (
    <div className="dashboard-container">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <TopNav onMenuClick={() => setSidebarOpen(true)} />

      <div className="main-content">
        <div className="permissions-container">
          <div className="permissions-header">
            <div className="permissions-title">
              <Shield size={28} color="var(--gold)" />
              <h2>User Permissions Management</h2>
            </div>
          </div>

          <div className="permissions-layout">
            {/* Left Panel - User Selection */}
            <div className="users-panel">
              <div className="panel-header">
                <Users size={18} />
                <h3>Select User</h3>
                <button 
                  className="filter-toggle-btn"
                  onClick={() => setShowFilters(!showFilters)}
                >
                  <Filter size={14} />
                </button>
              </div>
              
              {/* Filters */}
              {showFilters && (
                <div className="user-filters">
                  <div className="filter-group">
                    <label>Faculty</label>
                    <select
                      value={selectedFacultyId}
                      onChange={(e) => setSelectedFacultyId(e.target.value)}
                    >
                      <option value="">All Faculties</option>
                      {faculties.map(f => (
                        <option key={f.id} value={f.id}>{f.name}</option>
                      ))}
                    </select>
                  </div>
                  
                  <div className="filter-group">
                    <label>Department</label>
                    <select
                      value={selectedDepartmentId}
                      onChange={(e) => setSelectedDepartmentId(e.target.value)}
                      disabled={!selectedFacultyId}
                    >
                      <option value="">All Departments</option>
                      {departments.map(d => (
                        <option key={d.id} value={d.id}>{d.name}</option>
                      ))}
                    </select>
                  </div>
                  
                  <div className="filter-group search">
                    <Search size={14} className="search-icon" />
                    <input
                      type="text"
                      placeholder="Search by name or email..."
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                    />
                    {(selectedFacultyId || selectedDepartmentId || searchTerm) && (
                      <button className="clear-filters" onClick={clearFilters}>
                        <X size={14} />
                      </button>
                    )}
                  </div>
                </div>
              )}
              
              {/* Users List */}
              <div className="users-list">
                {loadingUsers ? (
                  <div className="loading-users">
                    <LoadingSpinner message="Loading users..." />
                  </div>
                ) : filteredUsers.length === 0 ? (
                  <div className="no-users">
                    <Users size={32} />
                    <p>No users found</p>
                  </div>
                ) : (
                  filteredUsers.map(user => (
                    <div
                      key={user.id}
                      className={`user-item ${selectedUserId === user.id ? 'active' : ''}`}
                      onClick={() => setSelectedUserId(user.id)}
                    >
                      <div className="user-avatar">
                        {user.displayName?.charAt(0) || user.email?.charAt(0) || 'U'}
                      </div>
                      <div className="user-info">
                      <div className="user-name">{user.displayName || user.fullName || user.email}</div>
                        {/* <div className="user-name">{user.fullName || user.email}</div> */}
                        <div className="user-details">
                          <span className="user-type">{getUserTypeLabel(user.userType)}</span>
                          {user.email && <span className="user-email">{user.email}</span>}
                        </div>
                      </div>
                      {selectedUserId === user.id && (
                        <div className="selected-indicator">
                          <CheckCircle2 size={16} color="#16a34a" />
                        </div>
                      )}
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Right Panel - Permissions */}
            <div className="permissions-panel">
              {!selectedUserId ? (
                <div className="empty-state">
                  <Shield size={48} />
                  <p>Select a user from the left panel to manage their permissions</p>
                </div>
              ) : loading ? (
                <LoadingSpinner message="Loading permissions..." />
              ) : error ? (
                <ErrorMessage message={error} onRetry={() => loadUserPermissions(selectedUserId)} />
              ) : (
                <>
                  {/* Selected User Info */}
                  <div className="selected-user-info">
                    <div className="user-avatar-large">
                      {selectedUser?.fullNameEn?.charAt(0) || selectedUser?.fullNameAr?.charAt(0) || 'U'}
                    </div>
                    <div className="user-details">
                      <h3>{selectedUser?.fullNameEn}</h3>
                      <div className="user-badges">
                        {/* {selectedUser?.roleName && <span className="badge role">{selectedUser.roleName}</span>} */}
                        {/* {selectedUser?.staffCode && <span className="badge code">{selectedUser.staffCode}</span>} */}
                      </div>
                    </div>
                  </div>

                  {/* Module Tabs */}
                  <div className="module-tabs">
                    {modules.map(module => {
                      const hasPermissions = userPermissions[module.id]?.permissions && 
                        Object.values(userPermissions[module.id].permissions).some(p => p.isAssigned);
                      return (
                        <button
                          key={module.id}
                          className={`module-tab ${activeModuleTab === module.id ? 'active' : ''} ${hasPermissions ? 'has-permissions' : ''}`}
                          onClick={() => setActiveModuleTab(module.id)}
                        >
                          {module.displayNameEn}
                          {hasPermissions && <span className="permission-dot"></span>}
                        </button>
                      );
                    })}
                  </div>

                  {/* Permissions for Selected Module */}
                  {currentModule && currentModulePermissions && (
                    <div className="permissions-content">
                      <div className="module-description">
                        <h4>{currentModule.displayNameEn}</h4>
                        <p>Manage permissions for this module. Select scope (Faculty/Program) if needed.</p>
                      </div>
                      
                      <div className="permissions-list">
                        {permissionTypes
                          .sort((a, b) => a.weight - b.weight)
                          .map(permType => {
                            const isAssigned = getIsAssigned(currentModule.id, permType.id);
                            const scopes = getCurrentScopes(currentModule.id, permType.id);
                            const isExpanded = expandedPermissions[`${currentModule.id}_${permType.id}`];
                            
                            return (
                              <div key={permType.id} className="permission-item">
                                <div className="permission-header">
                                  <label className="permission-checkbox">
                                    <input
                                      type="checkbox"
                                      checked={isAssigned}
                                      onChange={(e) => handlePermissionToggle(currentModule.id, permType.id, e.target.checked)}
                                    />
                                    <span className="permission-name">{permType.nameEn}</span>
                                    <span className="permission-name-ar">({permType.nameAr})</span>
                                  </label>
                                  {isAssigned && scopes.length > 0 && (
                                    <button
                                      className="expand-scope-btn"
                                      onClick={() => toggleExpand(currentModule.id, permType.id)}
                                    >
                                      {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                                      <span>{scopes.length} scope{scopes.length !== 1 ? 's' : ''}</span>
                                    </button>
                                  )}
                                </div>
                                
                                {isAssigned && isExpanded && (
                                  <div className="permission-scopes">
                                    <div className="scopes-header">
                                      <span className="scopes-title">Scope Restrictions</span>
                                      <button
                                        type="button"
                                        className="add-scope-btn"
                                        onClick={() => addNewScope(currentModule.id, permType.id)}
                                      >
                                        + Add Scope
                                      </button>
                                    </div>
                                    
                                    <div className="scopes-list">
                                      {scopes.length === 0 ? (
                                        <div className="no-scope-message">
                                          No scope restrictions. This permission applies to everything.
                                        </div>
                                      ) : (
                                        scopes.map((scope, idx) => (
                                          <div key={idx} className="scope-item">
                                            <div className="scope-selects">
                                              <select
                                                value={scope.facultyId || ''}
                                                onChange={(e) => updateScope(currentModule.id, permType.id, idx, 'facultyId', e.target.value)}
                                                className="scope-select"
                                              >
                                                <option value="">All Faculties</option>
                                                {facultiesList.map(f => (
                                                  <option key={f.id} value={f.id}>{f.nameEn}</option>
                                                ))}
                                              </select>
                                              
                                              {scope.facultyId && (
                                                <select
                                                  value={scope.programId || ''}
                                                  onChange={(e) => updateScope(currentModule.id, permType.id, idx, 'programId', e.target.value)}
                                                  className="scope-select"
                                                >
                                                  <option value="">All Programs</option>
                                                  {getProgramsByFaculty(scope.facultyId).map(p => (
                                                    <option key={p.id} value={p.id}>{p.nameEn}</option>
                                                  ))}
                                                </select>
                                              )}
                                            </div>
                                            <button
                                              type="button"
                                              className="remove-scope-btn"
                                              onClick={() => removeScope(currentModule.id, permType.id, idx)}
                                              title="Remove this scope"
                                            >
                                              <X size={14} />
                                            </button>
                                          </div>
                                        ))
                                      )}
                                    </div>
                                  </div>
                                )}
                              </div>
                            );
                          })}
                      </div>
                    </div>
                  )}

                  {/* Save Button */}
                  <div className="save-section">
                    <button
                      className={`save-permissions-btn ${hasChanges ? 'has-changes' : ''}`}
                      onClick={handleSavePermissions}
                      disabled={saving || !hasChanges}
                    >
                      <Save size={18} />
                      {saving ? 'Saving...' : hasChanges ? 'Save Changes' : 'No Changes'}
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Success Toast */}
      {showSuccess && (
        <div className="success-toast">
          <CheckCircle2 size={20} color="#16a34a" />
          <span>Permissions saved successfully!</span>
        </div>
      )}
    </div>
  );
};

export default PermissionManagement;