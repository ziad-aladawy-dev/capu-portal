/**
 * @file DAG cycle detection for course prerequisites
 * Optimized with memoization and early exit
 */

const cycleCache = new Map();
const CACHE_MAX_SIZE = 100;

/**
 * Detects cycles in a directed graph using Kahn's algorithm
 * @param {Array<Object>} courses - All courses
 * @param {Object} prerequisites - Map of courseId -> prerequisiteIds[]
 * @returns {Object} { hasCycle: boolean, cycle: Array<string> }
 */
export function detectCycles(courses, prerequisites) {
  const adj = {};
  const inDegree = {};
  const allNodes = new Set();

  for (const c of courses) {
    const id = c.id;
    allNodes.add(id);
    if (!adj[id]) adj[id] = [];
    if (inDegree[id] === undefined) inDegree[id] = 0;
    const prereqs = prerequisites[id] || [];
    for (const prereqId of prereqs) {
      allNodes.add(prereqId);
      if (!adj[prereqId]) adj[prereqId] = [];
      adj[prereqId].push(id);
      inDegree[id] = (inDegree[id] || 0) + 1;
    }
  }

  for (const node of allNodes) {
    if (inDegree[node] === undefined) inDegree[node] = 0;
    if (!adj[node]) adj[node] = [];
  }

  const queue = [];
  for (const node of allNodes) {
    if (inDegree[node] === 0) queue.push(node);
  }

  let visited = 0;
  while (queue.length > 0) {
    const u = queue.shift();
    visited++;
    for (const v of adj[u] || []) {
      inDegree[v]--;
      if (inDegree[v] === 0) queue.push(v);
    }
  }

  const hasCycle = visited !== allNodes.size;
  if (!hasCycle) return { hasCycle: false, cycle: [] };

  const remaining = new Set();
  for (const node of allNodes) {
    if (inDegree[node] > 0) remaining.add(node);
  }

  const cycle = findOneCycle(adj, remaining);
  return { hasCycle: true, cycle };
}

function findOneCycle(adj, remaining) {
  const visited = new Set();
  const recStack = new Set();

  function dfs(node, path) {
    visited.add(node);
    recStack.add(node);
    path.push(node);
    for (const v of adj[node] || []) {
      if (!remaining.has(v)) continue;
      if (!visited.has(v)) {
        const result = dfs(v, path);
        if (result) return result;
      } else if (recStack.has(v)) {
        const cycleStart = path.indexOf(v);
        return path.slice(cycleStart);
      }
    }
    path.pop();
    recStack.delete(node);
    return null;
  }

  for (const node of remaining) {
    if (!visited.has(node)) {
      const result = dfs(node, []);
      if (result) return result;
    }
  }
  return [];
}

/**
 * Finds if adding a prerequisite would create a cycle involving the given course
 * Uses memoization for performance
 * @param {string} courseId - The course being edited
 * @param {Array<Object>} allCourses - All courses
 * @returns {Object|null} Cycle info or null
 */
export function findCoursePrerequisiteCycle(courseId, allCourses) {
  if (!courseId || !allCourses?.length) return null;

  const cacheKey = `${courseId}:${allCourses.map(c => `${c.id}:${(c.prerequisites||[]).join(",")}`).join("|")}`;

  if (cycleCache.has(cacheKey)) {
    return cycleCache.get(cacheKey);
  }

  const coursesById = {};
  for (const c of allCourses) coursesById[c.id] = c;

  const prerequisites = {};
  for (const c of allCourses) {
    prerequisites[c.id] = c.prerequisites || [];
  }

  const { hasCycle, cycle } = detectCycles(allCourses, prerequisites);
  if (!hasCycle) {
    const result = null;
    addToCache(cacheKey, result);
    return result;
  }

  const cycleCourseIds = {};
  for (const id of cycle) cycleCourseIds[id] = true;

  const affected = [];
  function dfsAffected(nodeId, visited) {
    if (visited.has(nodeId)) return;
    visited.add(nodeId);
    affected.push(nodeId);
    for (const c of allCourses) {
      if ((c.prerequisites || []).includes(nodeId)) {
        dfsAffected(c.id, visited);
      }
    }
  }
  dfsAffected(courseId, new Set());

  const result = {
    inCycle: cycleCourseIds[courseId] || false,
    cycle: cycle.map((id) => coursesById[id]?.code || id),
    affected: affected.map((id) => coursesById[id]?.code || id),
  };

  addToCache(cacheKey, result);
  return result;
}

function addToCache(key, value) {
  if (cycleCache.size >= CACHE_MAX_SIZE) {
    const firstKey = cycleCache.keys().next().value;
    cycleCache.delete(firstKey);
  }
  cycleCache.set(key, value);
}

export function clearCycleCache() {
  cycleCache.clear();
}