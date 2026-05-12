import { useState } from "react";
import { NavLink } from "react-router-dom";
import {
  ChevronRight,
  LogOut,
  Building2,
  Plug,
  LayoutDashboard,
  Users,
  UserPlus,
  Shield,
} from "lucide-react";

import "./sidebar.css";

const categoryIcons = {
  overview: <LayoutDashboard size={14} />,
  administration: <Building2 size={14} />,
  integration: <Plug size={14} />,
};

function Sidebar({ isOpen, isMobile, onClose }) {
  const [openedCategory, setOpenedCategory] = useState("administration");

  const categories = [
    {
      key: "overview",
      title: "Overview",
      items: [{ label: "Dashboard", path: "/admin/dashboard", icon: <LayoutDashboard size={13} /> }],
    },
    {
      key: "administration",
      title: "Administration",
      items: [
        { label: "University Structure", path: "/admin/faculties", icon: <Building2 size={13} /> },
        { label: "Users Management", path: "/admin/users", icon: <Users size={13} /> },
        { label: "Add Student", path: "/admin/users/add-student", icon: <UserPlus size={13} /> },
        { label: "Add Staff", path: "/admin/users/add-staff", icon: <UserPlus size={13} /> },
        { label: "Permissions", path: "/admin/permissions", icon: <Shield size={13} /> },
      ],
    },
    {
      key: "integration",
      title: "Integration",
      items: [{ label: "SIS Sync", path: "/admin/sync", icon: <Plug size={13} /> }],
    },
  ];

  const handleFeatureClick = () => {
    if (isMobile && onClose) onClose();
  };

  return (
    <aside className={`sidebar ${isOpen ? "is-open" : "is-closed"}`}>
      <svg className="sidebar-geo" viewBox="0 0 230 620" preserveAspectRatio="none">
        <circle cx="230" cy="0" r="140" fill="rgba(224,192,106,0.04)" />
        <circle cx="0" cy="460" r="110" fill="rgba(35,42,116,0.35)" />
        <line x1="115" y1="80" x2="230" y2="200" stroke="rgba(224,192,106,0.06)" strokeWidth="1" />
        <line x1="0" y1="300" x2="115" y2="180" stroke="rgba(224,192,106,0.04)" strokeWidth="1" />
      </svg>

      <div className="sidebar-logo">
        <div className="sidebar-logo-mark">
          <svg viewBox="0 0 20 20" fill="none">
            <path d="M10 2L18 7V13L10 18L2 13V7L10 2Z" fill="#07091e" />
            <path d="M10 5L15 8V12L10 15L5 12V8L10 5Z" fill="#e0c06a" opacity="0.6" />
            <circle cx="10" cy="10" r="2" fill="#07091e" />
          </svg>
        </div>
        <div className="sidebar-logo-text">
          UniAdmin
          <small>Control Panel</small>
        </div>
      </div>

      <div className="sidebar-top">
        <div className="sidebar-user-card">
          <div className="sidebar-avatar">A</div>
          <div className="sidebar-user-info">
            <strong>Admin User</strong>
            <span>Super Administrator</span>
          </div>
          <div className="sidebar-user-badge" />
        </div>
      </div>

      <div className="sidebar-content">
        <div className="sidebar-section-label">Menu</div>

        {categories.map((category) => {
          const opened = openedCategory === category.key;

          return (
            <div className="sidebar-category" key={category.key}>
              <button
                type="button"
                className={`sidebar-category-header ${opened ? "is-open" : ""}`}
                onClick={() => setOpenedCategory(opened ? null : category.key)}
              >
                <div className="sidebar-cat-icon">{categoryIcons[category.key]}</div>
                <span className="sidebar-category-title">{category.title}</span>
                <ChevronRight size={11} className="sidebar-cat-arrow" />
              </button>

              {opened && (
                <div className="sidebar-features">
                  {category.items.map((item) => (
                    <NavLink
                      key={item.path}
                      to={item.path}
                      end={item.path === "/admin/users" || item.path === "/admin/dashboard"}
                      onClick={handleFeatureClick}
                      className={({ isActive }) =>
                        `sidebar-feature ${isActive ? "active" : ""}`
                      }
                    >
                      <span className="sidebar-feature-dot" />
                      {item.icon}
                      <span>{item.label}</span>
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="sidebar-footer">
        <button className="sidebar-footer-btn">
          <LogOut size={14} />
          <span>Sign out</span>
        </button>
      </div>
    </aside>
  );
}

export default Sidebar;
