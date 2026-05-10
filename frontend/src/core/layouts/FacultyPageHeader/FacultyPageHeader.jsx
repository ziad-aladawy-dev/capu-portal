import React from 'react';
import { Plus, Download } from 'lucide-react';
import './FacultyPageHeader.css';

const FacultyPageHeader = ({ title, icon: Icon, onAdd, onExport, showActions = true }) => {
  return (
    <div className="header-section">
      <div className="header-left">
        <div className="header-icon">
          <Icon size={28} />
        </div>
        <div className="header-title-wrap">
          <h1 className="header-title">{title}</h1>
          <div className="gold-line" />
        </div>
      </div>
      
      {showActions && (
        <div className="header-buttons">
          {onExport && (
            <button className="export-btn" onClick={onExport}>
              <Download size={20} /> Export
            </button>
          )}
          {onAdd && (
            <button className="add-btn" onClick={onAdd}>
              <Plus size={20} /> Add {title.split(' ')[0]}
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default FacultyPageHeader;