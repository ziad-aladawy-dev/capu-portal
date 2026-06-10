import { useState } from "react";
import { ChevronDown, ChevronRight, Check } from "lucide-react";
import { getLocalized } from "../utils/getLocalized";
import { getNodeTypeConfig } from "../../modules/university/utils/nodeTypeRegistry";
import NodeTypeBadge from "./NodeTypeBadge";
import "./structureTree.css";

function DefaultNodeRenderer({ node, isSelected, isExpanded, onToggle, onSelect, depth, selectionMode, isChecked, onCheck }) {
  const typeConfig = getNodeTypeConfig(node.type);
  const Icon = typeConfig?.icon;
  const typeColor = typeConfig?.color || "rgba(26,31,94,0.4)";

  return (
    <button
      className={`structure-tree-node ${isSelected ? "is-selected" : ""}`}
      onClick={selectionMode === "multi" ? undefined : () => onSelect(node)}
      style={{ paddingLeft: 12 + depth * 16 }}
    >
      <span
        className="structure-tree-toggle"
        onClick={(e) => { e.stopPropagation(); onToggle(node.id); }}
      >
        {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
      </span>
      {selectionMode === "multi" && (
        <span
          className={`structure-tree-checkbox ${isChecked ? "checked" : ""}`}
          onClick={(e) => { e.stopPropagation(); onCheck(node.id); }}
        >
          {isChecked && <Check size={10} />}
        </span>
      )}
      {Icon && <Icon size={14} style={{ color: typeColor, flexShrink: 0 }} />}
      <span className="structure-tree-label">
        {node.localizedName || getLocalized(node.name, "en") || node.name}
      </span>
      <NodeTypeBadge type={node.type} size="xs" />
      {isSelected && selectionMode !== "multi" && (
        <Check size={12} className="structure-tree-check" />
      )}
    </button>
  );
}

function TreeNode({
  node,
  depth,
  expandedNodes,
  toggleNode,
  selectedId,
  onSelect,
  selectionMode,
  checkedIds,
  onCheck,
  renderNode,
}) {
  const hasChildren = node.children && node.children.length > 0;
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedId === node.id;
  const isChecked = checkedIds?.has(node.id);

  const Renderer = renderNode || DefaultNodeRenderer;

  return (
    <div className="structure-tree-group">
      <Renderer
        node={node}
        isSelected={isSelected}
        isExpanded={isExpanded}
        onToggle={toggleNode}
        onSelect={onSelect}
        depth={depth}
        selectionMode={selectionMode}
        isChecked={isChecked}
        onCheck={onCheck}
      />
      {hasChildren && isExpanded && (
        <div className="structure-tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              depth={depth + 1}
              expandedNodes={expandedNodes}
              toggleNode={toggleNode}
              selectedId={selectedId}
              onSelect={onSelect}
              selectionMode={selectionMode}
              checkedIds={checkedIds}
              onCheck={onCheck}
              renderNode={renderNode}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function StructureTree({
  tree,
  selectedId,
  onSelect,
  selectionMode = "single",
  checkedIds,
  onCheck,
  renderNode,
  defaultExpandedDepth = 0,
}) {
  const [expandedNodes, setExpandedNodes] = useState(() => {
    if (!tree || tree.length === 0) return new Set();
    const initial = new Set();
    const expandToDepth = (nodes, currentDepth) => {
      nodes.forEach((n) => {
        if (currentDepth < defaultExpandedDepth) {
          initial.add(n.id);
        }
        if (n.children) expandToDepth(n.children, currentDepth + 1);
      });
    };
    expandToDepth(tree, 0);
    return initial;
  });

  const toggleNode = (nodeId) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  };

  if (!tree || tree.length === 0) return null;

  return (
    <div className="structure-tree-scroll">
      {tree.map((root) => (
        <TreeNode
          key={root.id}
          node={root}
          depth={0}
          expandedNodes={expandedNodes}
          toggleNode={toggleNode}
          selectedId={selectedId}
          onSelect={onSelect}
          selectionMode={selectionMode}
          checkedIds={checkedIds}
          onCheck={onCheck}
          renderNode={renderNode}
        />
      ))}
    </div>
  );
}

export { DefaultNodeRenderer, StructureTree };
export default StructureTree;
