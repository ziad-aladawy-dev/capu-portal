import PropTypes from "prop-types";
import { useState, useEffect } from "react";
import { X, ChevronRight, ChevronDown, Building2, GraduationCap, Layers, BookOpen, Award } from "lucide-react";
import { universityStructureService } from "../services/universityStructureService";
import "../styles/scopeModal.css";

const typeIcons = {
  University: Building2,
  Faculty: GraduationCap,
  Department: Layers,
  Program: BookOpen,
  Level: Award,
  System: Layers,
  Specialization: Award,
};

function TreeNode({ node, onSelect, selectedId, level = 0 }) {
  const [open, setOpen] = useState(level < 1);
  const hasChildren = node.children && node.children.length > 0;
  const Icon = typeIcons[node.type] || Building2;
  const isSelected = selectedId === node.id;

  return (
    <div className="scope-tree-node">
      <div
        className={`scope-tree-item ${isSelected ? "selected" : ""}`}
        style={{ paddingLeft: `${level * 20 + 12}px` }}
        onClick={() => onSelect(node)}
      >
        {hasChildren && (
          <button
            className="scope-tree-toggle"
            onClick={(e) => {
              e.stopPropagation();
              setOpen(!open);
            }}
          >
            {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>
        )}
        {!hasChildren && <span className="scope-tree-placeholder" />}
        <div className="scope-tree-icon">
          <Icon size={14} />
        </div>
        <span className="scope-tree-name">{node.name}</span>
        <span className="scope-tree-type">{node.type}</span>
      </div>
      {hasChildren && open && (
        <div className="scope-tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              onSelect={onSelect}
              selectedId={selectedId}
              level={level + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

ScopeTreeModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onClose: PropTypes.func.isRequired,
  onSelect: PropTypes.func.isRequired,
  initialScopeId: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
};

TreeNode.propTypes = {
  node: PropTypes.shape({
    id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
    name: PropTypes.string.isRequired,
    type: PropTypes.string.isRequired,
    children: PropTypes.array,
  }).isRequired,
  onSelect: PropTypes.func.isRequired,
  selectedId: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  level: PropTypes.number,
};

export function ScopeTreeModal({ isOpen, onClose, onSelect, initialScopeId }) {
  const [tree, setTree] = useState([]);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState(initialScopeId);

  useEffect(() => {
    if (isOpen) {
      setLoading(true);
      universityStructureService.getTree()
        .then(setTree)
        .catch(console.error)
        .finally(() => setLoading(false));
    }
  }, [isOpen]);

  const handleSelect = (node) => {
    setSelectedId(node.id);
    onSelect(node);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="scope-modal-overlay" onClick={onClose}>
      <div className="scope-modal-container" onClick={(e) => e.stopPropagation()}>
        <div className="scope-modal-header">
          <h3>Select Scope</h3>
          <button className="scope-modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <div className="scope-modal-body">
          {loading ? (
            <div className="scope-loading">Loading structure...</div>
          ) : tree.length === 0 ? (
            <div className="scope-empty">No structure available</div>
          ) : (
            tree.map((root) => (
              <TreeNode
                key={root.id}
                node={root}
                onSelect={handleSelect}
                selectedId={selectedId}
              />
            ))
          )}
        </div>
      </div>
    </div>
  );
}