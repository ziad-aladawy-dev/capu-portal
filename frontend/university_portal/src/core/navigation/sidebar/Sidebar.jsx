import { useState } from "react";
import {
  ChevronRight,
  LogOut,
  Building2,
  Plug,
} from "lucide-react";

import "./sidebar.css";

const categoryIcons = {
  administration: <Building2 size={14} />,
  integration: <Plug size={14} />,
};

function Sidebar({ isOpen, isMobile, onClose }) {
  const [openedCategory, setOpenedCategory] = useState("administration");

  const categories = [
    {
      key: "administration",
      title: "Administration",
      items: ["University Structure", "Users Management", "Permissions"],
    },
    {
      key: "integration",
      title: "Integration",
      items: ["SIS Sync", "Data Sync"],
    },
  ];

  // On mobile, clicking a nav item closes the sidebar
  const handleFeatureClick = () => {
    if (isMobile && onClose) onClose();
  };

  return (
   <aside
  className={`sidebar ${isOpen ? "is-open" : "is-closed"}`}
>
      {/* Decorative SVG geometry */}
      <svg className="sidebar-geo" viewBox="0 0 230 620" preserveAspectRatio="none">
        <circle cx="230" cy="0" r="140" fill="rgba(224,192,106,0.04)" />
        <circle cx="0" cy="460" r="110" fill="rgba(35,42,116,0.35)" />
        <line x1="115" y1="80" x2="230" y2="200" stroke="rgba(224,192,106,0.06)" strokeWidth="1" />
        <line x1="0" y1="300" x2="115" y2="180" stroke="rgba(224,192,106,0.04)" strokeWidth="1" />
      </svg>

      {/* Logo */}
      <div className="sidebar-logo">
        <div className="sidebar-logo-mark">
          <svg viewBox="0 0 20 20" fill="none">
            <path d="M10 2L18 7V13L10 18L2 13V7L10 2Z" fill="#07091e" stroke="#07091e" strokeWidth="0.5" />
            <path d="M10 5L15 8V12L10 15L5 12V8L10 5Z" fill="#e0c06a" opacity="0.6" />
            <circle cx="10" cy="10" r="2" fill="#07091e" />
          </svg>
        </div>
        <div className="sidebar-logo-text">
          UniAdmin
          <small>Control Panel</small>
        </div>
      </div>

      {/* User Card */}
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

      {/* Nav */}
      <div className="sidebar-content">
        <div className="sidebar-section-label">Menu</div>

        {categories.map((category) => {
          const opened = openedCategory === category.key;

          return (
            <div className="sidebar-category" key={category.key}>
              <button
                className={`sidebar-category-header ${opened ? "is-open" : ""}`}
                onClick={() => setOpenedCategory(opened ? null : category.key)}
              >
                <div className="sidebar-cat-icon">
                  {categoryIcons[category.key]}
                </div>
                <span className="sidebar-category-title">{category.title}</span>
                <ChevronRight size={11} className="sidebar-cat-arrow" />
              </button>

              {opened && (
                <div className="sidebar-features">
                  {category.items.map((item) => (
                    <button
                      key={item}
                      className={`sidebar-feature ${
                        item === "University Structure" ? "active" : ""
                      }`}
                      onClick={handleFeatureClick}
                    >
                      <span className="sidebar-feature-dot" />
                      {item}
                    </button>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Footer */}
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