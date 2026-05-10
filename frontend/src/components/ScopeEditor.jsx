import React, { useState } from "react";
import { useScope } from "../hooks/use-scope";
import { X, Check } from "lucide-react";
import "./ScopeEditor.css";

export const ScopeEditor = ({ isOpen, onClose, onSave = null }) => {
  const { activeScope, updateScope, getFacultyOptions, getProgramOptions } = useScope();
  
  const [selectedColleges, setSelectedColleges] = useState(activeScope?.colleges || []);
  const [selectedYear, setSelectedYear] = useState(activeScope?.academicYear || "2024-2025");
  const [selectedSemester, setSelectedSemester] = useState(activeScope?.semester || "Fall");

  const years = ["2023-2024", "2024-2025", "2025-2026"];
  const semesters = ["Fall", "Spring", "Summer"];
  const colleges = getFacultyOptions(); // Returns colleges/faculties

  const handleCollegeToggle = (collegeId) => {
    setSelectedColleges(prev => 
      prev.includes(collegeId)
        ? prev.filter(id => id !== collegeId)
        : [...prev, collegeId]
    );
  };

  const handleSelectAll = () => {
    if (selectedColleges.length === colleges.length) {
      setSelectedColleges([]);
    } else {
      setSelectedColleges(colleges.map(c => c.id));
    }
  };

  const handleSave = () => {
    updateScope({
      colleges: selectedColleges,
      academicYear: selectedYear,
      semester: selectedSemester
    });
    
    if (onSave) {
      onSave();
    }
    onClose();
  };

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div className="scope-editor-backdrop" onClick={onClose} />
      
      {/* Modal */}
      <div className="scope-editor-modal">
        <div className="scope-editor-header">
          <h2>Edit Scope</h2>
          <button className="close-button" onClick={onClose} aria-label="Close">
            <X size={20} />
          </button>
        </div>

        <div className="scope-editor-body">
          {/* Colleges Section */}
          <div className="scope-section">
            <div className="section-header">
              <h3>Colleges & Faculties</h3>
              <button 
                className="select-all-button"
                onClick={handleSelectAll}
              >
                {selectedColleges.length === colleges.length ? "Deselect All" : "Select All"}
              </button>
            </div>
            
            <div className="colleges-list">
              {colleges.map(college => (
                <label key={college.id} className="college-item">
                  <input
                    type="checkbox"
                    checked={selectedColleges.includes(college.id)}
                    onChange={() => handleCollegeToggle(college.id)}
                  />
                  <span className="college-name">{college.name}</span>
                </label>
              ))}
            </div>
          </div>

          {/* Academic Year Section */}
          <div className="scope-section">
            <h3>Academic Year</h3>
            <select 
              value={selectedYear}
              onChange={(e) => setSelectedYear(e.target.value)}
              className="scope-select"
            >
              {years.map(year => (
                <option key={year} value={year}>{year}</option>
              ))}
            </select>
          </div>

          {/* Semester Section */}
          <div className="scope-section">
            <h3>Semester</h3>
            <div className="semester-buttons">
              {semesters.map(semester => (
                <button
                  key={semester}
                  className={`semester-button ${selectedSemester === semester ? "active" : ""}`}
                  onClick={() => setSelectedSemester(semester)}
                >
                  {semester}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="scope-editor-footer">
          <button className="button-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="button-primary" onClick={handleSave}>
            <Check size={18} />
            Save Changes
          </button>
        </div>
      </div>
    </>
  );
};
