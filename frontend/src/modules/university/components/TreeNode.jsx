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

const typeIcons = {
  University: Building2,
  Faculty: GraduationCap,
  Department: Layers,
  Program: BookOpen,
  Level: BookOpen,
};

function TreeNode({
  node,
  parentId,
  onAdd,
  onDelete,
  onRename,
  onMove,
  selectedNode,
  setSelectedNode,
  matchedIds = [],
  search = "",
}) {
  const [open, setOpen] = useState(true);

  const hasChildren = node.children?.length > 0;
  const Icon = typeIcons[node.type] || Layers;

  const isSelected = selectedNode?.id === node.id;
  const isMatched = search && matchedIds.includes(node.id);

  return (
    <div className="tree-node">
      <div
        className={`tree-node-card ${isSelected ? "selected" : ""} ${
          isMatched ? "matched" : ""
        }`}
        onClick={() => setSelectedNode(node)}
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
          <button type="button" title="Move up" onClick={() => onMove(parentId, node.id, "up")}>
            <ArrowUp size={13} />
          </button>

          <button type="button" title="Move down" onClick={() => onMove(parentId, node.id, "down")}>
            <ArrowDown size={13} />
          </button>

          <button type="button" title="Add child" onClick={() => onAdd(node.id)}>
            <Plus size={13} />
          </button>

          <button type="button" title="Rename" onClick={() => onRename(node.id)}>
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
