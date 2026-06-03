import { useState } from "react";
import { Network, Search, Plus } from "lucide-react";
import TreeNode from "../components/TreeNode";
import { AddEditNodeModal } from "../components/AddEditNodeModal";
import { MoveNodeModal } from "../components/MoveNodeModal";
import { ConfirmDeleteModal } from "../components/ConfirmDeleteModal";
import { useUniversityStructure } from "../hooks/useUniversityStructure";
import { universityStructureService } from "../services/universityStructureService";
import { normalizeType, canMoveToParent } from "../utils/nodeTypeHelpers";
import { useToast } from "../../../core/components/Toast";
import "../styles/universityStructure.css";
import "../styles/scopeModal.css";

const UniversityStructurePage = () => {
  const { addToast } = useToast();
  const {
    treeData,
    loading,
    error,
    selectedNode,
    setSelectedNode,
    createNode,
    updateNode,
    deleteNode,
    reorderNode,
    moveNode,
  } = useUniversityStructure();

  const [searchTerm, setSearchTerm] = useState("");
  const [showAddModal, setShowAddModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showMoveModal, setShowMoveModal] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [parentIdForAdd, setParentIdForAdd] = useState(null);
  const [parentTypeForAdd, setParentTypeForAdd] = useState(null);
  const [siblingsCount, setSiblingsCount] = useState(0);
  const [breadcrumb, setBreadcrumb] = useState([]);
  const [editParentType, setEditParentType] = useState(null);
  const [nodeToDelete, setNodeToDelete] = useState(null);

  const findNodeInTree = (nodes, nodeId) => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children && node.children.length) {
        const found = findNodeInTree(node.children, nodeId);
        if (found) return found;
      }
    }
    return null;
  };

  const getParentTypeForNode = (node) => {
    if (!node || !node.parentId) return null;
    const parent = findNodeInTree(treeData, node.parentId);
    return parent ? parent.type : null;
  };

  const handleAddClick = (parentId, currentChildrenCount, parentType) => {
    setParentIdForAdd(parentId);
    setSiblingsCount(currentChildrenCount);
    setParentTypeForAdd(parentType || null);
    setShowAddModal(true);
  };

  const handleSaveAdd = async (request) => {
    const result = await createNode(request);
    if (!result.success) {
      addToast(`Error: ${result.error}`, "error");
    }
  };

  const handleEditClick = (nodeToEdit) => {
    const targetNode = nodeToEdit || selectedNode;
    if (targetNode) {
      setSelectedNode(targetNode);
      setEditParentType(getParentTypeForNode(targetNode));
      setShowEditModal(true);
    }
  };

  const handleSaveEdit = async (request, nodeId) => {
    await updateNode(nodeId, request);
  };

  const handleMoveClick = () => {
    if (selectedNode) setShowMoveModal(true);
  };

  const handleMove = async (newParentId, order) => {
    if (!selectedNode) return;
    const targetParentNode = newParentId ? findNodeInTree(treeData, newParentId) : null;
    const targetParentType = targetParentNode ? targetParentNode.type : null;
    if (!canMoveToParent(selectedNode.type, targetParentType)) {
      addToast(`Cannot move ${selectedNode.type} under ${targetParentType || "Root"}`, "error");
      return;
    }
    await moveNode(selectedNode.id, newParentId, order);
    setSelectedNode(null);
  };

  const handleDragDropMove = async (draggedNodeId, targetNodeId, newOrder) => {
    const draggedNode = findNodeInTree(treeData, draggedNodeId);
    const targetNode = findNodeInTree(treeData, targetNodeId);
    if (!draggedNode || !targetNode) return;

    const targetParentType = targetNode.type;
    if (!canMoveToParent(draggedNode.type, targetParentType)) {
      addToast(`Cannot move ${draggedNode.type} under ${targetParentType}`, "error");
      return;
    }

    await moveNode(draggedNodeId, targetNodeId, newOrder);
  };

  const handleDeleteClick = (nodeIdToDelete) => {
    const nodeToDelete = nodeIdToDelete ? findNodeInTree(treeData, nodeIdToDelete) : selectedNode;
    if (nodeToDelete && nodeToDelete.parentId !== null) {
      setNodeToDelete(nodeToDelete);
      setShowDeleteConfirm(true);
    }
  };

  const handleConfirmDelete = async () => {
    if (!nodeToDelete) return;
    await deleteNode(nodeToDelete.id);
    setSelectedNode(null);
    setNodeToDelete(null);
    setShowDeleteConfirm(false);
  };

  const handleReorder = async (nodeId, direction, parentId) => {
    const node = findNodeInTree(treeData, nodeId);
    if (!node) return;
    let siblings = [];
    if (parentId) {
      const parent = findNodeInTree(treeData, parentId);
      if (parent && parent.children) siblings = parent.children;
    } else {
      siblings = treeData;
    }
    const currentIndex = siblings.findIndex(child => child.id === nodeId);
    if (currentIndex === -1) return;
    let newOrder = currentIndex;
    if (direction === 'up' && currentIndex > 0) newOrder = currentIndex - 1;
    else if (direction === 'down' && currentIndex < siblings.length - 1) newOrder = currentIndex + 1;
    else return;
    await reorderNode(nodeId, newOrder);
  };

  const loadBreadcrumb = async (node) => {
    if (node) {
      try {
        const bc = await universityStructureService.getBreadcrumb(node.id);
        setBreadcrumb(bc);
      } catch (err) {
        console.error("Failed to load breadcrumb", err);
        setBreadcrumb([]);
      }
    } else {
      setBreadcrumb([]);
    }
  };

  const handleSelectNode = (node) => {
    setSelectedNode(node);
    loadBreadcrumb(node);
  };

  if (loading) return <div className="structure-page">Loading...</div>;
  if (error) return <div className="structure-page error">Error: {error}</div>;

  return (
    <div className="structure-page">
      <div className="structure-header">
        <div className="structure-title">
          <div className="structure-title-icon">
            <Network size={20} />
          </div>
          <div>
            <span>University Module</span>
            <h1>University Structure</h1>
            <p>Manage faculties, departments, programs and levels.</p>
          </div>
        </div>
        <button className="structure-add-btn" onClick={() => handleAddClick(null, treeData.length, null)}>
          <Plus size={16} /> Add Root
        </button>
      </div>

      {breadcrumb.length > 0 && (
        <div className="breadcrumb-bar page-card">
          {breadcrumb.map((item, idx) => (
            <span key={item.id}>
              {item.name}
              {idx < breadcrumb.length - 1 && " / "}
            </span>
          ))}
        </div>
      )}

      <div className="tree-search-bar page-card">
        <Search size={15} />
        <input
          type="text"
          placeholder="Search hierarchy..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="structure-layout">
        <div className="structure-tree-container page-card">
          {treeData.map((root) => (
            <TreeNode
              key={root.id}
              node={root}
              parentId={null}
              onAdd={handleAddClick}
              onDelete={handleDeleteClick}
              onRename={handleEditClick}
              onMove={handleReorder}
              onDropMove={handleDragDropMove}
              selectedNode={selectedNode}
              setSelectedNode={handleSelectNode}
              matchedIds={[]}
              search={searchTerm}
            />
          ))}
        </div>

        <div className="details-panel page-card">
          {selectedNode ? (
            <>
              <div className="details-title">
                <h3>Node Details</h3>
              </div>
              <div className="details-grid">
                <div><span>Name</span><strong>{selectedNode.name}</strong></div>
                <div><span>Type</span><strong>{normalizeType(selectedNode.type)}</strong></div>
                <div><span>Depth</span><strong>{selectedNode.depth}</strong></div>
                <div><span>Children Count</span><strong>{selectedNode.children?.length || 0}</strong></div>
              </div>
              <div className="details-actions" style={{ marginTop: 16, display: "flex", gap: 8 }}>
                <button className="btn-primary" onClick={() => handleEditClick(selectedNode)}>Edit</button>
                {selectedNode.parentId !== null && (
                  <button className="btn-secondary" onClick={handleMoveClick}>Move</button>
                )}
                {selectedNode.parentId !== null && (
                  <button className="btn-danger" onClick={handleDeleteClick}>Delete</button>
                )}
              </div>
            </>
          ) : (
            <div className="empty-selection">Select a node to view details.</div>
          )}
        </div>
      </div>

      <AddEditNodeModal
        isOpen={showAddModal}
        onClose={() => setShowAddModal(false)}
        onSave={handleSaveAdd}
        parentId={parentIdForAdd}
        parentType={parentTypeForAdd}
        siblingsCount={siblingsCount}
      />

      {selectedNode && (
        <>
          <AddEditNodeModal
            isOpen={showEditModal}
            onClose={() => setShowEditModal(false)}
            onSave={handleSaveEdit}
            node={selectedNode}
            currentParentType={editParentType}
          />
          <MoveNodeModal
            isOpen={showMoveModal}
            onClose={() => setShowMoveModal(false)}
            onMove={handleMove}
            currentNode={selectedNode}
          />
          <ConfirmDeleteModal
            isOpen={showDeleteConfirm}
            onClose={() => setShowDeleteConfirm(false)}
            onConfirm={handleConfirmDelete}
            nodeName={nodeToDelete?.name}
          />
        </>
      )}
    </div>
  );
};

export default UniversityStructurePage;