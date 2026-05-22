import { useState } from "react";
import { NavLink } from "react-router-dom";
import { ChevronRight, LogOut, LayoutDashboard, Building2, Users, Shield, BookOpen, Receipt } from "lucide-react";

import { buildMenu } from "../menuAggregator";
import { usePermission } from "../../auth/usePermission";
import { useAuth } from "../../auth/useAuth";
import "../../styles/sidebar.css";

const CATEGORY_ICONS = {
  Overview: LayoutDashboard,
  Administration: Building2,
  "People Management": Users,
  "Security & Access": Shield,
  Academic: BookOpen,
  Finance: Receipt,
};

function Sidebar({ isOpen, isMobile, onClose }) {
  const [openedCategory, setOpenedCategory] = useState("Overview");
  const { can } = usePermission();
  const { user, logout } = useAuth();

  const menu = buildMenu(can);

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

      {user && (
        <div className="sidebar-top">
          <div className="sidebar-user-card">
            <div className="sidebar-avatar">
              {user.name ? user.name.charAt(0).toUpperCase() : "?"}
            </div>
            <div className="sidebar-user-info">
              <strong>{user.name}</strong>
              <span>{user.role || "User"}</span>
            </div>
            <div className="sidebar-user-badge" />
          </div>
        </div>
      )}

      <div className="sidebar-content">
        <div className="sidebar-section-label">Menu</div>

        {menu.map((category) => {
          const opened = openedCategory === category.category;

          return (
            <div className="sidebar-category" key={category.category}>
              <button
                type="button"
                className={`sidebar-category-header ${opened ? "is-open" : ""}`}
                onClick={() => setOpenedCategory(opened ? null : category.category)}
              >
                <div className="sidebar-cat-icon">
                  {(() => {
                    const CatIcon = CATEGORY_ICONS[category.category] || Building2;
                    return <CatIcon size={14} />;
                  })()}
                </div>
                <span className="sidebar-category-title">{category.category}</span>
                <ChevronRight size={11} className="sidebar-cat-arrow" />
              </button>

              {opened && (
                <div className="sidebar-features">
                  {category.items.map((item) => {
                    const Icon = item.icon;

                    return (
                      <NavLink
                        key={item.path}
                        to={item.path}
                        end={item.path === "/admin/dashboard"}
                        onClick={handleFeatureClick}
                        className={({ isActive }) =>
                          `sidebar-feature ${isActive ? "active" : ""}`
                        }
                      >
                        <span className="sidebar-feature-dot" />
                        {Icon && <Icon size={13} />}
                        <span>{item.label}</span>
                      </NavLink>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="sidebar-footer">
        <button className="sidebar-footer-btn" onClick={logout}>
          <LogOut size={14} />
          <span>Sign out</span>
        </button>
      </div>
    </aside>
  );
}

export default Sidebar;
