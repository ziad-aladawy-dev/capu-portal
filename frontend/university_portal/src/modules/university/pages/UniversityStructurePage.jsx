import { useMemo, useState } from "react";
import { Plus, Search, Network, Building2 } from "lucide-react";

import TreeNode from "../components/TreeNode";
import normalizedTree from "../data/normalizedTree";
import { searchTree } from "../utils/treeSearch";

import {
  buildTree,
  getAncestors,
} from "../utils/treeUtils";

import "../styles/universityStructure.css";

const UniversityStructurePage = () => {
  const [treeData, setTreeData] = useState(normalizedTree);
  const [selectedNode, setSelectedNode] = useState(null);
  const [search, setSearch] = useState("");

  const renderedTree = buildTree(treeData.rootId, treeData.nodes);

  const searchResult = searchTree(renderedTree, search);
  const matchedIds = searchResult.matchedIds;

  const addNode = (parentId) => {
    const nodeName = prompt("Node Name");
    if (!nodeName) return;

    const nodeType =
      prompt("Node Type: Faculty / Department / Program / Level", "Department") ||
      "Department";

    const newId = Date.now();

    const updatedNodes = {
      ...treeData.nodes,
    };

    const parent = updatedNodes[parentId];

    updatedNodes[newId] = {
      id: newId,
      name: nodeName,
      type: nodeType,
      parentId,
      childrenIds: [],
      path: `${parent.path}/${newId}`,
      depth: parent.depth + 1,
      order: parent.childrenIds.length + 1,
    };

    updatedNodes[parentId] = {
      ...parent,
      childrenIds: [...parent.childrenIds, newId],
    };

    setTreeData({
      ...treeData,
      nodes: updatedNodes,
    });
  };

  const deleteNode = (nodeId) => {
    if (nodeId === treeData.rootId) return;

    const updatedNodes = { ...treeData.nodes };

    const deleteRecursive = (id) => {
      const node = updatedNodes[id];
      if (!node) return;

      node.childrenIds.forEach(deleteRecursive);
      delete updatedNodes[id];
    };

    const parentId = updatedNodes[nodeId]?.parentId;

    if (parentId) {
      updatedNodes[parentId] = {
        ...updatedNodes[parentId],
        childrenIds: updatedNodes[parentId].childrenIds.filter((id) => id !== nodeId),
      };
    }

    deleteRecursive(nodeId);

    setTreeData({
      ...treeData,
      nodes: updatedNodes,
    });

    if (selectedNode?.id === nodeId) setSelectedNode(null);
  };

  const renameNode = (nodeId) => {
    const currentName = treeData.nodes[nodeId]?.name || "";
    const newName = prompt("New Name", currentName);

    if (!newName) return;

    setTreeData({
      ...treeData,
      nodes: {
        ...treeData.nodes,
        [nodeId]: {
          ...treeData.nodes[nodeId],
          name: newName,
        },
      },
    });

    if (selectedNode?.id === nodeId) {
      setSelectedNode({
        ...selectedNode,
        name: newName,
      });
    }
  };

  const moveNode = (parentId, nodeId, direction) => {
    if (!parentId) return;

    const parent = treeData.nodes[parentId];
    if (!parent) return;

    const children = [...parent.childrenIds];
    const index = children.findIndex((id) => id === nodeId);

    const targetIndex = direction === "up" ? index - 1 : index + 1;

    if (targetIndex < 0 || targetIndex >= children.length) return;

    [children[index], children[targetIndex]] = [children[targetIndex], children[index]];

    setTreeData({
      ...treeData,
      nodes: {
        ...treeData.nodes,
        [parentId]: {
          ...parent,
          childrenIds: children,
        },
      },
    });
  };

  const breadcrumb = useMemo(() => {
    if (!selectedNode) return [];

    const ancestors = getAncestors(selectedNode.id, treeData.nodes);

    return [...ancestors.map((item) => item.name), selectedNode.name];
  }, [selectedNode, treeData]);

  const stats = useMemo(() => {
    const nodes = Object.values(treeData.nodes);

    return {
      total: nodes.length,
      faculties: nodes.filter((node) => node.type === "Faculty").length,
      departments: nodes.filter((node) => node.type === "Department").length,
      programs: nodes.filter((node) => node.type === "Program").length,
    };
  }, [treeData]);

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
          <TreeNode
            node={renderedTree}
            parentId={null}
            onAdd={addNode}
            onDelete={deleteNode}
            onRename={renameNode}
            onMove={moveNode}
            selectedNode={selectedNode}
            setSelectedNode={setSelectedNode}
            matchedIds={matchedIds}
            search={search}
          />
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
                  <strong>{selectedNode.childrenIds?.length}</strong>
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
};

export default UniversityStructurePage;
