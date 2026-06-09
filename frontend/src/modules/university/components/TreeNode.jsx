import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ChevronDown,
  ChevronRight,
  Plus,
  Pencil,
  Trash2,
  ArrowUp,
  ArrowDown,
} from "lucide-react";
import { getLocalized } from "../../../core/utils/getLocalized";
import { getAllowedChildTypes } from "../utils/nodeTypeHelpers";
import { getNodeTypeConfig } from "../utils/nodeTypeRegistry";
import NodeTypeBadge from "../../../core/components/NodeTypeBadge";
import PermissionGate from "../../../core/auth/PermissionGate";

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
  expandedNodes = new Set(),
  onToggleNode = () => {},
  matchedIds = [],
  search = "",
  permissionResource = "structure.structure",
}) {
  const { t, i18n } = useTranslation();
  const hasChildren = node.children?.length > 0;
  const typeConfig = getNodeTypeConfig(node.type);
  const Icon = typeConfig?.icon || null;
  const typeColor = typeConfig?.color || "#6b7280";
  const canAddChildren = getAllowedChildTypes(node.type).length > 0;

  const isOpen = expandedNodes.has(node.id);
  const [dragOver, setDragOver] = useState(false);

  const isSelected = selectedNode?.id === node.id;
  const isMatched = search && matchedIds.includes(node.id);

  const displayName = node.localizedName || getLocalized(node.name, i18n.language);

  const handleToggle = (e) => {
    e.stopPropagation();
    onToggleNode(node.id);
  };

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
      const { draggedNodeId } = data;
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
        className={`tree-node-card ${isSelected ? "selected" : ""} ${isMatched ? "matched" : ""} ${dragOver ? "drag-over" : ""}`}
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
            onClick={handleToggle}
            disabled={!hasChildren}
          >
            {hasChildren ? (
              isOpen ? <ChevronDown size={15} /> : <ChevronRight size={15} />
            ) : (
              <span className="tree-toggle-dot" />
            )}
          </button>

          {Icon && (
            <div className="tree-node-icon" style={{ background: `linear-gradient(135deg, ${typeColor}, ${typeColor}dd)`, color: "#fff" }}>
              <Icon size={15} />
            </div>
          )}

          <div className="tree-node-text">
            <strong>{displayName}</strong>
            <span>
              <NodeTypeBadge type={node.type} showIcon={false} size="xs" />
              {" • "}{t("depth")} {node.depth}
            </span>
          </div>
        </div>

        <div className="tree-node-actions" onClick={(e) => e.stopPropagation()}>
          <PermissionGate resource={permissionResource} minLevel={3}>
            <button type="button" title={t("move_up")} onClick={() => onMove(node.id, "up", parentId)}>
              <ArrowUp size={13} />
            </button>
          </PermissionGate>
          <PermissionGate resource={permissionResource} minLevel={3}>
            <button type="button" title={t("move_down")} onClick={() => onMove(node.id, "down", parentId)}>
              <ArrowDown size={13} />
            </button>
          </PermissionGate>
          {canAddChildren && (
            <PermissionGate resource={permissionResource} minLevel={2}>
              <button
                type="button"
                title={t("add_child")}
                onClick={() => onAdd(node.id, node.children?.length || 0, node.type)}
              >
                <Plus size={13} />
              </button>
            </PermissionGate>
          )}
          <PermissionGate resource={permissionResource} minLevel={3}>
            <button type="button" title={t("rename")} onClick={() => onRename(node)}>
              <Pencil size={13} />
            </button>
          </PermissionGate>
          <PermissionGate resource={permissionResource} minLevel={5}>
            <button
              type="button"
              title={t("delete")}
              className="danger"
              disabled={node.parentId === null}
              onClick={() => onDelete(node.id)}
            >
              <Trash2 size={13} />
            </button>
          </PermissionGate>
        </div>
      </div>

      {hasChildren && isOpen && (
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
              expandedNodes={expandedNodes}
              onToggleNode={onToggleNode}
              matchedIds={matchedIds}
              search={search}
              permissionResource={permissionResource}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export default TreeNode;