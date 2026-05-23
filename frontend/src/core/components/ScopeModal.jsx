import { useState, useEffect, useCallback } from "react";
import { X, ChevronRight, ChevronDown, Building2, Check } from "lucide-react";
import * as structureService from "../services/structureService";
import { useDomain } from "../contexts/DomainContext";
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

function TreeNode({ node, expandedNodes, toggleNode, selectedId, onSelect, depth }) {
  const hasChildren = node.children && node.children.length > 0;
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedId === node.id;
  const Icon = TYPE_ICONS[node.type] || Building2;

  return (
    <div className="scope-tree-group">
      <button
        className={`scope-tree-node ${isSelected ? "is-selected" : ""}`}
        onClick={() => onSelect(node)}
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
        <Icon size={14} className="scope-tree-type-icon" />
        <span className="scope-tree-label">{node.name}</span>
        <span className="scope-tree-type">{node.typeNameLocalized || node.type}</span>
        {isSelected && <Check size={12} className="scope-tree-check" />}
      </button>
      {hasChildren && isExpanded && (
        <div className="scope-tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              expandedNodes={expandedNodes}
              toggleNode={toggleNode}
              selectedId={selectedId}
              onSelect={onSelect}
              depth={depth + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ScopeModal({ onClose }) {
  const { scopeNode, selectScopeNode } = useDomain();
  const [tree, setTree] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedNodes, setExpandedNodes] = useState(new Set());
  const [selectedNode, setSelectedNode] = useState(null);

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
    if (scopeNode) setSelectedNode(scopeNode);
  }, [scopeNode]);

  const toggleNode = useCallback((nodeId) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  const handleSelect = useCallback((node) => {
    setSelectedNode(node);
  }, []);

  const handleApply = useCallback(() => {
    selectScopeNode(selectedNode);
    onClose();
  }, [selectedNode, selectScopeNode, onClose]);

  const handleClear = useCallback(() => {
    setSelectedNode(null);
  }, []);

  return (
    <div className="scope-modal-overlay" onClick={onClose}>
      <div className="scope-modal" onClick={(e) => e.stopPropagation()}>
        <div className="scope-modal-header">
          <h2>Select Scope</h2>
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
                    selectedId={selectedNode?.id}
                    onSelect={handleSelect}
                    depth={0}
                  />
                ))}
              </div>
            )}
          </div>

          <div className="scope-modal-detail-panel">
            <div className="scope-modal-panel-label">Selection</div>
            {selectedNode ? (
              <div className="scope-selection-card">
                <div className="scope-selection-icon">
                  <Building2 size={18} />
                </div>
                <div className="scope-selection-info">
                  <strong>{selectedNode.name}</strong>
                  <span>{selectedNode.typeNameLocalized || selectedNode.type}</span>
                </div>
                <button className="scope-selection-clear" onClick={handleClear} title="Clear selection">
                  <X size={14} />
                </button>
              </div>
            ) : (
              <div className="scope-selection-empty">
                <Building2 size={24} />
                <p>No scope selected</p>
                <span>All data will be shown</span>
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
            disabled={!selectedNode}
          >
            Apply Scope
          </button>
        </div>
      </div>
    </div>
  );
}

export default ScopeModal;
