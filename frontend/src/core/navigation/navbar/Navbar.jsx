import PropTypes from "prop-types";
import { useState, useRef, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell,
  ChevronDown,
  Menu,
  Building2,
  CalendarRange,
  BookOpen,
  HelpCircle,
  Lock,
  LogOut,
} from "lucide-react";

import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useAuth } from "../../auth/useAuth";
import * as notificationService from "../../services/notificationService";
import ChangePasswordModal from "../../components/ChangePasswordModal";
import "../../styles/navbar.css";

function Navbar({ onToggleSidebar, showSecondary }) {
  const { selectedDomain, selectDomain, domains, domainsLoading } = useDomain();
  const { selectedYear, selectedSemester, academicYears, semesters, selectYear, selectSemester } = useAcademic();
  const { logout } = useAuth();
  const navigate = useNavigate();

  const [openDropdown, setOpenDropdown] = useState(null);
  const [unreadCount, setUnreadCount] = useState(0);
  const [showChangePassword, setShowChangePassword] = useState(false);
  const scopeRef = useRef(null);
  const yearRef = useRef(null);
  const semRef = useRef(null);
  const userMenuRef = useRef(null);

  const fetchUnreadCount = useCallback(async () => {
    try {
      const notifications = await notificationService.fetchUnreadNotifications();
      setUnreadCount(notifications?.length || 0);
    } catch { }
  }, []);

  useEffect(() => {
    fetchUnreadCount();
    const interval = setInterval(fetchUnreadCount, 30000);
    return () => clearInterval(interval);
  }, [fetchUnreadCount]);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (
        !scopeRef.current?.contains(e.target) &&
        !yearRef.current?.contains(e.target) &&
        !semRef.current?.contains(e.target) &&
        !userMenuRef.current?.contains(e.target)
      ) {
        setOpenDropdown(null);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const toggleDropdown = (name) => {
    setOpenDropdown((prev) => (prev === name ? null : name));
  };

  const scopeItems = domains.length > 0
    ? domains
    : [{ id: "root-001", name: "Capital University", type: "University" }];

  return (
    <header className="navbar">
      <div className="navbar-left">
        <button className="nav-icon-btn" onClick={onToggleSidebar}>
          <Menu size={16} />
        </button>

        {!showSecondary && <div className="nav-divider" />}

        <div className="nav-dropdown-wrapper" ref={scopeRef}>
          <button
            className={`nav-dropdown-trigger scope-trigger ${
              openDropdown === "scope" ? "is-open" : ""
            }`}
            onClick={() => toggleDropdown("scope")}
          >
            <Building2 size={15} />
            <div className="scope-content">
              <span className="nav-label">Current Scope</span>
              <strong>{selectedDomain?.name || "All"}</strong>
            </div>
            <ChevronDown size={13} />
          </button>

          {openDropdown === "scope" && (
            <div className="nav-dropdown-menu">
              {domainsLoading && (
                <div className="nav-dropdown-item" style={{ justifyContent: "center" }}>
                  Loading...
                </div>
              )}
              {!domainsLoading && scopeItems.map((f) => (
                <button
                  key={f.id}
                  className={`nav-dropdown-item ${
                    selectedDomain?.id === f.id ? "is-selected" : ""
                  }`}
                  onClick={() => {
                    selectDomain(f);
                    setOpenDropdown(null);
                  }}
                >
                  <Building2 size={13} />
                  <div>
                    <strong>{f.name}</strong>
                    <span>{f.type === "University" || f.type === "Faculty" ? f.type : ""}</span>
                  </div>
                  {selectedDomain?.id === f.id && (
                    <span className="nav-dropdown-check">✓</span>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="nav-dropdown-wrapper" ref={yearRef}>
          <button
            className={`nav-dropdown-trigger small ${
              openDropdown === "year" ? "is-open" : ""
            }`}
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
              {academicYears.map((y) => (
                <button
                  key={y.id}
                  className={`nav-dropdown-item ${
                    selectedYear === y.name ? "is-selected" : ""
                  }`}
                  onClick={() => {
                    selectYear(y.name);
                    setOpenDropdown(null);
                  }}
                >
                  <CalendarRange size={13} />
                  <span>{y.name}</span>
                  {selectedYear === y.name && (
                    <span className="nav-dropdown-check">✓</span>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="nav-dropdown-wrapper" ref={semRef}>
          <button
            className={`nav-dropdown-trigger small ${
              openDropdown === "semester" ? "is-open" : ""
            }`}
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
              {semesters.map((s) => (
                <button
                  key={s.id}
                  className={`nav-dropdown-item ${
                    selectedSemester === s.name ? "is-selected" : ""
                  }`}
                  onClick={() => {
                    selectSemester(s.name);
                    setOpenDropdown(null);
                  }}
                >
                  <BookOpen size={13} />
                  <span>{s.name}</span>
                  {selectedSemester === s.name && (
                    <span className="nav-dropdown-check">✓</span>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="navbar-right">
        <div className="nav-search">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input placeholder="Search anything…" aria-label="Search" />
        </div>

        <button className="nav-icon-btn" onClick={() => navigate("/admin/notifications")} title="Notifications">
          <Bell size={14} />
          {unreadCount > 0 && <span className="badge">{unreadCount > 99 ? "99+" : unreadCount}</span>}
        </button>

        <button className="nav-icon-btn">
          <HelpCircle size={14} />
        </button>

        <div className="nav-dropdown-wrapper" ref={userMenuRef}>
          <button
            className="topbar-avatar"
            onClick={() => toggleDropdown("user")}
          >
            A
          </button>
          {openDropdown === "user" && (
            <div className="nav-dropdown-menu" style={{ right: 0, left: "auto" }}>
              <button
                className="nav-dropdown-item"
                onClick={() => { setOpenDropdown(null); setShowChangePassword(true); }}
              >
                <Lock size={13} />
                <span>Change Password</span>
              </button>
              <button
                className="nav-dropdown-item"
                onClick={() => { setOpenDropdown(null); logout(); }}
              >
                <LogOut size={13} />
                <span>Sign Out</span>
              </button>
            </div>
          )}
        </div>
      </div>

      {showChangePassword && (
        <ChangePasswordModal
          onClose={() => setShowChangePassword(false)}
          onSuccess={() => fetchUnreadCount()}
        />
      )}
    </header>
  );
}

export default Navbar;

Navbar.propTypes = {
  onToggleSidebar: PropTypes.func.isRequired,
  showSecondary: PropTypes.bool,
};
