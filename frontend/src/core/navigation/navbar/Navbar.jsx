import { useState, useRef, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell, ChevronDown, Menu, Building2, CalendarRange,
  BookOpen, HelpCircle, User, LogOut, Key, Settings, Search,
} from "lucide-react";

import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useAuth } from "../../auth/useAuth";
import * as notificationService from "../../services/notificationService";
import ChangePasswordModal from "../../auth/components/ChangePasswordModal";
import ScopeModal from "../../components/ScopeModal";
import "../../styles/navbar.css";

function Navbar({ onToggleSidebar, showSecondary, onToggleSecondary, onOpenCommandPalette }) {
  const { scopeNode } = useDomain();
  const { selectedYear, selectedSemester, academicYears = [], semesters = [], selectYear, selectSemester, loading: academicLoading } = useAcademic();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [openDropdown, setOpenDropdown] = useState(null);
  const [showChangePassword, setShowChangePassword] = useState(false);
  const [showScopeModal, setShowScopeModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  // Notification state
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

  // Fetch unread count on mount
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
    if (notifications.length > 0) return; // already loaded
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

  const userInitial = user?.name?.charAt(0)?.toUpperCase() || user?.fullName?.charAt(0)?.toUpperCase() || "U";
  const userName = user?.name || user?.fullName || "User";

  const formatTime = (iso) => {
    if (!iso) return "";
    const diff = (Date.now() - new Date(iso).getTime()) / 1000;
    if (diff < 60) return "just now";
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
  };

  const handleSearch = (e) => {
    e.preventDefault();
    const trimmed = searchQuery.trim();
    if (!trimmed) return;
    navigate(`/admin/users?search=${encodeURIComponent(trimmed)}`);
    setSearchQuery("");
  };

  const scopeBadgeText = [
    scopeNode?.name,
    selectedYear !== "—" ? selectedYear : null,
    selectedSemester !== "—" ? selectedSemester : null,
  ].filter(Boolean).join(" | ") || "All Scopes";

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
            title="Toggle directory search"
          >
            <Search size={14} />
          </button>

          {!showSecondary && <div className="nav-divider" />}

          <div className="nav-dropdown-wrapper" ref={scopeRef}>
            <button
              className="nav-dropdown-trigger scope-trigger"
              onClick={() => setShowScopeModal(true)}
            >
              <Building2 size={15} />
              <div className="scope-content">
                <span className="nav-label">Current Scope</span>
                <strong>{scopeNode?.name || "All"}</strong>
                {scopeNode && <small>{scopeNode.type}</small>}
              </div>
              <ChevronDown size={13} />
            </button>
          </div>

          <div className="nav-dropdown-wrapper" ref={yearRef}>
            <button
              className={`nav-dropdown-trigger small ${openDropdown === "year" ? "is-open" : ""}`}
              onClick={() => toggleDropdown("year")}
            >
              <CalendarRange size={15} />
              <div>
                <span className="nav-label">Academic Year</span>
                <strong>{selectedYear}</strong>
              </div>
              <ChevronDown size={13} />
            </button>
            {openDropdown === "year" && (
              <div className="nav-dropdown-menu">
                {academicLoading ? (
                  <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>Loading years…</div>
                ) : academicYears.length === 0 ? (
                  <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>No years found</div>
                ) : academicYears.map((y) => (
                  <button
                    key={y.id}
                    className={`nav-dropdown-item ${selectedYear === y.name ? "is-selected" : ""}`}
                    onClick={() => { selectYear(y.name); setOpenDropdown(null); }}
                  >
                    <CalendarRange size={13} />
                    <span>{y.name}</span>
                    {selectedYear === y.name && <span className="nav-dropdown-check">✓</span>}
                  </button>
                ))}
              </div>
            )}
          </div>

          <div className="nav-dropdown-wrapper" ref={semRef}>
            <button
              className={`nav-dropdown-trigger small ${openDropdown === "semester" ? "is-open" : ""}`}
              onClick={() => toggleDropdown("semester")}
            >
              <BookOpen size={15} />
              <div>
                <span className="nav-label">Semester</span>
                <strong>{selectedSemester}</strong>
              </div>
              <ChevronDown size={13} />
            </button>
            {openDropdown === "semester" && (
              <div className="nav-dropdown-menu">
                {academicLoading ? (
                  <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>Loading semesters…</div>
                ) : semesters.length === 0 ? (
                  <div className="nav-dropdown-item" style={{ justifyContent: "center", opacity: 0.5 }}>No semesters</div>
                ) : semesters.map((s) => (
                  <button
                    key={s.id}
                    className={`nav-dropdown-item ${selectedSemester === s.name ? "is-selected" : ""}`}
                    onClick={() => { selectSemester(s.name); setOpenDropdown(null); }}
                  >
                    <BookOpen size={13} />
                    <span>{s.name}</span>
                    {selectedSemester === s.name && <span className="nav-dropdown-check">✓</span>}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="navbar-right">
          {/* Scope badge summary */}
          <div className="nav-scope-badge" title={`Viewing: ${scopeBadgeText}`}>
            <span className="nav-scope-badge-dot" />
            <span className="nav-scope-badge-text">{scopeBadgeText}</span>
          </div>

          {/* Cmd+K trigger */}
          <button
            className="nav-search nav-cmdk-trigger"
            onClick={onOpenCommandPalette}
            title="Search (Ctrl+K)"
          >
            <Search size={13} />
            <span className="nav-cmdk-label">Search…</span>
            <kbd className="nav-cmdk-kbd">⌘K</kbd>
          </button>

          {/* Notification Bell */}
          <div className="nav-dropdown-wrapper" ref={bellRef}>
            <button className="nav-icon-btn" onClick={() => toggleDropdown("bell")}>
              <Bell size={14} />
              {unreadCount > 0 && <span className="badge">{unreadCount > 9 ? "9+" : unreadCount}</span>}
            </button>
            {openDropdown === "bell" && (
              <div className="nav-dropdown-menu notification-dropdown">
                <div className="notif-dropdown-header">
                  <strong>Notifications</strong>
                  <button className="notif-view-all" onClick={() => { setOpenDropdown(null); navigate("/admin/notifications"); }}>
                    View all
                  </button>
                </div>
                {notifLoading ? (
                  <div className="notif-dropdown-empty">Loading…</div>
                ) : notifications.length === 0 ? (
                  <div className="notif-dropdown-empty">No unread notifications</div>
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

          {/* User Avatar + Profile Dropdown */}
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
                  <span>Change Password</span>
                </button>
                <button className="nav-dropdown-item" onClick={() => { setOpenDropdown(null); navigate("/admin/notifications"); }}>
                  <Bell size={13} />
                  <span>Notifications</span>
                </button>
                <div className="profile-dropdown-divider" />
                <button className="nav-dropdown-item logout-item" onClick={() => { setOpenDropdown(null); logout(); }}>
                  <LogOut size={13} />
                  <span>Sign Out</span>
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
