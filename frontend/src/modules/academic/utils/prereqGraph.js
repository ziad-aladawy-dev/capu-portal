// Client-side helpers over the catalog prerequisite edge list
// ([{ courseId, prerequisiteCourseId }]) returned by GET /courses/prerequisites.
// The server re-validates on write; these exist for instant inline UX.

// Map courseId -> [prerequisiteCourseId]
export function buildPrereqMap(pairs) {
  const map = new Map();
  for (const p of pairs || []) {
    if (!map.has(p.courseId)) map.set(p.courseId, []);
    map.get(p.courseId).push(p.prerequisiteCourseId);
  }
  return map;
}

/**
 * Would setting `proposedIds` as the prerequisites of `courseId` close a cycle?
 * Walks prerequisite chains from each proposed id (using every OTHER course's
 * current edges) looking for a path back to courseId. Returns the offending
 * path as an array of course ids ([candidate, ..., courseId]) or null.
 */
export function findCyclePath(pairs, courseId, proposedIds) {
  const adjacency = new Map();
  for (const p of pairs || []) {
    if (p.courseId === courseId) continue; // replaced by the proposal
    if (!adjacency.has(p.courseId)) adjacency.set(p.courseId, []);
    adjacency.get(p.courseId).push(p.prerequisiteCourseId);
  }

  for (const start of proposedIds || []) {
    if (start === courseId) return [courseId];
    const path = dfsTo(adjacency, start, courseId, new Set());
    if (path) return path;
  }
  return null;
}

function dfsTo(adjacency, node, target, visited) {
  if (node === target) return [node];
  if (visited.has(node)) return null;
  visited.add(node);
  for (const next of adjacency.get(node) || []) {
    const sub = dfsTo(adjacency, next, target, visited);
    if (sub) return [node, ...sub];
  }
  return null;
}
