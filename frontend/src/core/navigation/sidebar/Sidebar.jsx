import { useState, useMemo } from "react";
import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ChevronRight, LogOut } from "lucide-react";

import { buildMenu, getCategoryIcon } from "../menuAggregator";
import { usePermission } from "../../auth/usePermission";
import { useAuth } from "../../auth/useAuth";
import "../../styles/sidebar.css";

const labelToKey = (label) => label.toLowerCase().replace(/\s+/g, "_");

function getUserDisplayName(user, language) {
  if (!user) return "";
  const raw = user.name || user.fullName || "";
  if (typeof raw === "object" && raw !== null) {
    return language === "ar"
      ? (raw.ar || raw.en || "")
      : (raw.en || raw.ar || "");
  }
  if (typeof raw === "string") {
    try {
      const parsed = JSON.parse(raw);
      return language === "ar"
        ? (parsed.ar || parsed.en || raw)
        : (parsed.en || parsed.ar || raw);
    } catch {
      return raw;
    }
  }
  return String(raw);
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
  const isRtl = language === "ar";

  const menu = buildMenu(can);

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
      dir={isRtl ? "rtl" : "ltr"}
    >
      <svg className="sidebar-geo" viewBox="0 0 230 620" preserveAspectRatio="none">
        <circle cx="230" cy="0" r="140" fill="rgba(224,192,106,0.04)" />
        <circle cx="0" cy="460" r="110" fill="rgba(35,42,116,0.35)" />
        <line x1="115" y1="80" x2="230" y2="200" stroke="rgba(224,192,106,0.06)" strokeWidth="1" />
        <line x1="0" y1="300" x2="115" y2="180" stroke="rgba(224,192,106,0.04)" strokeWidth="1" />
      </svg>

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
                className={`sidebar-category-header ${opened ? "is-open" : ""}`}
                onClick={() =>
                  setOpenedCategory(opened ? null : catKey)
                }
              >
                <div className="sidebar-cat-icon">
                  <CatIcon size={14} />
                </div>
                <span className="sidebar-category-title">
                  {t(catKey)}
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
                        <span>{t(itemKey)}</span>
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
