import React, { useState } from "react";
import { Users as UsersIcon, Search, Plus, Edit, Trash2, ChevronDown, X } from "lucide-react";
import { mockUsers, mockRoles } from "../lib/mock-data";
import "./Users.css";

export const Users = () => {
  const [users, setUsers] = useState(mockUsers);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedUser, setSelectedUser] = useState(null);
  const [showAddModal, setShowAddModal] = useState(false);

  const filteredUsers = users.filter(user =>
    user.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    user.email.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handleRoleChange = (userId, newRoleId) => {
    setUsers(prev => prev.map(user =>
      user.id === userId ? { ...user, roleId: newRoleId } : user
    ));
  };

  const handleRemoveOverride = (userId, moduleId) => {
    setUsers(prev => prev.map(user => {
      if (user.id === userId) {
        const newOverrides = (user.permissionOverrides || []).filter(
          o => o.module !== moduleId
        );
        return { ...user, permissionOverrides: newOverrides };
      }
      return user;
    }));
  };

  const handleAddOverride = (userId, moduleId, type) => {
    setUsers(prev => prev.map(user => {
      if (user.id === userId) {
        const existing = (user.permissionOverrides || []).find(o => o.module === moduleId);
        if (existing) return user;
        return {
          ...user,
          permissionOverrides: [
            ...(user.permissionOverrides || []),
            { module: moduleId, type }
          ]
        };
      }
      return user;
    }));
  };

  const getRoleName = (roleId) => {
    const role = mockRoles.find(r => r.id === roleId);
    return role?.name || "Unknown";
  };

  const availableModules = ["students", "admin", "financial", "registration", "permissions"];

  return (
    <div className="users-page">
      <div className="page-header">
        <div className="header-title">
          <UsersIcon size={24} />
          <h1>User Management</h1>
        </div>
        <button className="add-user-button" onClick={() => setShowAddModal(true)}>
          <Plus size={18} />
          Add User
        </button>
      </div>

      <div className="search-bar">
        <Search size={20} />
        <input
          type="text"
          placeholder="Search users..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="users-table">
        <div className="table-header">
          <div className="col-user">User</div>
          <div className="col-role">Role</div>
          <div className="col-overrides">Permission Overrides</div>
          <div className="col-actions">Actions</div>
        </div>

        <div className="table-body">
          {filteredUsers.map(user => (
            <div key={user.id} className="table-row">
              <div className="col-user">
                <div className="user-avatar">{user.name.charAt(0)}</div>
                <div className="user-info">
                  <div className="user-name">{user.name}</div>
                  <div className="user-email">{user.email}</div>
                </div>
              </div>

              <div className="col-role">
                <select
                  value={user.roleId}
                  onChange={(e) => handleRoleChange(user.id, parseInt(e.target.value))}
                  className="role-select"
                >
                  {mockRoles.map(role => (
                    <option key={role.id} value={role.id}>{role.name}</option>
                  ))}
                </select>
              </div>

              <div className="col-overrides">
                <div className="overrides-list">
                  {(user.permissionOverrides || []).map(override => (
                    <span
                      key={override.module}
                      className={`override-badge ${override.type}`}
                    >
                      {override.type === "added" ? "+" : "−"} {override.module}
                      <button
                        className="remove-override"
                        onClick={() => handleRemoveOverride(user.id, override.module)}
                      >
                        <X size={12} />
                      </button>
                    </span>
                  ))}
                </div>
                <div className="add-override-wrapper">
                  <select
                    className="add-override-select"
                    value=""
                    onChange={(e) => {
                      if (e.target.value) {
                        const [module, type] = e.target.value.split(":");
                        handleAddOverride(user.id, module, type);
                        e.target.value = "";
                      }
                    }}
                  >
                    <option value="">+ Add override</option>
                    {availableModules
                      .filter(m => !(user.permissionOverrides || []).some(o => o.module === m))
                      .map(m => (
                        <React.Fragment key={m}>
                          <option value={`${m}:added`}>+ {m}</option>
                          <option value={`${m}:removed`}>− {m}</option>
                        </React.Fragment>
                      ))}
                  </select>
                </div>
              </div>

              <div className="col-actions">
                <button className="action-button edit" title="Edit user">
                  <Edit size={16} />
                </button>
                <button className="action-button delete" title="Delete user">
                  <Trash2 size={16} />
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>

      {showAddModal && (
        <div className="modal-backdrop" onClick={() => setShowAddModal(false)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Add New User</h2>
              <button className="close-btn" onClick={() => setShowAddModal(false)}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>Name</label>
                <input type="text" placeholder="Enter name" />
              </div>
              <div className="form-group">
                <label>Email</label>
                <input type="email" placeholder="Enter email" />
              </div>
              <div className="form-group">
                <label>Role</label>
                <select>
                  {mockRoles.map(role => (
                    <option key={role.id} value={role.id}>{role.name}</option>
                  ))}
                </select>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn-secondary" onClick={() => setShowAddModal(false)}>
                Cancel
              </button>
              <button className="btn-primary">Add User</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};