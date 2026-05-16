# Authorization Scalability Roadmap

Risk: RBAC/ABAC row explosion. Per-user permission resolution joins
`StaffRoles` + `RolePermissions` + `StaffPermissions` + expands action
levels into a `HashSet<string>`. Grows linearly with users × roles ×
scope-rows × services. At 10k staff / 5 roles avg / 50 services /
3 scope dimensions, low-millions of rows touched per lookup before
cache hit.

## Already shipped

| Mitigation | Where | Effect |
| --- | --- | --- |
| **Per-user version-stamped lookup cache** | `PermissionManagementService.GetPermissionLookupAsync` + `ICacheService` | First lookup hits DB, subsequent reads within TTL serve from Memory/Redis. Version-stamp rotation orphans every cached entry on a single write — no key enumeration, instant cross-instance invalidation through shared cache. |
| **Filter-column DB indexes** *(PR-8)* | `IX_StaffRoles_StaffId_Year_Semester`, `IX_StaffRoles_StructureNodePath`, same on `StaffPermissions` | Cold cache miss goes from full table scan to indexed seek + range scan on `Path`. The `StartsWith` filter benefits from the B-tree prefix ordering. |
| **Resilient Redis cache** *(PR-3)* | `RedisCacheService` try/catch | Cache outage degrades to "no cache" not 500. Removes the cache as a hard dependency. |

## Not yet shipped — ordered by ROI

### 1. Permission snapshot table

Materialise the resolved `HashSet<string>` to a `UserPermissionSnapshots`
table keyed by `(UserId, ScopeFingerprint)`. Rebuilt on every grant
change (write side) and read in a single row lookup at request time.

- Replaces the join+expand pipeline on cold-cache reads.
- Survives process restarts and Redis outages — durable cache.
- Adds write amplification: each `CreateAssignmentAsync` /
  `UpdateAssignmentAsync` must enumerate the user's distinct scopes
  and rewrite each snapshot row. For typical admin users with few
  scopes this is cheap; for power users it's bounded by their active
  scope count.

Trigger to ship: cold-miss latency p99 > 50ms in prod (currently
sub-ms on test data). Or any reported "permissions feel slow after
restart" complaints.

### 2. Bitmask action storage

Replace the resolved `HashSet<string>` (e.g. `permissions.users.View`,
`permissions.users.Insert`, ...) with a `Dictionary<(Module, Resource), ActionLevel>`
keyed by the resource and storing the max granted level as a flag-able
int. `Contains("permissions.users.View")` becomes
`dict[("permissions","users")] >= ActionLevel.View`.

- 5× smaller cached payload (one entry per resource instead of one per
  action), faster `.Contains` (dict lookup + int compare vs string
  hash), smaller Redis bandwidth.
- Breaking change to `IPermissionManagementService.GetPermissionLookupAsync`
  return type — touches `PermissionHandler` consumer.
- Doesn't reduce DB rows or query cost. Pure resolution-time + cache-
  payload optimisation.

Trigger to ship: cache memory pressure or sub-millisecond p99 targets.

### 3. Policy engine (e.g. Cedar / OPA)

Replace the in-process RBAC+ABAC evaluator with a dedicated policy
service. Permissions become policies expressed in the policy language;
the engine compiles + indexes them once and answers queries in O(log n).

- Removes the "row explosion" framing entirely: policies are not stored
  per-(user × scope) but as rules that match by predicate.
- Externalises a critical path — new dep, new SLO, new failure mode.
- ABAC constraints (`isClosed`, time windows) become first-class.

Trigger to ship: governance / audit requirements that the current
RBAC+ABAC scheme can't articulate (e.g. "managers can edit only their
own department's records during business hours"), OR the row count
becomes genuinely unmanageable.

### 4. Graph-based resolution cache

Build a directed graph: user → roles → role-permissions → resources,
keyed by the structural node hierarchy. Lookups walk the graph from
the user node with the request scope as a filter.

- Avoids the per-request join: the graph is the index.
- Memory-heavy; recomputed on assignment changes.
- Best when reads dominate writes by 100:1+.

Trigger to ship: most likely irrelevant given the cache + snapshot
patterns above already serve the read-heavy case.

## When to revisit

- p99 authorization latency > 50ms — ship #1.
- Cache memory / Redis bandwidth becomes a budget concern — ship #2.
- New auth requirements that don't fit RBAC+ABAC — evaluate #3.

Don't ship any of these speculatively. The current stack scales to
mid-five-figure users on commodity hardware.
