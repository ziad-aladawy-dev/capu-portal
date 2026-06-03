import { useState, useEffect } from "react";
import { X } from "lucide-react";
import { universityStructureService } from "../services/universityStructureService";
import {canMoveToParent } from "../utils/nodeTypeHelpers";

const getNodePath = (node, allNodesMap) => {
  const parts = [];
  let current = node;
  while (current) {
    parts.unshift(current.name);
    if (current.parentId && allNodesMap.get(current.parentId)) {
      current = allNodesMap.get(current.parentId);
    } else {
      break;
    }
  }
  return parts.join(" / ");
};

export function MoveNodeModal({ isOpen, onClose, onMove, currentNode }) {
  const [targetParentId, setTargetParentId] = useState("");
  const [order, setOrder] = useState(0);
  const [parentOptions, setParentOptions] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isOpen && currentNode) {
      setLoading(true);
      universityStructureService.getTree()
        .then((tree) => {
          const allNodesMap = new Map();
          const collectNodes = (nodes) => {
            nodes.forEach(n => {
              allNodesMap.set(n.id, n);
              if (n.children) collectNodes(n.children);
            });
          };
          collectNodes(tree);

          const flatten = (nodes, acc = []) => {
            nodes.forEach(n => {
              acc.push(n);
              if (n.children) flatten(n.children, acc);
            });
            return acc;
          };
          const allNodes = flatten(tree);
          
          const validParents = allNodes.filter(parent => {
            if (parent.id === currentNode.id) return false;
            if (currentNode.path && parent.path && parent.path.startsWith(currentNode.path)) return false;
            return canMoveToParent(currentNode.type, parent.type);
          });
          
          const options = [];

          if (canMoveToParent(currentNode.type, null)) {
            options.push({ 
              id: "", 
              label: "-- Root (No Parent) --"
            });
          }

          validParents.forEach(parent => {
            const path = getNodePath(parent, allNodesMap);
            options.push({
              id: parent.id,
              label: path,
            });
          });
          
          setParentOptions(options);

          const currentParentExists = options.some(opt => opt.id === (currentNode.parentId || ""));
          setTargetParentId(currentParentExists ? (currentNode.parentId || "") : "");
          setOrder(currentNode.order ?? 0);
        })
        .catch(err => {
          console.error("Failed to load tree for move modal", err);
          setParentOptions([]);
        })
        .finally(() => setLoading(false));
    }
  }, [isOpen, currentNode]);

  const handleSubmit = (e) => {
    e.preventDefault();
    const newParentId = targetParentId === "" ? null : targetParentId;
    onMove(newParentId, order);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-container" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Move Node: {currentNode?.name}</h3>
          <button className="modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <div className="form-group">
              <label>New Parent (optional)</label>
              {loading ? (
                <div>Loading structure...</div>
              ) : (
                <select
                  value={targetParentId}
                  onChange={(e) => setTargetParentId(e.target.value)}
                >
                  {parentOptions.map((opt) => (
                    <option key={opt.id || "root"} value={opt.id}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              )}
              {parentOptions.length === 0 && !loading && (
                <div className="error-message">No valid parents available for this node.</div>
              )}
              <small className="input-hint">Only allowed parents are shown based on structure rules.</small>
            </div>
            <div className="form-group">
              <label>Order</label>
              <input
                type="number"
                value={order}
                onChange={(e) => setOrder(parseInt(e.target.value) || 0)}
                min="0"
              />
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="btn-primary">
              Move
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}