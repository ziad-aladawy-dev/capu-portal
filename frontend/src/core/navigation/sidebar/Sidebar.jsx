import { useState, useMemo } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ChevronRight, LogOut } from "lucide-react";

import { getLocalized } from "../../utils/getLocalized";
import { buildMenu, getCategoryIcon } from "../menuAggregator";
import { usePermission } from "../../auth/usePermission";
import { useAuth } from "../../auth/useAuth";
import "../../styles/sidebar.css";

const labelToKey = (label) => label.toLowerCase().replace(/\s+/g, "_");

function getUserDisplayName(user, language) {
  if (!user) return "";
  return getLocalized(user.name || user.fullName, language);
}

function getUserAvatar(user, language) {
  return getUserDisplayName(user, language).charAt(0).toUpperCase() || "U";
}

function getUserRole(user, language) {
  if (!user) return "";
  const role = user.role || "Staff";
  const roleMap = {
    "Super Admin": language === "ar" ? "مدير عام" : "Super Admin",
    Staff: language === "ar" ? "موظف" : "Staff",
    Student: language === "ar" ? "طالب" : "Student",
  };
  return roleMap[role] || role;
}

function Sidebar({ isOpen, isMobile, onClose }) {
  const { t, i18n } = useTranslation();
  const [openedCategory, setOpenedCategory] = useState("overview");
  const { can } = usePermission();
  const { user, logout } = useAuth();
  const language = i18n.language;
  const location = useLocation();

  // `can` changes identity whenever the permission map recomputes (login,
  // scope change, permission refresh) — exactly the moments the menu should
  // rebuild. Between those, memoization skips the registry walk.
  const menu = useMemo(() => buildMenu(can), [can]);

  const activeCategoryKey = useMemo(() => {
    for (const cat of menu) {
      const catKey = labelToKey(cat.category);
      for (const item of cat.items) {
        if (item.path === location.pathname) return catKey;
        const hasChildInCategory = cat.items.some(
          (other) => other.path !== item.path && other.path.startsWith(item.path + "/")
        );
        if (hasChildInCategory && location.pathname.startsWith(item.path + "/")) return catKey;
      }
    }
    return null;
  }, [menu, location.pathname]);

  const displayName = useMemo(
    () => getUserDisplayName(user, language) || t("guest"),
    [user, language, t]
  );
  const avatar = useMemo(() => getUserAvatar(user, language), [user, language]);
  const roleName = useMemo(() => getUserRole(user, language), [user, language]);

  const handleLogout = async () => {
    if (logout) await logout();
    window.location.href = "/admin/login";
  };

  const handleFeatureClick = () => {
    if (isMobile && onClose) onClose();
  };

  return (
    <aside
      className={`sidebar ${isOpen ? "is-open" : "is-closed"}`}
    >

      <div className="sidebar-brand">
        <div className="sidebar-logo-mark">
          <img src="/images/UniLogo2.png" alt="Capital University" className="sidebar-logo-img" />
        </div>
        <div className="sidebar-university-name">{t("app_name")}</div>
        <div className="sidebar-university-sub">{t("control_panel")}</div>
      </div>

      {user && (
        <div className="sidebar-user-section">
          <div className="sidebar-user-card">
            <div className="sidebar-avatar">{avatar}</div>
            <div className="sidebar-user-info">
              <strong>{displayName}</strong>
              <span>{roleName}</span>
            </div>
            <div className="sidebar-user-badge" />
          </div>
        </div>
      )}

      <div className="sidebar-content">
        <div className="sidebar-section-label">{t("menu")}</div>

        {menu.map((category) => {
          const catKey = labelToKey(category.category);
          const opened = openedCategory === catKey;
          const CatIcon = getCategoryIcon(category.category);

          return (
            <div className="sidebar-category" key={category.category}>
              <button
                type="button"
                className={`sidebar-category-header ${opened ? "is-open" : ""}${activeCategoryKey === catKey ? " has-active-feature" : ""}`}
                onClick={() =>
                  setOpenedCategory(opened ? null : catKey)
                }
              >
                <div className="sidebar-cat-icon">
                  <CatIcon size={14} />
                </div>
                <span className="sidebar-category-title">
                  {t(catKey) === catKey ? category.category : t(catKey)}
                </span>
                <ChevronRight size={11} className="sidebar-cat-arrow" />
              </button>

              {opened && (
                <div className="sidebar-features">
                  {category.items.map((item) => {
                    const ItemIcon = item.icon;
                    const itemKey = labelToKey(item.label);

                    return (
                      <NavLink
                        key={item.path}
                        to={item.path}
                        end={
                          item.path === "/admin/dashboard" ||
                          item.path === "/admin/users"
                        }
                        onClick={handleFeatureClick}
                        className={({ isActive }) =>
                          `sidebar-feature ${isActive ? "active" : ""}`
                        }
                      >
                        <span className="sidebar-feature-dot" />
                        {ItemIcon && <ItemIcon size={13} />}
                        <span>{t(itemKey) === itemKey ? item.label : t(itemKey)}</span>
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
        <button className="sidebar-footer-btn" onClick={handleLogout}>
          <LogOut size={14} />
          <span>{t("logout")}</span>
        </button>
      </div>
    </aside>
  );
}

export default Sidebar;
