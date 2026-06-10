export function detectCycles(courses, prerequisites) {
  const adj = {};
  const inDegree = {};
  const allNodes = new Set();

  for (const c of courses) {
    const id = c.id;
    allNodes.add(id);
    if (!adj[id]) adj[id] = [];
    if (inDegree[id] === undefined) inDegree[id] = 0;
    const prereqs = (prerequisites[id] || []);
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
    for (const v of (adj[u] || [])) {
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
    for (const v of (adj[node] || [])) {
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

export function findCoursePrerequisiteCycle(courseId, allCourses) {
  const coursesById = {};
  for (const c of allCourses) coursesById[c.id] = c;

  const prerequisites = {};
  for (const c of allCourses) {
    prerequisites[c.id] = c.prerequisites || [];
  }

  const { hasCycle, cycle } = detectCycles(allCourses, prerequisites);
  if (!hasCycle) return null;

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

  return {
    inCycle: cycleCourseIds[courseId] || false,
    cycle: cycle.map((id) => coursesById[id]?.code || id),
    affected: affected.map((id) => coursesById[id]?.code || id),
  };
}
