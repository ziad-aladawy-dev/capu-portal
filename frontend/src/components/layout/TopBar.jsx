import React, { useState } from "react";
import { Menu, Bell, ChevronDown, Settings } from "lucide-react";
import { useScope } from "../../hooks/use-scope";
import { ScopeEditor } from "../ScopeEditor";
import "./TopBar.css";

export const TopBar = ({ onMenuToggle }) => {
  const { 
    activeScope, 
    getCurrentFacultyName, 
    getCurrentSemesterDisplay,
    getFacultyOptions,
    getAcademicYearOptions,
    getSemesterOptions,
    updateStructuralScope,
    updateTemporalScope
  } = useScope();
  const [showFacultyDropdown, setShowFacultyDropdown] = useState(false);
  const [showSemesterDropdown, setShowSemesterDropdown] = useState(false);
  const [scopeEditorOpen, setScopeEditorOpen] = useState(false);

  const facultyOptions = getFacultyOptions();
  const yearOptions = getAcademicYearOptions();
  const semesterOptions = getSemesterOptions();

  const handleFacultySelect = (facultyId) => {
    updateStructuralScope(facultyId, null);
    setShowFacultyDropdown(false);
  };

  const handleYearSelect = (yearId) => {
    updateTemporalScope(yearId, activeScope?.temporal?.semesterId);
    setShowSemesterDropdown(false);
  };

  return (
    <header className="top-bar">
      <div className="top-bar-left">
        <button className="menu-toggle" onClick={onMenuToggle} title="Toggle sidebar">
          <Menu size={20} />
        </button>

        <nav className="breadcrumb">
          <div className="breadcrumb-item dropdown">
            <button 
              className="breadcrumb-button"
              onClick={() => setShowFacultyDropdown(!showFacultyDropdown)}
            >
              <span>{getCurrentFacultyName()}</span>
              <ChevronDown size={16} />
            </button>
            {showFacultyDropdown && (
              <div className="dropdown-menu">
                {facultyOptions.map(faculty => (
                  <div 
                    key={faculty.id} 
                    className="dropdown-item"
                    onClick={() => handleFacultySelect(faculty.id)}
                  >
                    {faculty.name}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="breadcrumb-separator">/</div>

          <div className="breadcrumb-item dropdown">
            <button 
              className="breadcrumb-button"
              onClick={() => setShowSemesterDropdown(!showSemesterDropdown)}
            >
              <span>{getCurrentSemesterDisplay()}</span>
              <ChevronDown size={16} />
            </button>
            {showSemesterDropdown && (
              <div className="dropdown-menu">
                {semesterOptions.map(semester => (
                  <div 
                    key={semester.id} 
                    className="dropdown-item"
                    onClick={() => updateTemporalScope(activeScope?.temporal?.academicYearId, semester.id)}
                  >
                    {semester.name} {yearOptions.find(y => y.id === activeScope?.temporal?.academicYearId)?.name}
                  </div>
                ))}
              </div>
            )}
          </div>
        </nav>
      </div>

      <div className="top-bar-right">
        <button 
          className="scope-button"
          onClick={() => setScopeEditorOpen(true)}
          title="Edit scope"
        >
          <Settings size={20} />
        </button>
        <button className="notification-button" title="Notifications">
          <Bell size={20} />
          <span className="notification-badge">3</span>
        </button>
      </div>

      <ScopeEditor 
        isOpen={scopeEditorOpen}
        onClose={() => setScopeEditorOpen(false)}
      />
    </header>
  );
};
