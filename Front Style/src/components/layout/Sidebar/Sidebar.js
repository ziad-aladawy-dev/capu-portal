import React, { useMemo, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import {
  Home,
  Users,
  Building2,
  BookOpen,
  GraduationCap,
  BarChart3,
  Settings,
  LogOut,
  X,
  Search,
  ChevronRight,
  Share2,
  Shield,
  Database
} from "lucide-react";

import authService from "../../../services/authService";
import "./Sidebar.css";

const Sidebar = ({ isOpen, onClose }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchTerm, setSearchTerm] = useState("");

  const currentUser = authService.getCurrentUser();

  const isActive = (path) => {
    if (path === "/dashboard" && location.pathname === "/") return true;
    return location.pathname.startsWith(path);
  };

  const navItems = [
    { icon: Home, label: "Dashboard", path: "/dashboard" },
    { icon: Building2, label: "University Structure", path: "/university-tree" },
    { icon: Shield, label: "Permissions", path: "/permissions" },
    { icon: Database, label: "Integration Sync", path: "/sync" },
    { icon: Users, label: "Users", path: "/users" },
    // { icon: GraduationCap, label: "Faculties", path: "/faculties" },
    // { icon: BookOpen, label: "Courses", path: "/courses" },
    // { icon: BarChart3, label: "Reports", path: "/reports" },
    // { icon: Settings, label: "Settings", path: "/settings" },
  ];

  const filteredNavItems = useMemo(() => {
    return navItems.filter((item) =>
      item.label.toLowerCase().includes(searchTerm.toLowerCase())
    );
  }, [searchTerm]);

  const handleNavigate = (path) => {
    navigate(path);
    onClose();
  };

  const handleLogout = () => {
    authService.logout();
    navigate("/login");
    onClose();
  };

  return (
    <>
      <div
        className={`sidebar-overlay ${isOpen ? "show" : ""}`}
        onClick={onClose}
      />

      <aside className={`sidebar ${isOpen ? "open" : ""}`}>
        <div className="sidebar-header">
          <div className="sidebar-brand">
            <div className="brand-icon">
              <GraduationCap size={21} />
            </div>
            <div className="brand-text">
              <span className="brand-title">Capital University</span>
              <span className="brand-subtitle">Admin Management System</span>
            </div>
          </div>
          <button className="sidebar-close" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        <div className="sidebar-section">MAIN MENU</div>

        <nav className="sidebar-nav">
          {filteredNavItems.map((item, i) => {
            const active = isActive(item.path);
            const Icon = item.icon;

            return (
              <button
                key={i}
                className={`nav-item ${active ? "active" : ""}`}
                onClick={() => handleNavigate(item.path)}
              >
                <div className="nav-icon-wrap">
                  <Icon size={18} />
                </div>
                <span>{item.label}</span>
                {active ? (
                  <div className="active-pill"></div>
                ) : (
                  <ChevronRight size={14} className="nav-arrow" />
                )}
              </button>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user">
            <div className="user-avatar">
              {currentUser?.name?.charAt(0) || "A"}
            </div>
            <div className="user-info">
              <p>{currentUser?.name || "Admin"}</p>
              <span>System Administrator</span>
            </div>
          </div>
          <button className="logout-btn" onClick={handleLogout}>
            <LogOut size={16} />
            <span>Logout</span>
          </button>
        </div>
      </aside>
    </>
  );
};

export default Sidebar;