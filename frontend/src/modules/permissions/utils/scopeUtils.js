export function findNode(nodes, id) {
  for (const n of nodes || []) {
    if (n.id === id) return n;
    if (n.children) {
      const f = findNode(n.children, id);
      if (f) return f;
    }
  }
  return null;
}

// An assignment scope is "outside" the active navbar scope when it neither
// falls inside the active node's subtree nor covers it from above (ancestor
// or global). Used to warn admins before they grant access elsewhere.
export function isOutsideActiveScope(tree, activeId, nodeId) {
  if (!activeId || !nodeId) return false;
  const active = findNode(tree, activeId);
  if (active && (active.id === nodeId || findNode(active.children || [], nodeId))) return false;
  const chosen = findNode(tree, nodeId);
  if (chosen && (chosen.id === activeId || findNode(chosen.children || [], activeId))) return false;
  return true;
}
