import {
  Bell,
  ChevronDown,
  Menu,
  Building2,
  CalendarRange,
  BookOpen,
  HelpCircle,
} from "lucide-react";

import "./navbar.css";

function Navbar({ onToggleSidebar }) {
  const selectedDomain = { name: "Capital University" };
  const selectedYear = "2025-2026";
  const selectedSemester = "Fall Semester";

  return (
    <header className="navbar">
      {/* Left side */}
      <div className="navbar-left">
        <button className="nav-icon-btn" onClick={onToggleSidebar}>
          <Menu size={16} />
        </button>

        <div className="nav-divider" />

        <div className="nav-dropdown-trigger">
          <Building2 size={15} />
          <div>
            <span className="nav-label">Current Scope</span>
            <strong>{selectedDomain.name}</strong>
          </div>
          <ChevronDown size={13} />
        </div>

        <div className="nav-dropdown-trigger small">
          <CalendarRange size={15} />
          <div>
            <span className="nav-label">Academic Year</span>
            <strong>{selectedYear}</strong>
          </div>
          <ChevronDown size={13} />
        </div>

        <div className="nav-dropdown-trigger small">
          <BookOpen size={15} />
          <div>
            <span className="nav-label">Semester</span>
            <strong>{selectedSemester}</strong>
          </div>
          <ChevronDown size={13} />
        </div>
      </div>

      {/* Right side */}
      <div className="navbar-right">
        <div className="nav-search">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input placeholder="Search anything…" />
        </div>

        <button className="nav-icon-btn">
          <Bell size={14} />
          <span className="badge" />
        </button>

        <button className="nav-icon-btn">
          <HelpCircle size={14} />
        </button>

        <div className="topbar-avatar">A</div>
      </div>
    </header>
  );
}

export default Navbar;