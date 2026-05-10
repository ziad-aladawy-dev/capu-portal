import React from "react";
import { useAuth } from "../hooks/use-auth";
import { useScope } from "../hooks/use-scope";
import { BarChart3, Users, FileText, Settings, Grid3X3, LogOut } from "lucide-react";
import { useNavigate } from "react-router-dom";
import "./Dashboard.css";

export const Dashboard = () => {
  const { user, logout } = useAuth();
  const { activeScope } = useScope();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  const modules = [
    {
      id: "students",
      icon: Users,
      label: "Students",
      description: "Manage student records and enrollments",
      color: "#3b82f6",
      path: "/students"
    },
    {
      id: "admin",
      icon: Settings,
      label: "Admin",
      description: "User management and system settings",
      color: "#8b5cf6",
      path: "/admin"
    },
    {
      id: "financial",
      icon: BarChart3,
      label: "Financial",
      description: "Manage financial records and reports",
      color: "#10b981",
      path: "/financial"
    },
    {
      id: "registration",
      icon: FileText,
      label: "Registration",
      description: "Handle course registration and scheduling",
      color: "#f59e0b",
      path: "/registration"
    },
    {
      id: "permissions",
      icon: Grid3X3,
      label: "Permissions",
      description: "Manage roles and access permissions",
      color: "#ef4444",
      path: "/permissions"
    }
  ];

  return (
    <div className="dashboard-container">
      {/* Welcome Section */}
      <div className="dashboard-welcome">
        <div className="welcome-content">
          <h1>Welcome back, {user?.name || "Admin"}!</h1>
          <p>You have access to the modules below based on your permissions</p>
        </div>
        <button onClick={handleLogout} className="logout-button">
          <LogOut size={18} />
          Logout
        </button>
      </div>

      {/* Scope Info */}
      <div className="scope-info">
        <div className="scope-card">
          <div className="scope-label">Current Scope</div>
          <div className="scope-value">
            {activeScope?.colleges?.length > 0 
              ? `${activeScope.colleges.length} College(s)` 
              : "All Colleges"}
          </div>
          <div className="scope-detail">
            {activeScope?.academicYear && `${activeScope.academicYear} / ${activeScope.semester || "All"}`}
          </div>
        </div>

        <div className="user-info-card">
          <div className="info-label">Role</div>
          <div className="info-value">{user?.role || "Admin"}</div>
          <div className="info-detail">
            {user?.email || "admin@capu.edu"}
          </div>
        </div>
      </div>

      {/* Modules Grid */}
      <div className="dashboard-modules">
        <h2>Available Modules</h2>
        <div className="modules-grid">
          {modules.map((module) => {
            const IconComponent = module.icon;
            return (
              <button
                key={module.id}
                className="module-card"
                onClick={() => navigate(module.path)}
                style={{ "--module-color": module.color }}
              >
                <div className="module-icon">
                  <IconComponent size={32} />
                </div>
                <h3>{module.label}</h3>
                <p>{module.description}</p>
                <div className="module-arrow">→</div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Quick Stats */}
      <div className="dashboard-stats">
        <h2>Quick Stats</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-number">2,847</div>
            <div className="stat-label">Total Students</div>
          </div>
          <div className="stat-card">
            <div className="stat-number">156</div>
            <div className="stat-label">Active Users</div>
          </div>
          <div className="stat-card">
            <div className="stat-number">8</div>
            <div className="stat-label">Departments</div>
          </div>
          <div className="stat-card">
            <div className="stat-number">42</div>
            <div className="stat-label">Courses</div>
          </div>
        </div>
      </div>
    </div>
  );
};
