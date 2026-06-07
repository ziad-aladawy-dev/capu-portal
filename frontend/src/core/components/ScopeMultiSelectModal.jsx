import { useState, useEffect, useCallback } from "react";
import { X, ChevronRight, ChevronDown, Building2, Check } from "lucide-react";
import * as structureService from "../services/structureService";
import "./scopeModal.css";

const TYPE_ICONS = {
  University: Building2,
  Faculty: Building2,
  System: Building2,
  Program: Building2,
  Level: Building2,
  Department: Building2,
  Specialization: Building2,
};

function TreeNode({ node, expandedNodes, toggleNode, selectedIds, onToggle, depth }) {
  const hasChildren = node.children && node.children.length > 0;
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedIds.has(node.id);
  const Icon = TYPE_ICONS[node.type] || Building2;

  return (
    <div className="scope-tree-group">
      <button
        className={`scope-tree-node ${isSelected ? "is-selected" : ""}`}
        onClick={() => onToggle(node.id)}
        style={{ paddingLeft: 12 + depth * 16 }}
      >
        {hasChildren ? (
          <span
            className="scope-tree-toggle"
            onClick={(e) => { e.stopPropagation(); toggleNode(node.id); }}
          >
            {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </span>
        ) : (
          <span className="scope-tree-toggle scope-tree-toggle-placeholder" />
        )}
        <span className={`scope-tree-checkbox ${isSelected ? "checked" : ""}`}>
          {isSelected && <Check size={10} />}
        </span>
        <Icon size={14} className="scope-tree-type-icon" />
        <span className="scope-tree-label">{node.name}</span>
        <span className="scope-tree-type">{node.typeNameLocalized || node.type}</span>
      </button>
      {hasChildren && isExpanded && (
        <div className="scope-tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              expandedNodes={expandedNodes}
              toggleNode={toggleNode}
              selectedIds={selectedIds}
              onToggle={onToggle}
              depth={depth + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ScopeMultiSelectModal({ onClose, initialSelectedIds, onApply }) {
  const [tree, setTree] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedNodes, setExpandedNodes] = useState(new Set());
  const [selectedIds, setSelectedIds] = useState(new Set(initialSelectedIds || []));
  const [selectedNodes, setSelectedNodes] = useState([]);

  useEffect(() => {
    setLoading(true);
    structureService.fetchStructureTree()
      .then((data) => {
        const roots = Array.isArray(data) ? data : [];
        setTree(roots);
        if (roots.length > 0) {
          setExpandedNodes((prev) => new Set(prev).add(roots[0].id));
        }
      })
      .catch(() => setTree([]))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (tree.length === 0 || selectedIds.size === 0) { setSelectedNodes([]); return; }
    const allNodes = [];
    const walk = (nodes) => {
      for (const n of nodes) {
        if (selectedIds.has(n.id)) allNodes.push(n);
        if (n.children) walk(n.children);
      }
    };
    walk(tree);
    setSelectedNodes(allNodes);
  }, [selectedIds, tree]);

  const toggleNode = useCallback((nodeId) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  const handleToggle = useCallback((nodeId) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  const handleApply = useCallback(() => {
    onApply([...selectedIds], selectedNodes.map(n => ({ id: n.id, name: n.name, type: n.typeNameLocalized || n.type })));
    onClose();
  }, [selectedIds, selectedNodes, onApply, onClose]);

  return (
    <div className="scope-modal-overlay" onClick={onClose}>
      <div className="scope-modal scope-modal-wide" onClick={(e) => e.stopPropagation()}>
        <div className="scope-modal-header">
          <h2>Select Scope Nodes</h2>
          <button className="scope-modal-close" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        <div className="scope-modal-body">
          <div className="scope-modal-tree-panel">
            <div className="scope-modal-panel-label">University Structure</div>
            {loading ? (
              <div className="scope-modal-loading">Loading structure...</div>
            ) : tree.length === 0 ? (
              <div className="scope-modal-loading">No structure data available</div>
            ) : (
              <div className="scope-tree-scroll">
                {tree.map((root) => (
                  <TreeNode
                    key={root.id}
                    node={root}
                    expandedNodes={expandedNodes}
                    toggleNode={toggleNode}
                    selectedIds={selectedIds}
                    onToggle={handleToggle}
                    depth={0}
                  />
                ))}
              </div>
            )}
          </div>

          <div className="scope-modal-detail-panel">
            <div className="scope-modal-panel-label">Selection ({selectedNodes.length})</div>
            {selectedNodes.length === 0 ? (
              <div className="scope-selection-empty">
                <Building2 size={24} />
                <p>No scopes selected</p>
                <span>Check nodes from the tree</span>
              </div>
            ) : (
              <div className="scope-selection-list">
                {selectedNodes.map((n) => (
                  <div key={n.id} className="scope-selection-card">
                    <div className="scope-selection-icon">
                      <Building2 size={16} />
                    </div>
                    <div className="scope-selection-info">
                      <strong>{n.name}</strong>
                      <span>{n.typeNameLocalized || n.type}</span>
                    </div>
                    <button
                      className="scope-selection-clear"
                      onClick={() => handleToggle(n.id)}
                      title="Remove"
                    >
                      <X size={12} />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="scope-modal-footer">
          <button className="scope-btn scope-btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="scope-btn scope-btn-primary"
            onClick={handleApply}
          >
            Apply ({selectedNodes.length})
          </button>
        </div>
      </div>
    </div>
  );
}

export default ScopeMultiSelectModal;