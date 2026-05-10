import React, { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { LogOut, GraduationCap, ChevronRight, ChevronDown, User, Settings } from "lucide-react";
import { useAuth } from "../../hooks/use-auth";
import { MODULES } from "../../lib/constants";
import "./AppSidebar.css";

export const AppSidebar = ({ isOpen, onClose }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout, moduleVisibility } = useAuth();
  const [expandedModule, setExpandedModule] = useState(null);

  // Navigation items structure with sub-items
  const navItems = [
    {
      id: "dashboard",
      label: MODULES.DASHBOARD,
      icon: "BarChart3",
      path: "/dashboard",
      children: []
    },
    {
      id: "students",
      label: MODULES.STUDENTS,
      icon: "Users",
      path: "/students",
      children: [
        { label: "All Students", path: "/students/list" },
        { label: "Enrollment", path: "/students/enrollment" },
        { label: "Grades", path: "/students/grades" }
      ]
    },
    {
      id: "admin",
      label: MODULES.ADMIN,
      icon: "Shield",
      path: "/admin",
      children: [
        { label: "Users", path: "/admin/users" },
        { label: "Roles", path: "/admin/roles" },
        { label: "Departments", path: "/admin/departments" }
      ]
    },
    {
      id: "financial",
      label: MODULES.FINANCIAL,
      icon: "DollarSign",
      path: "/financial",
      children: [
        { label: "Billing", path: "/financial/billing" },
        { label: "Payments", path: "/financial/payments" }
      ]
    },
    {
      id: "registration",
      label: MODULES.REGISTRATION,
      icon: "BookOpen",
      path: "/registration",
      children: [
        { label: "Courses", path: "/registration/courses" },
        { label: "Registration", path: "/registration/manage" }
      ]
    },
    {
      id: "permissions",
      label: MODULES.PERMISSIONS,
      icon: "Lock",
      path: "/permissions",
      children: []
    }
  ];

  // Filter nav items based on module visibility
  const visibleNavItems = navItems.filter(item =>
    moduleVisibility.includes(item.label)
  );

  const handleNavigate = (path) => {
    navigate(path);
    onClose();
  };

  const toggleModule = (moduleId) => {
    setExpandedModule(expandedModule === moduleId ? null : moduleId);
  };

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  const isActive = (path) => location.pathname.startsWith(path);

  return (
    <>
      <div
        className={`sidebar-overlay ${isOpen ? "show" : ""}`}
        onClick={onClose}
      />

      <aside className={`app-sidebar ${isOpen ? "open" : ""}`}>
        {/* Header */}
        <div className="sidebar-header">
          <div className="sidebar-brand">
            <div className="brand-icon">
              <GraduationCap size={20} />
            </div>
            <div className="brand-text">
              <span className="brand-title">Capital University</span>
              <span className="brand-subtitle">Admin Portal</span>
            </div>
          </div>
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav">
          {visibleNavItems.map((item) => (
            <div key={item.id} className="nav-section">
              <button
                className={`nav-item ${expandedModule === item.id ? "expanded" : ""} ${isActive(item.path) ? "active" : ""}`}
                onClick={() => {
                  if (item.children.length > 0) {
                    toggleModule(item.id);
                  } else {
                    handleNavigate(item.path);
                  }
                }}
              >
                <span className="nav-label">{item.label}</span>
                {item.children.length > 0 && (
                  <span className="nav-chevron">
                    {expandedModule === item.id ? (
                      <ChevronDown size={16} />
                    ) : (
                      <ChevronRight size={16} />
                    )}
                  </span>
                )}
              </button>

              {item.children.length > 0 && expandedModule === item.id && (
                <div className="nav-children">
                  {item.children.map((child) => (
                    <button
                      key={child.path}
                      className="nav-child-item"
                      onClick={() => handleNavigate(child.path)}
                    >
                      {child.label}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </nav>

        {/* Footer */}
        <div className="sidebar-footer">
          <div className="user-card">
            <div className="user-avatar">
              {user?.name?.charAt(0) || "A"}
            </div>
            <div className="user-info">
              <p className="user-name">{user?.name || "Admin"}</p>
              <span className="user-role">Administrator</span>
            </div>
          </div>

          <div className="sidebar-actions">
            <button className="sidebar-action-btn" title="Profile">
              <User size={16} />
            </button>
            <button className="sidebar-action-btn" title="Settings">
              <Settings size={16} />
            </button>
            <button 
              className="sidebar-action-btn logout-btn"
              onClick={handleLogout}
              title="Logout"
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>
      </aside>
    </>
  );
};
