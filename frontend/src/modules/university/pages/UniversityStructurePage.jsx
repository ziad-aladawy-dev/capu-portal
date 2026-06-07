import { useState, useEffect, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Network, Search, Plus, ExternalLink } from "lucide-react";
import TreeNode from "../components/TreeNode";
import { AddEditNodeModal } from "../components/AddEditNodeModal";
import { MoveNodeModal } from "../components/MoveNodeModal";
import { ConfirmDeleteModal } from "../components/ConfirmDeleteModal";
import { useUniversityStructure } from "../hooks/useUniversityStructure";
import { universityStructureService } from "../services/universityStructureService";
import { getLocalized } from "../../../core/utils/getLocalized";
import { canMoveToParent, getContextualActions } from "../utils/nodeTypeHelpers";
import NodeTypeBadge from "../../../core/components/NodeTypeBadge";
import "../styles/universityStructure.css";
import "../styles/scopeModal.css";

const UniversityStructurePage = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
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
  const [expandedNodes, setExpandedNodes] = useState(new Set());

  const toggleNode = useCallback((nodeId) => {
    setExpandedNodes(prev => {
      const newSet = new Set(prev);
      if (newSet.has(nodeId)) {
        newSet.delete(nodeId);
      } else {
        newSet.add(nodeId);
      }
      return newSet;
    });
  }, []);

  const findNodeInTree = useCallback((nodes, nodeId) => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children?.length) {
        const found = findNodeInTree(node.children, nodeId);
        if (found) return found;
      }
    }
    return null;
  }, []);

  const getParentTypeForNode = useCallback((node) => {
    if (!node?.parentId) return null;
    const parent = findNodeInTree(treeData, node.parentId);
    return parent?.type || null;
  }, [treeData, findNodeInTree]);

  const loadBreadcrumb = useCallback(async (node) => {
    if (node?.id) {
      try {
        const bc = await universityStructureService.getBreadcrumb(node.id);
        const translatedBc = bc.map(item => ({
          ...item,
          name: item.localizedName || getLocalized(item.name, i18n.language)
        }));
        setBreadcrumb(translatedBc);
      } catch (err) {
        console.error("Failed to load breadcrumb", err);
        setBreadcrumb([]);
      }
    } else {
      setBreadcrumb([]);
    }
  }, [i18n.language]);

  const displayNodeName = useMemo(() => {
    if (selectedNode?.localizedName) return selectedNode.localizedName;
    if (!selectedNode?.name) return "";
    return getLocalized(selectedNode.name, i18n.language);
  }, [selectedNode, i18n.language]);

  useEffect(() => {
    if (selectedNode) {
      loadBreadcrumb(selectedNode);
    }
  }, [selectedNode, i18n.language, loadBreadcrumb]);

  const handleAddClick = (parentId, currentChildrenCount, parentType) => {
    setParentIdForAdd(parentId);
    setSiblingsCount(currentChildrenCount);
    setParentTypeForAdd(parentType || null);
    setShowAddModal(true);
  };

  const handleSaveAdd = async (request) => {
    const result = await createNode(request);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
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
    const result = await updateNode(nodeId, request);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
    }
  };

  const handleMoveClick = () => {
    if (selectedNode) setShowMoveModal(true);
  };

  const handleMove = async (newParentId, order) => {
    if (!selectedNode) return;
    const targetParentNode = newParentId ? findNodeInTree(treeData, newParentId) : null;
    const targetParentType = targetParentNode?.type || null;
    if (!canMoveToParent(selectedNode.type, targetParentType)) {
      alert(`${t("cannot_move")} ${selectedNode.type} ${t("under")} ${targetParentType || t("root")}`);
      return;
    }
    const result = await moveNode(selectedNode.id, newParentId, order);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
    }
  };

  const handleDragDropMove = async (draggedNodeId, targetNodeId, newOrder) => {
    const draggedNode = findNodeInTree(treeData, draggedNodeId);
    const targetNode = findNodeInTree(treeData, targetNodeId);
    if (!draggedNode || !targetNode) return;
    const targetParentType = targetNode.type;
    if (!canMoveToParent(draggedNode.type, targetParentType)) {
      alert(`${t("cannot_move")} ${draggedNode.type} ${t("under")} ${targetParentType}`);
      return;
    }
    const result = await moveNode(draggedNodeId, targetNodeId, newOrder);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
    }
  };

  const handleDeleteClick = (nodeIdToDelete) => {
    const node = nodeIdToDelete ? findNodeInTree(treeData, nodeIdToDelete) : selectedNode;
    if (node && node.parentId !== null) {
      setNodeToDelete(node);
      setShowDeleteConfirm(true);
    }
  };

  const handleConfirmDelete = async () => {
    if (!nodeToDelete) return;
    const result = await deleteNode(nodeToDelete.id);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
    }
    setNodeToDelete(null);
    setShowDeleteConfirm(false);
  };

  const handleReorder = async (nodeId, direction, parentId) => {
    const node = findNodeInTree(treeData, nodeId);
    if (!node) return;
    let siblings = [];
    if (parentId) {
      const parent = findNodeInTree(treeData, parentId);
      if (parent?.children) siblings = parent.children;
    } else {
      siblings = treeData;
    }
    const currentIndex = siblings.findIndex(child => child.id === nodeId);
    if (currentIndex === -1) return;
    let newOrder = currentIndex;
    if (direction === 'up' && currentIndex > 0) newOrder = currentIndex - 1;
    else if (direction === 'down' && currentIndex < siblings.length - 1) newOrder = currentIndex + 1;
    else return;
    const result = await reorderNode(nodeId, newOrder);
    if (!result.success) {
      alert(`${t("error")}: ${result.error}`);
    }
  };

  const handleSelectNode = (node) => {
    setSelectedNode(node);
  };

  if (loading) return <div className="structure-page">{t("loading")}</div>;
  if (error) return <div className="structure-page error">{t("error")}: {error}</div>;

  return (
    <div className="structure-page">
      <div className="structure-header">
        <div className="structure-title">
          <div className="structure-title-icon"><Network size={20} /></div>
          <div>
            <span>{t("university_module")}</span>
            <h1>{t("university_structure")}</h1>
            <p>{t("manage_structure")}</p>
          </div>
        </div>
        <button className="structure-add-btn" onClick={() => handleAddClick(null, treeData.length, null)}>
          <Plus size={16} /> {t("add_root")}
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
          placeholder={t("search_hierarchy")}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="structure-layout">
        <div className="structure-tree-container page-card">
          {treeData.map((root) => (
            <TreeNode
              key={`${root.id}-${i18n.language}`}
              node={root}
              parentId={null}
              onAdd={handleAddClick}
              onDelete={handleDeleteClick}
              onRename={handleEditClick}
              onMove={handleReorder}
              onDropMove={handleDragDropMove}
              selectedNode={selectedNode}
              setSelectedNode={handleSelectNode}
              expandedNodes={expandedNodes}
              onToggleNode={toggleNode}
              matchedIds={[]}
              search={searchTerm}
            />
          ))}
        </div>

        <div className="details-panel page-card">
          {selectedNode ? (
            <>
              <div className="details-title"><h3>{t("node_details")}</h3></div>
              <div className="details-grid">
                <div><span>{t("name")}</span><strong>{displayNodeName}</strong></div>
                <div><span>{t("type")}</span><strong><NodeTypeBadge type={selectedNode.type} size="lg" /></strong></div>
                <div><span>{t("depth")}</span><strong>{selectedNode.depth}</strong></div>
                <div><span>{t("children_count")}</span><strong>{selectedNode.children?.length || 0}</strong></div>
              </div>
              <div className="details-actions" style={{ marginTop: 16, display: "flex", gap: 8 }}>
                <button className="btn-primary" onClick={() => handleEditClick(selectedNode)}>{t("edit")}</button>
                {selectedNode.parentId !== null && (
                  <button className="btn-secondary" onClick={handleMoveClick}>{t("move")}</button>
                )}
              </div>
              {(() => {
                const actions = getContextualActions(selectedNode.type);
                if (actions.length === 0) return null;
                return (
                  <div className="contextual-actions" style={{ marginTop: 12 }}>
                    <label style={{ fontSize: 12, color: "#888", marginBottom: 6, display: "block" }}>{t("quick_actions")}</label>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                      {actions.map((action) => {
                        const Icon = action.icon;
                        return (
                          <button
                            key={action.capability}
                            className="btn-secondary"
                            style={{ display: "inline-flex", alignItems: "center", gap: 4, fontSize: 12, padding: "4px 10px" }}
                            onClick={() => navigate(action.path)}
                          >
                            <Icon size={13} />
                            {t(action.labelKey)}
                            <ExternalLink size={11} />
                          </button>
                        );
                      })}
                    </div>
                  </div>
                );
              })()}
            </>
          ) : (
            <div className="empty-selection">{t("select_node")}</div>
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
            nodeName={nodeToDelete?.localizedName || nodeToDelete?.name}
          />
        </>
      )}
    </div>
  );
};

export default UniversityStructurePage;