import { useState, useRef, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell, ChevronDown, Menu, Building2, CalendarRange,
  BookOpen, HelpCircle, User, LogOut, Key, Settings, Search, Globe,
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

function Navbar({ onToggleSidebar, showSecondary, onToggleSecondary, onOpenCommandPalette }) {
  const { t, i18n } = useTranslation();
  const { scopeNode } = useDomain();
  const { selectedYear, selectedSemester, selectedYearObj, selectedSemesterObj, academicYears = [], semesters = [], selectYear, selectSemester, clearYear, clearSemester, loading: academicLoading } = useAcademic();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const language = i18n.language;

  const [openDropdown, setOpenDropdown] = useState(null);
  const [showChangePassword, setShowChangePassword] = useState(false);
  const [showScopeModal, setShowScopeModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState([]);
  const [notifLoading, setNotifLoading] = useState(false);

  const scopeRef = useRef(null);
  const yearRef = useRef(null);
  const semRef = useRef(null);
  const bellRef = useRef(null);
  const avatarRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (
        !scopeRef.current?.contains(e.target) &&
        !yearRef.current?.contains(e.target) &&
        !semRef.current?.contains(e.target) &&
        !bellRef.current?.contains(e.target) &&
        !avatarRef.current?.contains(e.target)
      ) {
        setOpenDropdown(null);
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

  const handleSearch = (e) => {
    e.preventDefault();
    const trimmed = searchQuery.trim();
    if (!trimmed) return;
    navigate(`/admin/users?search=${encodeURIComponent(trimmed)}`);
    setSearchQuery("");
  };

  const scopeDisplayName = scopeNode
    ? (scopeNode.localizedName || getLocalized(scopeNode.name, language))
    : t("all_scopes");

  const scopeBadgeText = [
    scopeDisplayName,
    selectedYearObj?.name || null,
    selectedSemesterObj?.name || null,
  ].filter(Boolean).join(" | ") || t("all_scopes");

  return (
    <>
      <header className="navbar">
        <div className="navbar-left">
          <button className="nav-icon-btn" onClick={onToggleSidebar}>
            <Menu size={16} />
          </button>

          <button
            className={`nav-icon-btn ${showSecondary ? "is-active" : ""}`}
            onClick={onToggleSecondary}
            title={t("toggle_directory_search")}
          >
            <Search size={14} />
          </button>

          {!showSecondary && <div className="nav-divider" />}

          <div className="nav-scope-group">
            <div className="nav-dropdown-wrapper" ref={scopeRef}>
              <button
                className={`nav-dropdown-trigger scope-trigger${scopeNode ? " has-active" : ""}`}
                onClick={() => setShowScopeModal(true)}
              >
                {(() => {
                  const ScopeIcon = scopeNode ? (getNodeTypeConfig(scopeNode.type)?.icon || Building2) : Building2;
                  return <ScopeIcon size={15} />;
                })()}
                <div className="scope-content">
                  <span className="nav-label">{t("current_scope")}</span>
                  <strong>{scopeDisplayName}</strong>
                </div>
                {scopeNode && <span className="scope-active-indicator" />}
                <ChevronDown size={13} />
              </button>
            </div>

            <div className="nav-dropdown-wrapper" ref={yearRef}>
              <button
                className={`nav-dropdown-trigger small ${openDropdown === "year" ? "is-open" : ""}${selectedYearObj ? " has-active" : ""}`}
                onClick={() => toggleDropdown("year")}
              >
                <CalendarRange size={15} />
                <div>
                  <span className="nav-label">{t("academic_year")}</span>
                  <strong>{selectedYearObj?.name || t("all_years")}</strong>
                </div>
                <ChevronDown size={13} />
              </button>
              {openDropdown === "year" && (
                <div className="nav-dropdown-menu">
                  <button
                    className={`nav-dropdown-item ${!selectedYearObj ? "is-selected" : ""}`}
                    onClick={() => { clearYear(); setOpenDropdown(null); }}
                  >
                    <Globe size={13} />
                    <span><strong>{t("all_years")}</strong></span>
                    {!selectedYearObj && <span className="nav-dropdown-check">✓</span>}
                  </button>
                  <div className="nav-dropdown-separator" />
                  {academicLoading ? (
                    <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>{t("loading")}…</div>
                  ) : academicYears.length === 0 ? (
                    <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>{t("no_years_found")}</div>
                  ) : academicYears.map((y) => (
                    <button
                      key={y.id}
                      className={`nav-dropdown-item ${selectedYearObj?.id === y.id ? "is-selected" : ""} ${y.isClosed ? "is-closed" : ""}`}
                      onClick={() => { selectYear(y); setOpenDropdown(null); }}
                    >
                      <CalendarRange size={13} />
                      <div className="nav-item-content">
                        <strong>{y.name}</strong>
                        <span className="nav-item-sub">{y.startDate?.split("T")[0]} – {y.endDate?.split("T")[0]}</span>
                      </div>
                      {y.isCurrent && <span className="nav-current-tag">{t("current_badge")}</span>}
                      {selectedYearObj?.id === y.id && <span className="nav-dropdown-check">✓</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="nav-dropdown-wrapper" ref={semRef}>
              <button
                className={`nav-dropdown-trigger small ${openDropdown === "semester" ? "is-open" : ""}${selectedSemesterObj ? " has-active" : ""}`}
                onClick={() => toggleDropdown("semester")}
              >
                <BookOpen size={15} />
                <div>
                  <span className="nav-label">{t("semester")}</span>
                  <strong>{selectedSemesterObj?.name || t("all_semesters")}</strong>
                </div>
                <ChevronDown size={13} />
              </button>
              {openDropdown === "semester" && (
                <div className="nav-dropdown-menu">
                  <button
                    className={`nav-dropdown-item ${!selectedSemesterObj ? "is-selected" : ""}`}
                    onClick={() => { clearSemester(); setOpenDropdown(null); }}
                  >
                    <Globe size={13} />
                    <span><strong>{t("all_semesters")}</strong></span>
                    {!selectedSemesterObj && <span className="nav-dropdown-check">✓</span>}
                  </button>
                  <div className="nav-dropdown-separator" />
                  {academicLoading ? (
                    <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>{t("loading")}…</div>
                  ) : semesters.length === 0 ? (
                    <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>{t("no_semesters")}</div>
                  ) : semesters.map((s) => (
                    <button
                      key={s.id}
                      className={`nav-dropdown-item ${selectedSemesterObj?.id === s.id ? "is-selected" : ""} ${s.isClosed ? "is-closed" : ""}`}
                      onClick={() => { selectSemester(s); setOpenDropdown(null); }}
                    >
                      <BookOpen size={13} />
                      <div className="nav-item-content">
                        <strong>{s.name}</strong>
                        <span className="nav-item-sub">{s.startDate?.split("T")[0]} – {s.endDate?.split("T")[0]}</span>
                      </div>
                      {s.isCurrent && <span className="nav-current-tag">{t("current_badge")}</span>}
                      {selectedSemesterObj?.id === s.id && <span className="nav-dropdown-check">✓</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="navbar-right">
          <div
            className={`nav-scope-badge${!scopeNode ? " is-global" : ""}`}
            title={`${t("viewing")}: ${scopeBadgeText}`}
            onClick={() => setShowScopeModal(true)}
          >
            <span className={`nav-scope-badge-dot${selectedYearObj || selectedSemesterObj ? " is-filtered" : ""}`} />
            <span className="nav-scope-badge-text">{scopeBadgeText}</span>
          </div>

          <button
            className="nav-search nav-cmdk-trigger"
            onClick={onOpenCommandPalette}
            title={`${t("search")} (Ctrl+K)`}
          >
            <Search size={13} />
            <span className="nav-cmdk-label">{t("search")}…</span>
            <kbd className="nav-cmdk-kbd">⌘K</kbd>
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

          <button className="nav-icon-btn">
            <HelpCircle size={14} />
          </button>

          <div className="language-selector">
            <button
              className="lang-toggle"
              onClick={() => changeLanguage(language === "ar" ? "en" : "ar")}
              title={language === "ar" ? "Switch to English" : "التبديل إلى العربية"}
            >
              <Globe size={14} />
              <span className="lang-code">{language === "ar" ? "AR" : "EN"}</span>
              <span className="lang-label">{language === "ar" ? "العربية" : "English"}</span>
              <ChevronDown size={10} className="lang-chevron" />
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
