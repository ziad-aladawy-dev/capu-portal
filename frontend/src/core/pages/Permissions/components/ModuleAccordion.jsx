import React, { useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { PERMISSION_LEVELS, PERMISSION_ACTIONS } from "../../lib/constants";
import { ResourceRow } from "./ResourceRow";
import "./ModuleAccordion.css";

export const ModuleAccordion = ({ 
  module, 
  resources = [], 
  onPermissionChange,
  onSetModuleLevel 
}) => {
  const [isExpanded, setIsExpanded] = useState(true);

  // Get the highest level in this module
  const maxLevel = Math.max(...resources.map(r => r.level), 0);
  const hasAnyPermission = maxLevel > 0;

  const getPermissionCountBadge = () => {
    const granted = resources.filter(r => r.level > 0).length;
    return `${granted}/${resources.length}`;
  };

  return (
    <div className="module-accordion">
      {/* Header */}
      <button 
        className="module-header"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="module-header-left">
          {isExpanded ? (
            <ChevronDown size={18} />
          ) : (
            <ChevronRight size={18} />
          )}
          <span className="module-name">{module}</span>
        </div>

        <div className="module-header-right">
          <span className="permission-badge">
            {getPermissionCountBadge()}
          </span>
        </div>
      </button>

      {/* Content */}
      {isExpanded && (
        <div className="module-content">
          {/* Bulk action bar */}
          <div className="module-bulk-action">
            <label className="bulk-label">Set all to:</label>
            <select 
              className="bulk-select"
              onChange={(e) => {
                const level = parseInt(e.target.value);
                onSetModuleLevel(module, level);
                e.target.value = "";
              }}
              defaultValue=""
            >
              <option value="">Choose level...</option>
              {PERMISSION_LEVELS.map(perm => (
                <option key={perm.id} value={perm.id}>
                  {perm.name}
                </option>
              ))}
            </select>
          </div>

          {/* Permissions table */}
          <div className="module-table-wrapper">
            <table className="permissions-table">
              <thead>
                <tr>
                  <th className="header-resource">Resource</th>
                  {PERMISSION_ACTIONS.map(action => (
                    <th 
                      key={action.level} 
                      className="header-action"
                      title={PERMISSION_LEVELS[action.level]?.description}
                    >
                      <span className="action-label">{action.name}</span>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {resources.map((resource) => (
                  <ResourceRow
                    key={`${module}-${resource.name}`}
                    module={module}
                    resource={resource.name}
                    currentLevel={resource.level}
                    onLevelChange={onPermissionChange}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
