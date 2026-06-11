import { useState, useRef, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell, ChevronDown, Menu, Building2,
  LogOut, Key, Search, Globe, Calendar,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { getLocalized } from "../../utils/getLocalized";
import { getNodeTypeConfig } from "../../../modules/university/utils/nodeTypeRegistry";

import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useAuth } from "../../auth/useAuth";
import * as notificationService from "../../services/notificationService";
import ChangePasswordModal from "../../auth/components/ChangePasswordModal";
import ScopeModal from "../../components/ScopeModal";
import "../../styles/navbar.css";

function getUserInitial(user, language) {
  if (!user) return "U";
  const displayName = getLocalized(user.name || user.fullName, language);
  return displayName?.charAt(0)?.toUpperCase() || "U";
}

function Navbar({ onToggleSidebar, onToggleSecondary, onOpenCommandPalette, secondaryOpen, showDirectoryToggle = true, style }) {
  const { t, i18n } = useTranslation();
  const { scopeNode } = useDomain();
  const {
    selectedYearObj, selectedSemesterObj,
    academicYears, semesters, selectYear, selectSemester,
  } = useAcademic();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const language = i18n.language;

  const [openDropdown, setOpenDropdown] = useState(null);
  const [showChangePassword, setShowChangePassword] = useState(false);
  const [showScopeModal, setShowScopeModal] = useState(false);
  const [contextOpen, setContextOpen] = useState(false);

  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState([]);
  const [notifLoading, setNotifLoading] = useState(false);

  const bellRef = useRef(null);
  const avatarRef = useRef(null);
  const contextRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (
        !bellRef.current?.contains(e.target) &&
        !avatarRef.current?.contains(e.target) &&
        !contextRef.current?.contains(e.target)
      ) {
        setOpenDropdown(null);
        setContextOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    notificationService
      .fetchUnreadNotifications()
      .then((data) => {
        const list = Array.isArray(data) ? data : [];
        setUnreadCount(list.length);
      })
      .catch(() => setUnreadCount(0));
  }, []);

  const loadNotifications = useCallback(async () => {
    if (notifications.length > 0) return;
    setNotifLoading(true);
    try {
      const data = await notificationService.fetchUnreadNotifications();
      setNotifications(Array.isArray(data) ? data.slice(0, 5) : []);
    } catch {
      setNotifications([]);
    } finally {
      setNotifLoading(false);
    }
  }, [notifications.length]);

  const handleMarkRead = async (id) => {
    try {
      await notificationService.markNotificationRead(id);
      setNotifications((prev) => prev.filter((n) => n.id !== id));
      setUnreadCount((prev) => Math.max(0, prev - 1));
    } catch { /* ignore */ }
  };

  const toggleDropdown = (name) => {
    setOpenDropdown((prev) => (prev === name ? null : name));
    if (name === "bell") loadNotifications();
  };

  const userInitial = getUserInitial(user, language);
  const userName = user?.name || user?.fullName || "User";

  const changeLanguage = (lng) => {
    i18n.changeLanguage(lng);
  };

  const formatTime = (iso) => {
    if (!iso) return "";
    const diff = (Date.now() - new Date(iso).getTime()) / 1000;
    if (diff < 60) return t("just_now");
    if (diff < 3600) return `${Math.floor(diff / 60)}${t("m_ago")}`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}${t("h_ago")}`;
    return `${Math.floor(diff / 86400)}${t("d_ago")}`;
  };

  const scopeDisplayName = scopeNode
    ? getLocalized(scopeNode.localizedName || scopeNode.name, language)
    : t("all_scopes");

  const yearLabel = selectedYearObj ? getLocalized(selectedYearObj.name, language) : null;
  const semesterLabel = selectedSemesterObj ? getLocalized(selectedSemesterObj.name, language) : null;

  return (
    <>
      <header className="navbar" style={style} data-secondary-open={secondaryOpen || undefined}>
        <div className="navbar-left">
          <button className="nav-icon-btn" onClick={onToggleSidebar}>
            <Menu size={16} />
          </button>
          {showDirectoryToggle && (
            <button
              className={`nav-icon-btn${secondaryOpen ? " is-active" : ""}`}
              onClick={onToggleSecondary}
              title={t("toggle_directory_search")}
            >
              <Search size={14} />
            </button>
          )}
        </div>

        <div className="navbar-center" ref={contextRef}>
          <button
            className={`nav-scope-pill${scopeNode || selectedYearObj || selectedSemesterObj ? " has-scope" : ""}`}
            onClick={() => setShowScopeModal(true)}
            title={t("select_scope")}
          >
            <div className="nav-scope-pill-content">
              {scopeNode ? (
                (() => {
                  const ScopeIcon = getNodeTypeConfig(scopeNode.type)?.icon || Building2;
                  return <ScopeIcon size={15} className="nav-scope-icon" />;
                })()
              ) : (
                <Globe size={15} className="nav-scope-icon" />
              )}
              <span className="nav-scope-pill-main">{scopeDisplayName}</span>
            </div>
            <ChevronDown size={14} className="nav-scope-pill-chevron" />
          </button>

          <div className="nav-context-divider" />

          <div className="nav-context-selectors">
            <div
              className={`nav-context-dropdown ${contextOpen ? "is-open" : ""}`}
              onClick={() => setContextOpen((p) => !p)}
              role="combobox"
              aria-expanded={contextOpen}
              aria-haspopup="listbox"
              aria-label="Select academic year and semester"
              tabIndex={0}
              onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setContextOpen((p) => !p); } }}
            >
              <Calendar size={13} className="nav-context-icon" />
              <span className="nav-context-label">
                {yearLabel || t("select_academic_year")}
                {semesterLabel && <><span className="nav-context-sep"> / </span>{semesterLabel}</>}
              </span>
              <ChevronDown size={11} className="nav-context-chevron" />
            </div>

            {contextOpen && (
              <div className="nav-context-menu" role="listbox" aria-label="Context selector">
                <div className="nav-context-section">
                  <div className="nav-context-section-title">{t("academic_year")}</div>
                  {academicYears.length === 0 ? (
                    <div className="nav-context-empty">{t("no_years_available")}</div>
                  ) : (
                    academicYears.map((y) => (
                      <button
                        key={y.id}
                        className={`nav-context-item ${selectedYearObj?.id === y.id ? "is-selected" : ""}`}
                        onClick={() => { selectYear(y); setContextOpen(false); }}
                        role="option"
                        aria-selected={selectedYearObj?.id === y.id}
                      >
                        {y.name}
                        {y.isCurrent && <span className="nav-context-check">●</span>}
                      </button>
                    ))
                  )}
                </div>
                {semesters.length > 0 && (
                  <div className="nav-context-section">
                    <div className="nav-context-section-title">{t("semester")}</div>
                    {semesters.map((s) => (
                      <button
                        key={s.id}
                        className={`nav-context-item ${selectedSemesterObj?.id === s.id ? "is-selected" : ""}`}
                        onClick={() => { selectSemester(s); setContextOpen(false); }}
                        role="option"
                        aria-selected={selectedSemesterObj?.id === s.id}
                      >
                        {s.name}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        <div className="navbar-right">
          <button
            className="nav-icon-btn"
            onClick={onOpenCommandPalette}
            title={`${t("search")} (Ctrl+K)`}
          >
            <Search size={14} />
          </button>

          <div className="nav-dropdown-wrapper" ref={bellRef}>
            <button className="nav-icon-btn" onClick={() => toggleDropdown("bell")}>
              <Bell size={14} />
              {unreadCount > 0 && <span className="badge">{unreadCount > 9 ? "9+" : unreadCount}</span>}
            </button>
            {openDropdown === "bell" && (
              <div className="nav-dropdown-menu notification-dropdown">
                <div className="notif-dropdown-header">
                  <strong>{t("notifications")}</strong>
                  <button className="notif-view-all" onClick={() => { setOpenDropdown(null); navigate("/admin/notifications"); }}>
                    {t("view_all")}
                  </button>
                </div>
                {notifLoading ? (
                  <div className="notif-dropdown-empty">{t("loading")}…</div>
                ) : notifications.length === 0 ? (
                  <div className="notif-dropdown-empty">{t("no_unread_notifications")}</div>
                ) : (
                  notifications.map((n) => (
                    <div key={n.id} className="notif-dropdown-item" onClick={() => handleMarkRead(n.id)}>
                      <div className="notif-dropdown-dot" />
                      <div className="notif-dropdown-body">
                        <span className="notif-dropdown-title">{n.title}</span>
                        <span className="notif-dropdown-time">{formatTime(n.createdAt)}</span>
                      </div>
                    </div>
                  ))
                )}
              </div>
            )}
          </div>

          <div className="language-selector">
            <button
              className="lang-toggle"
              onClick={() => changeLanguage(language === "ar" ? "en" : "ar")}
              title={language === "ar" ? "Switch to English" : "التبديل إلى العربية"}
            >
              <Globe size={14} />
              <span className="lang-code">{language === "ar" ? "AR" : "EN"}</span>
            </button>
          </div>

          <div className="nav-dropdown-wrapper" ref={avatarRef}>
            <div className="topbar-avatar" onClick={() => toggleDropdown("profile")} style={{ cursor: "pointer" }}>
              {userInitial}
            </div>
            {openDropdown === "profile" && (
              <div className="nav-dropdown-menu profile-dropdown">
                <div className="profile-dropdown-header">
                  <div className="profile-dropdown-avatar">{userInitial}</div>
                  <div>
                    <strong>{userName}</strong>
                    <span style={{ fontSize: 11, opacity: 0.6, display: "block" }}>{user?.email || ""}</span>
                  </div>
                </div>
                <div className="profile-dropdown-divider" />
                <button className="nav-dropdown-item" onClick={() => { setOpenDropdown(null); setShowChangePassword(true); }}>
                  <Key size={13} />
                  <span>{t("change_password")}</span>
                </button>
                <button className="nav-dropdown-item" onClick={() => { setOpenDropdown(null); navigate("/admin/notifications"); }}>
                  <Bell size={13} />
                  <span>{t("notifications")}</span>
                </button>
                <div className="profile-dropdown-divider" />
                <button className="nav-dropdown-item logout-item" onClick={() => { setOpenDropdown(null); logout(); }}>
                  <LogOut size={13} />
                  <span>{t("logout")}</span>
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {showScopeModal && (
        <ScopeModal onClose={() => setShowScopeModal(false)} />
      )}
      {showChangePassword && (
        <ChangePasswordModal onClose={() => setShowChangePassword(false)} />
      )}
    </>
  );
}

export default Navbar;
