import React, { useState } from 'react';
import { Menu, Moon, Bell, ChevronDown, Settings } from 'lucide-react';
import { useScope } from '../../../hooks/use-scope';
import { ScopeEditor } from '../../components/ScopeEditor';
import './TopNav.css';

const TopNav = ({ onMenuClick }) => {
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
    <div className="top-nav">
      <div className="nav-left">
        <Menu size={24} className="menu-icon" onClick={onMenuClick}/>

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

      <div className="nav-right">
        <button
          className="scope-button"
          onClick={() => setScopeEditorOpen(true)}
          title="Edit scope"
        >
          <Settings size={20} />
        </button>
        <Moon size={20} className="nav-icon" />
        <button className="notification-button" title="Notifications">
          <Bell size={20} />
          <span className="notification-badge">3</span>
        </button>
      </div>

      <ScopeEditor
        isOpen={scopeEditorOpen}
        onClose={() => setScopeEditorOpen(false)}
      />
    </div>
  );
};

export default TopNav;