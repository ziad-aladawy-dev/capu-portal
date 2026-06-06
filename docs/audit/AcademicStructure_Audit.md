# Academic Structure Module — Verification Audit

**Doc:** `docs/AcademicStructure_Model.md`
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Model entire university org hierarchy as a single recursive self-referencing `StructureNode` entity with an enum `Type` discriminator plus materialized `Path`/`Depth`. No per-type tables (`Faculties`, `Departments`, `Programs`). Adding a node kind = enum value + rules entry, no migration.
- **Responsibilities (bounded):** shape, ordering, soft-delete, traversal, type-relationship validation. Agnostic of courses, plans, enrollments, semesters, permissions.
- **Assumptions:**
  - Single self-referencing FK (`ParentId` nullable), forest of trees.
  - Materialized `Path` = `/{rootId}/{...}/{selfId}` (GUID segments, leading `/`, no trailing `/`); `Depth` 0-based.
  - Validation external to entity, in `StructureNodeRules.AllowedChildren`.
  - Soft-delete only (`IsDeleted` + EF global query filter); cascade by path prefix in code, FK `Restrict`.
  - `IsActive` distinct from `IsDeleted`; move under inactive parent blocked.
  - Move rewrites `Path`/`Depth` for moved node + every descendant in one batch.
- **Invariants (doc §6):** single-parent; root only `University` (policy); parent→child pair must be in rules on create AND move; no cycles (descendant-or-self reject + self-move reject); move under inactive blocked; `Depth = segments(Path) − 1`; `Order` sibling-local dense `0..n−1`; soft-deleted invisible to all queries; `Path`/`Depth` service-written, never caller-trusted.
- **Expected flow:** Create builds `path = {parent.Path}/{newId}` (root parent path = `""`). Move validates → rewrites subtree → repairs permission path prefix → invalidates permission cache. Delete = recursive soft-delete by path prefix.

## Step 2 — Implementation Files

- `Core.Domain/UniversityStructure/StructureNode.cs` (entity)
- `Core.Domain/UniversityStructure/Enums/StructureNodeType.cs` (enum)
- `Core.Domain/Common/BaseEntity.cs` (`IsDeleted`, audit cols)
- `Core.Application/UniversityStructure/UniversityStructureService.cs` (Create/Update/Delete/Move/Reorder/Tree/Breadcrumb/Ancestors/SubTree)
- `Core.Application/UniversityStructure/StructureLookupService.cs` (typed lookups)
- `Core.Application/UniversityStructure/UniversityStructureRules/{StructureNodeRules, StructureNodeValidator}.cs`
- `Core.Infrastructure/Persistence/Configurations/StructureNodeConfiguration.cs`
- `Core.Infrastructure/Repositories/StructureNodeRepository.cs`
- `Core.Abstractions/UniversityStructure/{IUniversityStructureService, IStructureLookupService, DTOs/*}.cs`

## Step 3 — Verification Table

| Assumption | Expected | Actual Implementation | Match |
| --- | --- | --- | --- |
| Single self-referencing entity, no per-type tables | one `StructureNode` table, enum `Type` | `StructureNode` self-ref via `Parent`/`Children`+`ParentId`; one table `StructureNodes` (`StructureNode.cs:6-25`, `StructureNodeConfiguration.cs:11`) | ✅ |
| `ParentId` nullable scalar, single parent | 0 or 1 parent | `Guid? ParentId` (`StructureNode.cs:12`) | ✅ |
| Materialized `Path` `/{...}/{self}`, GUID segments | leading `/`, no trailing | Create: `path=""` for root, else `parent.Path`; `node.Path = $"{path}/{node.Id}"` (`UniversityStructureService.cs:120,143,157`) | ✅ |
| `Depth` 0-based, root=0 | derived | root `depth=0`; child `parent.Depth+1` (`:107,141`) | ✅ |
| `Path` `nvarchar(4000)`, required, indexed | schema | `HasMaxLength(4000)` + `IsRequired` + `HasIndex(Path)` (`StructureNodeConfiguration.cs:25-27,45`) | ✅ |
| Indexes on ParentId/Path/Depth/Type/Order/IsDeleted | all indexed | all 6 `HasIndex` present (`:43-53`) | ✅ |
| FK `OnDelete=Restrict`, no cascade | restrict | `OnDelete(DeleteBehavior.Restrict)` (`:41`) | ✅ |
| EF global query filter `!IsDeleted` | deleted invisible | `HasQueryFilter(x => !x.IsDeleted)` (`:55`); `IsDeleted` on `BaseEntity.cs:11` | ✅ |
| Validation external, dictionary-driven | rules outside entity | `StructureNodeRules.AllowedChildren` dict + `StructureNodeValidator` (`StructureNodeRules.cs:7-60`, `StructureNodeValidator.cs`) | ✅ |
| Allowed pairs: Faculty→{System,Program}, Level→{Level,Specialization}, Specialization→{Level}, Program→{Level,Specialization} | per doc §7 | exact match (`StructureNodeRules.cs:18-59`) | ✅ |
| Root only `University` | policy | `CanBeRoot => type==University` (`StructureNodeValidator.cs:20-23`); enforced on create (`UniversityStructureService.cs:113-118`) | ✅ |
| Type pair validated on create AND move | both paths | create `:130-139`; move `:236-246` | ✅ |
| No cycles: reject parent path ⊃ node path + self-move | descendant-or-self | self-move `:209`; `newParent.Path.StartsWith(node.Path)` `:248` | ✅ |
| Move under inactive parent blocked | reject | `if(!newParent.IsActive) throw` (`:230-234`) | ✅ |
| Move rewrites Path/Depth for node + descendants in one batch | bulk update | node rewrite `:275-285`; descendants loop `:294-313`; `UpdateRangeAsync` `:317` | ✅ |
| Soft-delete cascade by path prefix | code, not DB | `RecursiveSoftDeleteAsync` flags `Path.StartsWith(path) && !IsDeleted` (`StructureNodeRepository.cs:75-91`) | ✅ |
| `Order` normalized dense `0..n-1` | reorder | `ReorderNodeAsync` reinserts + renumbers `i` (`UniversityStructureService.cs:489-503`) | ✅ |
| Path-based breadcrumb/ancestors | parse Path to IDs | `Path.Split('/').Select(Guid.Parse)` (`:375-382,444-454`) | ✅ |
| Move repairs permission path prefix + invalidates cache | wired | `RepairPermissionPathPrefixAsync` + `InvalidateAllAsync` on path change (`:323-330`; repo `:167-198`) | ✅ |
| Typed lookups (`GetByType`, `GetChildrenByType`) | service-exposed | `StructureLookupService.cs:22-61` | ✅ |
| `Department` is an open/optional kind | data-only | enum `Department=6` exists but absent from `AllowedChildren` (not key, not child) and not root-eligible → unreachable (`StructureNodeType.cs:22-23`, `StructureNodeRules.cs`) | ⚠️ (Divergence 1) |
| `descendants[0]` is the moved node | ordered fetch | `GetDescendantsAsync` unordered (`StructureNodeRepository.cs:99-106`); `descendants[0]=node` assumes index 0 (`UniversityStructureService.cs:315`) | ⚠️ (Divergence 2) |
| Path rewrite is prefix-anchored | substring from prefix | `item.Path.Replace(oldPath,newPath)` — unanchored `String.Replace` (`:300-302`) | ✅ (safe by GUID uniqueness; Note) |

## Step 4 — Divergence Analysis

### Divergence 1 — `Department` enum value is unreachable (orphan kind)
- **Model:** "kinds of node" are open; each lives as an enum value + a `StructureNodeRules.AllowedChildren` entry. Doc deliberately de-emphasizes Department (§8.1 "A Department may not exist at all"), but treats every enum type as a creatable kind.
- **Implementation:** `StructureNodeType.Department = 6` exists (`StructureNodeType.cs:22-23`) but appears **nowhere** in `StructureNodeRules.AllowedChildren` — neither as a parent key nor as an allowed child of any type — and `CanBeRoot` allows only `University`. Therefore no `Department` node can ever be created: as a root it fails `CanBeRoot`; under any parent it fails `IsValidChild`.
- **Evidence:** `StructureNodeType.cs:22-23`; `StructureNodeRules.cs:7-60`; `StructureNodeValidator.cs:7-23`.
- **Impact:** Documentation/Low. Latent trap: a client/dev passing `Type=Department` to `CreateNodeAsync` gets a generic `"{Department} cannot be added under {parent}"` exception, not a clear "type not supported". Dead enum member with no rules wiring.
- **Severity:** Low
- **Notes:** Consistent with the doc's "no guaranteed Department" stance, but the dangling enum value contradicts the "enum value + rules entry = a kind" model. Either remove the enum value or add a rules entry.

### Divergence 2 — `MoveNodeAsync` assumes moved node is at `descendants[0]`
- **Model:** Move rewrites the moved node and every descendant in one batch (doc §4).
- **Implementation:** `GetDescendantsAsync(oldPath)` returns an **unordered** list (`StructureNodeRepository.cs:99-106`, no `OrderBy`). The loop (`:294-313`) skips the element with `item.Id == node.Id` and rewrites the rest, then unconditionally does `descendants[0] = node` (`UniversityStructureService.cs:315`), assuming the moved node sits at index 0.
- **Evidence:** `UniversityStructureService.cs:290-318`; `StructureNodeRepository.cs:99-106`.
- **Impact:** Operational/Low — **no verified data loss**. EF identity map: `GetByIdAsync(nodeId)` (`:202`) and the descendants query return the **same tracked instance** for the moved node, and all descendant entities are tracked from the query. Every in-place `Path`/`Depth` mutation persists on `SaveChanges` regardless of list membership, so overwriting index 0 cannot drop a descendant's update. The line is redundant/fragile, not incorrect under current tracking. Risk surfaces only if the fetch ever becomes no-tracking.
- **Severity:** Low
- **Notes:** Fragile-by-accident; relies on EF change tracking rather than the list being authoritative.

## Step 5 — Hidden-Logic Review

- **Unanchored path rewrite:** `item.Path.Replace(oldPath, newPath)` (`UniversityStructureService.cs:300`) is `String.Replace` (all occurrences), not a prefix-anchored substring. Safe in practice: `oldPath` is a full GUID-segment prefix (`/{guid}/...`), GUID segments cannot prefix-collide or repeat mid-path, so exactly one occurrence (the prefix) is replaced. **No verified issue** — doc §4 explicitly relies on GUID non-collision.
- **`RecursiveSoftDeleteAsync` / `GetDescendants*` prefix match:** `Path.StartsWith(path)` with full-GUID prefix; sibling roots have distinct GUIDs → no false cascade. **No verified issue.**
- **Redundant `IsDeleted` re-check in Move:** `node.IsDeleted` / `newParent.IsDeleted` checked manually (`:204,224`) even though the EF global filter already excludes deleted rows from `GetByIdAsync`. Harmless defensive redundancy. **No verified issue.**
- **Raw `throw new Exception(...)`:** Create/Update/Delete/Move/Reorder throw base `Exception` with English literals (`:116,128,137,173,189,...`), bypassing the platform's localized-exception pattern. Maintainability/localization smell, not a correctness bug. **No verified security issue.**
- **`GetByIdAsync`/`GetTreeAsync` re-fetch all nodes** (`:36,72`) — matches doc §10's stated O(n) cost model. Not a defect; doc flags it as a known scale ceiling.
- **No hard-delete path exists** — matches invariant #8. **No verified issue.**

## Step 6 — Flow Verification

### Create node
- **Expected:** root → `CanBeRoot` gate, `path=/{id}`, depth 0; child → parent type-pair validated, `path={parent.Path}/{id}`, depth `parent.Depth+1`.
- **Actual:** `UniversityStructureService.cs:105-164` — exact match.
- **Match:** YES.

### Move subtree
- **Expected:** validate (self, exists, active, type-pair, no-cycle) → rewrite node + descendants `Path`/`Depth` → repair permission prefix → invalidate cache.
- **Actual:** `:197-331` — all gates present; subtree rewrite present; `RepairPermissionPathPrefixAsync` + `InvalidateAllAsync` on path change. Matches, with Divergence 2 fragility (no data impact).
- **Match:** YES (functional).

### Soft delete
- **Expected:** flag every row whose `Path` starts with deleted node's path; FK `Restrict`, cascade in code.
- **Actual:** `DeleteNodeAsync` → `RecursiveSoftDeleteAsync(path)` (`:185-195`; repo `:75-91`). Matches.
- **Match:** YES.

### Reorder siblings
- **Expected:** dense `0..n-1` normalization.
- **Actual:** `:477-508` — remove self, clamp target, reinsert, renumber. Matches.
- **Match:** YES.

### Traversal (tree/breadcrumb/ancestors/subtree/lookups)
- **Expected:** Path-based; in-memory dictionary join for trees; typed lookups via `IStructureLookupService`.
- **Actual:** `GetTreeAsync`/`GetSubTreeAsync` dict join (`:34-68,398-435`); breadcrumb/ancestors parse `Path` (`:368-475`); lookups scan-all + filter (`StructureLookupService.cs`). Matches doc §10.
- **Match:** YES.

## Step 7 — Final Verdict

### Scores
- Architecture Match: 9/10
- Logic Correctness: 9/10
- Operational Safety: 8/10
- Maintainability: 7/10

### Confirmed Issues
1. `StructureNodeType.Department` (=6) is unreachable — absent from `AllowedChildren` and not root-eligible; dead enum member, latent trap — Low.
2. `MoveNodeAsync` assumes moved node at `descendants[0]` over an unordered fetch; correct only by virtue of EF identity-map tracking — Low, fragile.
3. Service throws base `Exception` with hardcoded English strings, bypassing the platform's localized-exception convention — Low, maintainability/localization.

### Model Violations
- None material. The implementation honors every structural invariant (single-parent, path-as-authority, soft-delete-by-prefix, type-pair on create+move, no-cycle, inactive-parent block, dense ordering, path/depth service-owned).

### False Positives
- "Hierarchy as data, not schema" — upheld (single table, enum + rules).
- "Cascade is policy-driven, FK `Restrict`" — upheld (`Restrict` + code prefix cascade).
- "Move rewrites whole subtree in one batch" — upheld.
- "Path is the authority for ancestry" — upheld (breadcrumb/ancestors parse Path).
- "Add a kind = enum + rules entry, no migration" — upheld (modulo Divergence 1's orphan).

### Findings Summary
- **Matches:** Self-referencing single-entity model, materialized path/depth semantics, schema config (indexes/Restrict/query filter/`nvarchar(4000)`), allowed-children rules table, root policy, create/move/delete/reorder/traversal flows, permission-path repair + cache invalidation on move.
- **Does not match:** `Department` enum has no rules wiring (unreachable); minor move-batch index fragility; non-localized raw exceptions.
- **Needs attention:** Resolve the orphan `Department` enum (remove or wire into rules) and harden `MoveNodeAsync` to not depend on list index 0. The architecture matches the documented model strongly; no security or data-integrity defect found.
