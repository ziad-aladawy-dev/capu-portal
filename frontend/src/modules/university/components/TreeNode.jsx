import { useState } from "react";
import {
  ChevronDown,
  ChevronRight,
  Plus,
  Pencil,
  Trash2,
  ArrowUp,
  ArrowDown,
  Building2,
  GraduationCap,
  Layers,
  BookOpen,
} from "lucide-react";
import { getAllowedChildTypes } from "../utils/nodeTypeHelpers";

const typeIcons = {
  University: Building2,
  Faculty: GraduationCap,
  Department: Layers,
  Program: BookOpen,
  Level: BookOpen,
  System: Layers,
  Specialization: BookOpen,
};

function TreeNode({
  node,
  parentId,
  onAdd,
  onDelete,
  onRename,
  onMove,
  onDropMove,
  selectedNode,
  setSelectedNode,
  matchedIds = [],
  search = "",
}) {
  const [open, setOpen] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const hasChildren = node.children?.length > 0;
  const Icon = typeIcons[node.type] || Layers;
  const canAddChildren = getAllowedChildTypes(node.type).length > 0;

  const isSelected = selectedNode?.id === node.id;
  const isMatched = search && matchedIds.includes(node.id);

  const handleDragStart = (e) => {
    e.dataTransfer.setData("text/plain", JSON.stringify({
      draggedNodeId: node.id,
      draggedNodeType: node.type,
      draggedNodeParentId: parentId
    }));
    e.dataTransfer.effectAllowed = "move";
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    setDragOver(true);
  };

  const handleDragLeave = () => {
    setDragOver(false);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setDragOver(false);
    try {
      const data = JSON.parse(e.dataTransfer.getData("text/plain"));
      const { draggedNodeId, draggedNodeType } = data;
      if (draggedNodeId === node.id) return;
      if (onDropMove) {
        onDropMove(draggedNodeId, node.id, node.children?.length || 0);
      }
    } catch (err) {
      console.error("Drop error", err);
    }
  };

  return (
    <div className="tree-node">
      <div
        className={`tree-node-card ${isSelected ? "selected" : ""} ${
          isMatched ? "matched" : ""
        } ${dragOver ? "drag-over" : ""}`}
        onClick={() => setSelectedNode(node)}
        draggable
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <div className="tree-node-main">
          <button
            type="button"
            className="tree-toggle"
            onClick={(e) => {
              e.stopPropagation();
              setOpen((prev) => !prev);
            }}
            disabled={!hasChildren}
          >
            {hasChildren ? (
              open ? <ChevronDown size={15} /> : <ChevronRight size={15} />
            ) : (
              <span className="tree-toggle-dot" />
            )}
          </button>

          <div className="tree-node-icon">
            <Icon size={15} />
          </div>

          <div className="tree-node-text">
            <strong>{node.name}</strong>
            <span>{node.type} • Depth {node.depth}</span>
          </div>
        </div>

        <div className="tree-node-actions" onClick={(e) => e.stopPropagation()}>
          <button type="button" title="Move up" onClick={() => onMove(node.id, "up", parentId)}>
            <ArrowUp size={13} />
          </button>
          <button type="button" title="Move down" onClick={() => onMove(node.id, "down", parentId)}>
            <ArrowDown size={13} />
          </button>

          {canAddChildren && (
            <button
              type="button"
              title="Add child"
              onClick={() => onAdd(node.id, node.children?.length || 0, node.type)}
            >
              <Plus size={13} />
            </button>
          )}

          <button type="button" title="Rename" onClick={() => onRename(node)}>
            <Pencil size={13} />
          </button>

          <button
            type="button"
            title="Delete"
            className="danger"
            disabled={node.parentId === null}
            onClick={() => onDelete(node.id)}
          >
            <Trash2 size={13} />
          </button>
        </div>
      </div>

      {hasChildren && open && (
        <div className="tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              parentId={node.id}
              onAdd={onAdd}
              onDelete={onDelete}
              onRename={onRename}
              onMove={onMove}
              onDropMove={onDropMove}
              selectedNode={selectedNode}
              setSelectedNode={setSelectedNode}
              matchedIds={matchedIds}
              search={search}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export default TreeNode;