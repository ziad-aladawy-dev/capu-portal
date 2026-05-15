export const buildTree = (
    nodeId,
    nodes
  ) => {
    const node = nodes[nodeId];
  
    return {
      ...node,
  
      children: node.childrenIds.map(
        (childId) =>
          buildTree(childId, nodes)
      ),
    };
  };
  
  export const getAncestors = (
    nodeId,
    nodes
  ) => {
    const ancestors = [];
  
    let current = nodes[nodeId];
  
    while (current?.parentId) {
      current = nodes[current.parentId];
  
      if (current) {
        ancestors.unshift(current);
      }
    }
  
    return ancestors;
  };
  
  export const isCircularMove = (
    draggedId,
    targetId,
    nodes
  ) => {
    if (draggedId === targetId)
      return true;
  
    const target = nodes[targetId];
  
    if (!target) return false;
  
    return target.path
      .split('/')
      .includes(String(draggedId));
  };
  
  export const updateNodePath = (
    nodeId,
    nodes
  ) => {
    const node = nodes[nodeId];
  
    if (!node) return;
  
    if (!node.parentId) {
      node.path = `${node.id}`;
      node.depth = 0;
    } else {
      const parent = nodes[node.parentId];
  
      node.path =
        `${parent.path}/${node.id}`;
  
      node.depth = parent.depth + 1;
    }
  
    node.childrenIds.forEach((childId) =>
      updateNodePath(childId, nodes)
    );
  };