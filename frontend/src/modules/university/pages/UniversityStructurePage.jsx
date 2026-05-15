import { useMemo, useState, useEffect, useCallback } from "react";
import { Plus, Search, Network, Building2 } from "lucide-react";

import TreeNode from "../components/TreeNode";
import { searchTree } from "../utils/treeSearch";
import { getAncestors } from "../utils/treeUtils";

import * as structureService from "../../../core/services/structureService";

import "../styles/universityStructure.css";

const TYPE_MAP = {
  1: "University",
  2: "Faculty",
  3: "System",
  4: "Program",
  5: "Level",
  6: "Department",
  7: "Specialization",
};

const TYPE_REVERSE = {
  University: 1,
  Faculty: 2,
  System: 3,
  Program: 4,
  Level: 5,
  Department: 6,
  Specialization: 7,
};

function flattenTree(node, nodesMap = {}) {
  nodesMap[node.id] = {
    id: node.id,
    name: node.name,
    type: TYPE_MAP[node.type] || "Department",
    parentId: node.parentId,
    childrenIds: (node.children || []).map((c) => c.id),
    path: node.path || `${node.id}`,
    depth: node.depth || 0,
    order: node.order || 0,
  };
  (node.children || []).forEach((c) => flattenTree(c, nodesMap));
  return nodesMap;
}

function convertTreeTypes(node) {
  return {
    ...node,
    type: TYPE_MAP[node.type] || "Department",
    children: (node.children || []).map(convertTreeTypes),
  };
}

function UniversityStructurePage() {
  const [treeData, setTreeData] = useState(null);
  const [treeLoading, setTreeLoading] = useState(true);
  const [treeError, setTreeError] = useState(null);
  const [selectedNode, setSelectedNode] = useState(null);
  const [search, setSearch] = useState("");

  const loadTree = useCallback(async () => {
    setTreeLoading(true);
    setTreeError(null);
    try {
      const roots = await structureService.fetchStructureTree();
      const rootsArray = Array.isArray(roots) ? roots : [];
      const nodes = {};
      rootsArray.forEach((root) => flattenTree(root, nodes));
      setTreeData({ rootId: rootsArray.length > 0 ? rootsArray[0].id : null, nodes, roots: rootsArray });
    } catch (err) {
      setTreeError(err.message || "Failed to load structure");
      setTreeData(null);
    } finally {
      setTreeLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTree();
  }, [loadTree]);

  const renderedTree = useMemo(() => {
    if (!treeData?.roots?.length) return null;
    if (treeData.roots.length === 1) return convertTreeTypes(treeData.roots[0]);
    return {
      id: "virtual-root",
      name: "University Structure",
      type: "University",
      parentId: null,
      children: treeData.roots.map(convertTreeTypes),
      depth: 0,
      path: "virtual-root",
    };
  }, [treeData]);

  const searchResult = useMemo(() => {
    if (!renderedTree || !search.trim()) return { matchedIds: [], visible: true };
    return searchTree(renderedTree, search);
  }, [renderedTree, search]);

  const handleAddNode = async (parentId) => {
    const nodeName = prompt("Node Name");
    if (!nodeName) return;
    const nodeTypeStr = prompt("Node Type: Faculty / Department / Program / Level", "Department") || "Department";
    const nodeType = TYPE_REVERSE[nodeTypeStr] || 6;
    try {
      await structureService.createStructureNode({
        name: nodeName,
        type: nodeType,
        parentId: parentId === "virtual-root" ? null : parentId,
        order: 0,
      });
      await loadTree();
    } catch (err) {
      alert("Failed to create node: " + (err.message || "Unknown error"));
    }
  };

  const handleDeleteNode = async (nodeId) => {
    if (nodeId === treeData?.rootId) return;
    if (!window.confirm("Delete this node and all its children?")) return;
    try {
      await structureService.deleteStructureNode(nodeId);
      if (selectedNode?.id === nodeId) setSelectedNode(null);
      await loadTree();
    } catch (err) {
      alert("Failed to delete node: " + (err.message || "Unknown error"));
    }
  };

  const handleRenameNode = async (nodeId) => {
    const node = treeData?.nodes?.[nodeId];
    const currentName = node?.name || "";
    const newName = prompt("New Name", currentName);
    if (!newName) return;
    try {
      await structureService.updateStructureNode(nodeId, { name: newName });
      await loadTree();
    } catch (err) {
      alert("Failed to rename node: " + (err.message || "Unknown error"));
    }
  };

  const handleMoveNode = async (parentId, nodeId, direction) => {
    if (!parentId || parentId === "virtual-root") return;
    try {
      const parent = treeData?.nodes?.[parentId];
      if (!parent) return;
      const childrenIds = parent.childrenIds;
      const index = childrenIds.indexOf(nodeId);
      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= childrenIds.length) return;
      await structureService.reorderStructureNode(nodeId, { newOrder: targetIndex + 1 });
      await loadTree();
    } catch (err) {
      alert("Failed to move node: " + (err.message || "Unknown error"));
    }
  };

  const breadcrumb = useMemo(() => {
    if (!selectedNode || !treeData?.nodes) return [];
    const ancestors = getAncestors(selectedNode.id, treeData.nodes);
    return [...ancestors.map((item) => item.name), selectedNode.name];
  }, [selectedNode, treeData]);

  const stats = useMemo(() => {
    if (!treeData?.nodes) return { total: 0, faculties: 0, departments: 0, programs: 0 };
    const nodes = Object.values(treeData.nodes);
    return {
      total: nodes.length,
      faculties: nodes.filter((node) => node.type === "Faculty").length,
      departments: nodes.filter((node) => node.type === "Department").length,
      programs: nodes.filter((node) => node.type === "Program").length,
    };
  }, [treeData]);

  if (treeLoading) {
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
              <p>Loading structure...</p>
            </div>
          </div>
        </div>
        <div style={{ padding: 48, textAlign: "center", color: "var(--text-muted)" }}>
          Loading...
        </div>
      </div>
    );
  }

  if (treeError) {
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
              <p>{treeError}</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (!renderedTree) {
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
              <p>No structure data available.</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

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
      </div>

      {selectedNode && (
        <div className="breadcrumb-bar page-card">
          {breadcrumb.join(" / ")}
        </div>
      )}

      <div className="tree-search-bar page-card">
        <Search size={15} />
        <input
          type="text"
          placeholder="Search hierarchy..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      <div className="structure-layout">
        <div className="structure-tree-container page-card">
          {renderedTree.id === "virtual-root" ? (
            renderedTree.children.map((root) => (
              <TreeNode
                key={root.id}
                node={root}
                parentId={null}
                onAdd={handleAddNode}
                onDelete={handleDeleteNode}
                onRename={handleRenameNode}
                onMove={handleMoveNode}
                selectedNode={selectedNode}
                setSelectedNode={setSelectedNode}
                matchedIds={searchResult.matchedIds}
                search={search}
              />
            ))
          ) : (
            <TreeNode
              node={renderedTree}
              parentId={null}
              onAdd={handleAddNode}
              onDelete={handleDeleteNode}
              onRename={handleRenameNode}
              onMove={handleMoveNode}
              selectedNode={selectedNode}
              setSelectedNode={setSelectedNode}
              matchedIds={searchResult.matchedIds}
              search={search}
            />
          )}
        </div>

        <div className="details-panel page-card">
          {selectedNode ? (
            <>
              <div className="details-title">
                <Building2 size={17} />
                <h3>Node Details</h3>
              </div>
              <div className="details-grid">
                <div>
                  <span>Name</span>
                  <strong>{selectedNode.name}</strong>
                </div>
                <div>
                  <span>Type</span>
                  <strong>{selectedNode.type}</strong>
                </div>
                <div>
                  <span>Depth</span>
                  <strong>{selectedNode.depth}</strong>
                </div>
                <div>
                  <span>Path</span>
                  <strong>{selectedNode.path}</strong>
                </div>
                <div>
                  <span>Children</span>
                  <strong>{selectedNode.childrenIds?.length || (selectedNode.children || []).length}</strong>
                </div>
                <div>
                  <span>ID</span>
                  <strong>{selectedNode.id}</strong>
                </div>
              </div>
            </>
          ) : (
            <div className="empty-selection">
              Select a node to view details.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default UniversityStructurePage;
