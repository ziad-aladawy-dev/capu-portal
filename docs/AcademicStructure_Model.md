# Academic Structure Module — AI Context Model

> Compressed architectural context for AI agents. Do **not** treat this as user-facing documentation. It encodes design intent, invariants, and anti-assumptions so future AI sessions reason about the module without re-scanning `src/2.Core/.../UniversityStructure/**`.
>
> Sources read:
> [StructureNode.cs](src/2.Core/CapitalUniversity.Core.Domain/UniversityStructure/StructureNode.cs),
> [StructureNodeType.cs](src/2.Core/CapitalUniversity.Core.Domain/UniversityStructure/Enums/StructureNodeType.cs),
> [StructureNodeRules.cs](src/2.Core/CapitalUniversity.Core.Application/UniversityStructure/UniversityStructureRules/StructureNodeRules.cs),
> [StructureNodeValidator.cs](src/2.Core/CapitalUniversity.Core.Application/UniversityStructure/UniversityStructureRules/StructureNodeValidator.cs),
> [UniversityStructureService.cs](src/2.Core/CapitalUniversity.Core.Application/UniversityStructure/UniversityStructureService.cs),
> [StructureLookupService.cs](src/2.Core/CapitalUniversity.Core.Application/UniversityStructure/StructureLookupService.cs),
> [StructureNodeConfiguration.cs](src/2.Core/CapitalUniversity.Core.Infrastructure/Persistence/Configurations/StructureNodeConfiguration.cs),
> [IStructureNodeRepository.cs](src/2.Core/CapitalUniversity.Core.Abstractions/Repositories/IStructureNodeRepository.cs),
> DTOs under `src/2.Core/CapitalUniversity.Core.Abstractions/UniversityStructure/DTOs/`.

---

## 1. Executive Summary

The Academic Structure module models the entire university organizational hierarchy as a **single recursive, self-referencing entity** (`StructureNode`) — *not* as a set of dedicated tables (`Universities`, `Faculties`, `Departments`, `Programs`, …). A node carries a **semantic `Type` discriminator** (enum) plus **materialized path / depth columns** so that any depth, any reshuffle, and any future type addition is a data-only change rather than a schema migration.

The module's job is bounded to: **shape, ordering, soft-delete, traversal, and type-relationship validation**. It is intentionally agnostic of: courses, plans, enrollments, semesters, permissions scoping, or any business semantic above "this node exists at this position in some hierarchy".

---

## 2. Core Design Philosophy

- **Hierarchy as data, not schema.** Adding "Institute", "Track", "Cohort", or any new academic level is a `StructureNodeType` enum entry + a row in `StructureNodeRules.AllowedChildren`. **No table is added. No migration is required.**
- **Single source of structural truth.** All other modules reference the hierarchy by `StructureNodeId` (a `Guid`), never by `FacultyId`, `DepartmentId`, etc. Those concepts do not exist as foreign keys anywhere else in the system.
- **Materialized path is the primary traversal index.** `Path` (`/{rootId}/{childId}/{...}/{self}`) + `Depth` are denormalized to make ancestor / descendant / subtree queries trivial SQL `LIKE` lookups rather than recursive CTEs.
- **The "tree" is a directed self-referencing graph constrained to a forest of trees.** A node has at most one parent (`ParentId` nullable). Multiple roots are allowed by the schema (current rule restricts roots to `University`, but this is application-layer policy, not a schema invariant).
- **Validation lives outside the entity.** `StructureNode` is a pure data shape; all "what can sit under what" rules live in `StructureNodeRules` so policy can mutate without touching the model.

---

## 3. Architectural Intent

| Goal | How it's expressed |
|---|---|
| Runtime extensibility of the academic hierarchy | Self-referencing FK + enum `Type` + dictionary-driven validator |
| No schema churn when org structure evolves | All "kinds of node" share one row shape; only `Type` differs |
| Cheap traversal | `Path` (`nvarchar(4000)`) + `Depth` (`int`), both indexed |
| Reorderable, movable subtrees | `MoveNodeAsync` rewrites `Path`/`Depth` for the moved node and **every descendant** in one batch update |
| Stable ordinals among siblings | `Order` column, normalized to `0..n-1` by `ReorderNodeAsync` |
| Reversible deletion | `IsDeleted` flag + global EF query filter; cascade via path prefix in `RecursiveSoftDeleteAsync` |
| Inactive-but-not-deleted state | `IsActive` flag distinct from `IsDeleted`; moves under inactive parents are blocked |

---

## 4. Database Behavioral Model

### Table: `StructureNodes`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Application-generated GUID |
| `Name` | `nvarchar(max)`, required | Display name; not unique by design |
| `Type` | `int` (enum), required | Indexed |
| `ParentId` | `uniqueidentifier` nullable | FK → `StructureNodes.Id`, `OnDelete=Restrict`, indexed |
| `Order` | `int`, default `0` | Sibling ordinal; indexed |
| `Path` | `nvarchar(4000)`, required | Materialized path: `/{rootId}/{...}/{self}`; indexed |
| `Depth` | `int`, required | 0-based; root nodes have `Depth=0`; indexed |
| `IsActive` | `bit`, default `true` | Soft-disable, distinct from deletion |
| `IsDeleted` | `bit`, default `false` | EF global query filter excludes deleted rows; indexed |
| `CreatedAt`/`UpdatedAt`/audit | inherited from `BaseEntity` | |

### Behavioral guarantees

- **Soft delete only.** No hard delete exists in the service. `RecursiveSoftDeleteAsync(path)` flags every row whose `Path` starts with the deleted node's path.
- **EF `HasQueryFilter(!IsDeleted)`** applies to every query — code outside the module sees a deleted node *as if it does not exist*.
- **Cascade is policy-driven, not schema-driven.** SQL FK is `Restrict`; recursive soft-delete is performed in code by path prefix. There is **no `ON DELETE CASCADE`** for parent → children.
- **`Path` is the authority for ancestry**, not `ParentId`. Move and breadcrumb operations parse `Path` to derive ancestor IDs.

### Path encoding (critical)

- Format: `/{guidA}/{guidB}/.../{selfId}` — every segment is a GUID, leading `/`, no trailing `/`.
- `CreateNodeAsync` builds path as `{parent.Path}/{newId}`. For a root, parent path is the empty string, so the root's path becomes `/{rootId}`.
- `MoveNodeAsync` performs string replacement (`oldPath` → `newPath`) on every descendant. This is safe because GUIDs cannot prefix-collide.
- **Never depend on humanly-meaningful segments in `Path`.** It is opaque other than as a structural index.

---

## 5. Entity Interaction Map

```
                  StructureNode (self)
                       │
                       │ ParentId (nullable FK)
                       ▼
                  StructureNode (parent)

External references (all by ID, no navigation):
   AcademicPlan.StructureNodeId  ───►  StructureNode.Id
   <future module>.StructureNodeId ──►  StructureNode.Id
```

- The entity has exactly **one relationship: itself**. Every external module that needs an organizational anchor stores `Guid StructureNodeId` and resolves through the service, never via EF navigation.
- `Parent` / `Children` navigation properties exist on the entity but are **only used inside this module** (assembled in-memory by services from a single `GetAllAsync` plus a dictionary join — see `UniversityStructureService.GetTreeAsync`). Other modules must not load nodes via EF Includes from this entity.

---

## 6. Invariants

These are guaranteed by the module. AI agents may rely on them.

1. **Single-parent invariant.** `ParentId` is scalar; a node has 0 or 1 parents.
2. **Root invariant (application-layer).** A root (`ParentId == null`) is currently only valid for `Type = University` (`StructureNodeValidator.CanBeRoot`). This is policy, not schema.
3. **Parent–child type pair invariant.** Every parent/child pair must satisfy `StructureNodeRules.AllowedChildren[parent.Type].Contains(child.Type)`. Enforced on create *and* on move.
4. **No cycles.** Move rejects any operation where the proposed parent's `Path` starts with the moved node's `Path` (descendant-or-self check). Move into self is also rejected explicitly.
5. **Move under inactive parent is blocked.** `newParent.IsActive == false` rejects the move.
6. **`Depth = number_of_segments(Path) - 1`.** Maintained on create, move, and descendant rewrite.
7. **Order is sibling-local.** Reorder normalizes to dense `0..n-1` across siblings of the same parent.
8. **Deletion is irreversible-from-API but reversible-in-DB.** No undelete endpoint exists; rows remain.
9. **Soft-deleted nodes are invisible to all queries.** Including parent/children navigation, breadcrumb, lookup, and tree assembly.
10. **`Path` and `Depth` are derived columns.** They are *always* written by the service; never trust caller-supplied values for these fields.

---

## 7. Allowed Flexibility (Intentionally Loose)

- **Number of node types is open.** Adding a new `StructureNodeType` enum value + a `StructureNodeRules` entry is the entire change.
- **Hierarchy depth is not bounded.** The schema permits arbitrary depth (limited only by `Path` `nvarchar(4000)` — ~110 GUID segments).
- **Hierarchy shape is not a strict pyramid.**
  - `Faculty` may parent both `System` and `Program` directly.
  - `Level` may parent another `Level` (recursive nesting of levels, e.g. Year → Term).
  - `Specialization` may sit under `Program` *or* under `Level`, and may itself host a `Level`.
  - **Therefore: the same `StructureNodeType` can appear at multiple depths and under multiple parent types.** Depth ≠ Type.
- **Roots are forest-capable.** The schema allows multiple `ParentId IS NULL` rows; current policy restricts roots to `University`, but a multi-university tenancy could relax this without schema change.
- **Names are not unique.** Two faculties named "Engineering" are valid.
- **Ordering is freely reassignable.**

---

## 8. Dangerous Assumptions To Avoid

> Future modules and AI agents **must not** assume any of these. They will break:

1. **"Faculty → Department → Program → Level" is NOT a guaranteed path.** This was the old hardcoded shape; the new model deliberately abandons it. A `Program` may be a direct child of `Faculty` with no `Department` in between. A `Department` may not exist at all in a given university tree.
2. **Leaf node ≠ a specific semantic type.** Do not assume "leaves are always `Level`" or "leaves are always `Specialization`". Any type may currently be a leaf, and the set of leaves shifts as admins add children.
3. **Depth ≠ semantic meaning.** `Depth = 2` is not "Department". Two trees may legitimately place `Program` at different depths.
4. **A type's parent is not unique.** `Program` may be a child of `Faculty` OR `System`. `Level` may be a child of `Program`, `Level`, or `Specialization`. Never assume a single parent type when traversing upward — walk `Path` instead.
5. **Do not query for a type by name string.** The enum integer is the contract; `StructureLookupService` exposes typed lookups.
6. **Do not introduce a dedicated `Faculty` / `Department` / `Program` table.** Doing so re-introduces the rigidity the module was designed to remove and creates a second source of truth for the hierarchy.
7. **Do not navigate cross-module via EF.** External entities (e.g. `AcademicPlan`) reference `StructureNodeId` *without* an EF navigation property. Adding one couples modules and breaks the modularity rule.
8. **Do not hard-delete.** The service exposes no hard delete; assume callers, audit, and downstream caches rely on `IsDeleted` semantics.
9. **Do not assume `Path` segments are human-readable or stable codes.** They are GUIDs.
10. **Do not assume the same node ID always sits at the same depth.** `MoveNodeAsync` legitimately changes a subtree's depth and path.

---

## 9. Integration Guidance For Future Modules

When a new module needs to anchor data to the academic hierarchy:

- **Store `Guid StructureNodeId` on the owning entity.** No `FacultyId`, no `ProgramId`.
- **Define the FK at schema level only** (`HasOne<StructureNode>().WithMany().HasForeignKey(...)`) with `OnDelete=Restrict`. **Do not** add an EF navigation property.
- **Resolve display / lineage through `IUniversityStructureService` or `IStructureLookupService`** — never join structure tables directly in business code.
- **Treat `StructureNodeId` as an opaque organizational scope token.** When you need to know "what type of node is this attached to", call the service and inspect the typed DTO; do not hardcode assumptions like "plans are always under Programs".
- **For permission / visibility scoping**, walk ancestors via `GetAncestorsChainAsync` or check `Path` prefix — never assume a fixed-depth lookup.
- **For caching**, key by `StructureNodeId`. If you need to invalidate on hierarchy reshape (rare), subscribe to a future move/delete event — *do not* try to encode hierarchy state into your cache key.
- **For listing children of a specific kind** (e.g. "all programs under this faculty"), use `IStructureLookupService.GetChildrenByTypeAsync(parentId, type)`. Do not assume only one child-type exists.
- **Soft-delete propagation.** If your entity is logically scoped to a node, decide explicitly: do you stay valid when the node is soft-deleted? The structure module will hide the node from queries; your entity may become orphaned-but-existent.

---

## 10. Query & Traversal Expectations

| Need | Method | Cost model |
|---|---|---|
| Full tree (admin UI) | `GetTreeAsync` | One `GetAllAsync` + in-memory dictionary join. O(n). Acceptable while n is in the low thousands. |
| Single node | `GetByIdAsync` (service) — also assembles its children | Currently re-fetches all nodes; will need a targeted path-prefix query if `n` grows. |
| Subtree of one node | `GetSubTreeAsync` → `GetDescendantsTreeAsync(path)` | Single `LIKE 'path%'` query + in-memory join. |
| Breadcrumb (root → self) | `GetBreadcrumbAsync` | Parses `Path` to IDs, single `WHERE Id IN (...)` lookup. |
| Ancestors only | `GetAncestorsChainAsync` | Same as breadcrumb minus the self node. |
| Children only (one level) | `GetChildrenAsync` / `GetChildrenOnlyAsync` | Direct `WHERE ParentId = X`. |
| Siblings | `GetSiblingsAsync(parentId)` | Direct `WHERE ParentId = X`. |
| Lookup by type | `IStructureLookupService.GetByTypeAsync(type)` | Currently scans all + LINQ filter; replace with `WHERE Type = X` when scale matters. |
| Move subtree | `MoveNodeAsync` | One descendant fetch + bulk update of `Path`/`Depth`/`UpdatedAt`. |
| Reorder siblings | `ReorderNodeAsync` | Fetch siblings, renumber, bulk update. |

**Traversal contract:** prefer `Path`-based lookups over recursive `ParentId` walks. The materialized path is the reason this module is cheap.

---

## 11. Future Expansion Considerations

- **Adding a new node kind.** Add enum value + entry in `StructureNodeRules.AllowedChildren`. Update `CanBeRoot` only if the new type can be a root. Migrations: none.
- **Multi-tenancy / multi-university.** Already possible at the data layer (multiple roots allowed); only `CanBeRoot` policy must relax, and ownership scoping must be layered above (likely via `Path` prefix or a `TenantId` column added to the schema).
- **Versioned hierarchy** (e.g. structure-as-of-academic-year). Currently not modeled. Would require either temporal tables or a `ValidFrom`/`ValidTo` pair on `StructureNode` plus path-aware version-resolving queries. Not a small change — design carefully.
- **Replacing `GetAllAsync` based tree builds.** When `n` exceeds a few thousand, swap `GetTreeAsync` and `GetByIdAsync` to subtree-only loads (`GetDescendantsTreeAsync`) and lazy expansion.
- **Performance ceilings.** `Path` is `nvarchar(4000)` indexed — wide indexes are expensive. If depth grows beyond ~30 levels routinely, consider a hash or a closure table.
- **Type-graph constraints.** `StructureNodeRules` is currently in-process and static. If administrators should configure allowed children dynamically, this dictionary must move into the database with cache invalidation.

---

## 12. Risks Of Hardcoding Semantics

- **Direct SQL against assumed table names** (`Faculties`, `Departments`) will fail — those tables do not exist.
- **Conditional logic keyed on "Department vs Program"** will break the moment an admin reshuffles the tree to skip departments. Use behavioral attributes (e.g. "does this node host plans?") rather than type identity wherever possible.
- **UI that assumes a fixed depth** (e.g. always renders 4 levels) will misrepresent trees that are deeper or shallower.
- **Caching by `(FacultyId, DepartmentId, ProgramId)` triplets** is meaningless and impossible — cache by `StructureNodeId`, optionally augmented with the node's `Path` prefix.
- **Permission rules of the form "user can see Faculty X"** must be expressed as "user can see node X and its descendants" using `Path` prefix matching, not type filtering.

---

## 13. Recommended AI Context Summary (compressed)

> **Drop-in context block for future prompts. ~150 tokens.**

```
Academic Structure = single self-referencing `StructureNode` table with enum `Type`,
materialized `Path` (/{guid}/{guid}/...), `Depth`, `Order`, `IsActive`, soft-delete
`IsDeleted` (EF global filter). One nullable `ParentId`. No per-type tables. Allowed
parent→child pairs live in `StructureNodeRules.AllowedChildren` (dictionary; mutable).
Roots restricted to `University` by policy, not schema. Move/delete cascade by `Path`
prefix in code, not DB. Other modules reference by `Guid StructureNodeId` only — no
EF navigations across modules. NEVER assume Faculty→Department→Program→Level shape:
hierarchy depth is unbounded, the same type may appear at multiple depths, and a
type may have multiple legal parent types. Leaf type is not fixed. To traverse,
parse `Path`; do not chain `ParentId` lookups. To list by type, use
`IStructureLookupService`. To add a node kind: add enum + rules entry, no migration.
```

---
