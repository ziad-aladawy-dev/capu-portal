import React from "react";
import { PERMISSION_ACTIONS } from "../../lib/constants";
import "./ResourceRow.css";

export const ResourceRow = ({ module, resource, currentLevel, onLevelChange }) => {
  return (
    <tr className="resource-row">
      <td className="resource-name">{resource}</td>
      {PERMISSION_ACTIONS.map((action) => (
        <td key={action.level} className="permission-cell">
          <label className="checkbox-wrapper">
            <input
              type="checkbox"
              className="permission-checkbox"
              checked={currentLevel >= action.level}
              onChange={(e) => {
                if (e.target.checked) {
                  // When checking a higher level, set to that level
                  onLevelChange(module, resource, action.level);
                } else {
                  // When unchecking, set to level below
                  onLevelChange(module, resource, action.level - 1);
                }
              }}
            />
            <span className="checkbox-visual" style={{ borderColor: action.color }}>
              {currentLevel >= action.level && (
                <span className="checkbox-checkmark" style={{ color: action.color }}>✓</span>
              )}
            </span>
          </label>
        </td>
      ))}
    </tr>
  );
};
