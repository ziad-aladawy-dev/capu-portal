import { useState, useRef, useEffect } from "react";
import {
  Bell,
  ChevronDown,
  Menu,
  Building2,
  CalendarRange,
  BookOpen,
  HelpCircle,
} from "lucide-react";

import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import "../../styles/navbar.css";

function Navbar({ onToggleSidebar, showSecondary }) {
  const { selectedDomain, selectDomain, domains, domainsLoading } = useDomain();
  const { selectedYear, selectedSemester, academicYears, semesters, selectYear, selectSemester } = useAcademic();

  const [openDropdown, setOpenDropdown] = useState(null);
  const scopeRef = useRef(null);
  const yearRef = useRef(null);
  const semRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (
        !scopeRef.current?.contains(e.target) &&
        !yearRef.current?.contains(e.target) &&
        !semRef.current?.contains(e.target)
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
                    <span>{f.type === "University" ? "University" : f.type === "Faculty" ? "Faculty" : ""}</span>
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
