# Authorization Implementation Guide

This document describes how authorization works in the codebase **as of the
manifest-driven refactor** (Phases 1–5 of `Master_Refactor_Plan.md` plus the
follow-on hardening pass). It supersedes the conceptual `Authorization_Model.md`
on the runtime details; that file still describes the scope-first principles and
ABAC layer, which are unchanged.

Treat this as the engineering reference: it covers the manifest contract, the
storage shape, the runtime evaluation algorithm, the cache/version model, the
scope plumbing, and the compat surfaces. Read it before changing any code under
`CapitalUniversity.Core.Abstractions/CrossCutting/Auth/Authorization` or
`CapitalUniversity.Core.Infrastructure/Services/Authorization`.

---

## 1. Mental model in one line

```
Module → Resource → Action       (manifest, in code)
Role   → per-action grants       (DB)
Staff  → roles + per-action overrides (allow/deny) under a scope (DB)
Runtime: effective = (allow ∪ implied(allow)) − (deny ∪ reverse-implied(deny))
```

Every other detail in this doc is in service of that shape.

---

## 2. The three identities

A permission is identified at three layers; they must all agree.

| Layer | Shape | Source of truth |
|---|---|---|
| Wire / DTO / attribute | `"module.resource.action"` string (`PermissionIdentity`) | `PermissionNames` constants |
| Manifest | `IPermissionManifest` → `ResourceDefinition` → `ActionDefinition` | C# code in domain folders |
| Storage | `Modules.ModuleKey`, `Resources.Key`, `RolePermission.Action` / `StaffPermissionOverride.Action` | `CoreDbContext` |

The `PermissionManifestSynchronizer` reconciles manifest → DB on startup. The
runtime checks attribute string → `PermissionLookupCache` → set membership. The
`PermissionNamesCoverageTests` and `PermissionNamesManifestCoverageTests`
together guard the three layers from drifting apart.

---

## 3. Manifests (the source of truth)

### 3.1 Where they live

Each business domain owns its own manifest, next to its domain code:

| Module key | Manifest file |
|---|---|
| `permissions` | `Core.Application/CrossCutting/Auth/Authorization/Manifest/AuthorizationPermissionManifest.cs` (Auth's own meta-permissions) |
| `academics` | `Core.Application/Semesters/Authorization/AcademicsPermissionManifest.cs` |
| `courses` | `Core.Application/Courses/Authorization/CoursesPermissionManifest.cs` |
| `notifications` | `Core.Application/CrossCutting/Notifications/Authorization/NotificationsPermissionManifest.cs` |
| `student-information` | `Module.Student.Abstractions/Manifest/StudentInformationPermissionManifest.cs` |
| `payments` | `Module.Payments.Abstractions/PaymentsPermissionManifest.cs` |
| `course-offerings` | `Module.CourseOffering.Abstractions/Manifest/CourseOfferingPermissionManifest.cs` |
| `schedule` | `Module.Schedule.Abstractions/Manifest/SchedulePermissionManifest.cs` |

Auth is **only the engine**. It does not declare business permissions — it
aggregates manifests via DI (`AddSingleton<IPermissionManifest, ...>()`).

### 3.2 Manifest shape

```csharp
public interface IPermissionManifest
{
    string Module { get; }                                // "academics"
    string DisplayName { get; }                           // "Academic Timeline"
    string? Icon { get; }                                 // "Calendar"
    int?    OrderNumber { get; }                          // UI order
    IReadOnlyCollection<ResourceDefinition> Resources { get; }
}

public sealed record ResourceDefinition
{
    public string Key { get; init; }                      // "academic-years"
    public string DisplayName { get; init; }
    public int    OrderNumber { get; init; }
    public IReadOnlyCollection<ActionDefinition> Actions { get; init; }
}

public sealed record ActionDefinition
{
    public string Name { get; init; }                     // "EditClose"
    public int    OrderNumber { get; init; }
    public IReadOnlyCollection<string> Implies { get; init; } = Array.Empty<string>();
    public bool   IsDangerous { get; init; }              // UI hint, not runtime
    public bool   IsHierarchical { get; init; }           // metadata only
    public int?   DisplayOrder { get; init; }
}
```

### 3.3 Implies is **explicit and resource-local**

There is no global CRUD ladder. A resource that wants `Delete` to imply `View`
must say so. The two canonical helpers do this declaratively:

```csharp
ResourceDefinition.WithCrudActions("academic-years", "Academic Timeline", 0);
// View, Insert→{View}, EditClose→{View,Insert}, Open→{V,I,EC}, Delete→{V,I,EC,O}

ResourceDefinition.WithCrudActionsNoOpen("schedule-slots", "Schedule Slots", 0);
// Same, minus the Open verb.
```

A non-hierarchical resource just lists actions verbatim:

```csharp
new ResourceDefinition
{
    Key = "notifications",
    DisplayName = "Notifications",
    OrderNumber = 0,
    Actions = new[]
    {
        ActionDefinition.Hierarchical("View",   0),
        ActionDefinition.Hierarchical("Insert", 1, "View"),
    },
}
```

For domain-specific verbs that do **not** inherit anything (Approve, Publish,
Refund, OverrideGrades, …), use `ActionDefinition.Explicit("Approve", 0,
dangerous: true)`. These actions never appear in another action's `Implies` and
are never granted transitively.

### 3.4 Registry validation

`PermissionManifestRegistry` runs at DI construction (singleton, so it throws
on app start, not at request time). It rejects:

- Empty `Module` keys
- Duplicate `Module` keys across manifests
- Duplicate `(Module, Resource)` keys within a manifest
- Empty / whitespace action names
- Duplicate canonical `module.resource.action` identities
- `Implies` references to actions not declared on the same resource
- Self-implying actions

There is **no cycle detection at validation time**, but the expander breaks
cycles on first revisit, so a malformed manifest can't hang the runtime.

---

## 4. Storage

### 4.1 Tables

```
Modules                  (Id, ModuleKey, DisplayName, Icon, OrderNumber)
Resources                (Id, ModuleId, Key, DisplayName, OrderNumber)

Roles                    (Id, Name, IsSystemRole)
RolePermissions          (Id, RoleId, ResourceId, Action)
                          UNIQUE (RoleId, ResourceId, Action)

StaffRoles               (Id, StaffId, RoleId, Year, Semester,
                          StructureNodeId, StructureNodePath)

StaffPermissions         (Id, StaffId, ResourceId, Action, Type,
                          Year, Semester, StructureNodeId, StructureNodePath,
                          ExpiresAt)
```

Key facts:

- **One row per granted action.** `RolePermission.Action` is a string
  (`"View"`, `"EditClose"`, `"Approve"`, …). There is no integer ladder in
  storage anymore.
- **`Type` on overrides** is `Allow` or `Deny`.
- **Scope columns are inline on the grant rows.** There used to be separate
  `RolePermissionScopes` / `StaffPermissionScopes` tables; they were dead and
  were dropped in `Phase5_DropDeadScopeTables`.

### 4.2 Why not a string FK for Module/Resource?

`RolePermissions.ResourceId` is a Guid FK to `Resources.Id` rather than a raw
`(ModuleKey, ResourceKey)` pair. This gives referential integrity and a single
indirection point for renames (the synchroniser refreshes `Resources.DisplayName`
in place). The trade-off is that a stale `Resources` row will hold grants alive
even if no manifest declares it — the synchroniser only adds and refreshes; it
never deletes (additive by design).

### 4.3 ActionLevel — what remains and why

`ActionLevel` is **still present in the codebase**, in three intentional places:

1. `PermissionOverrideModel.Level` — DTO field, preserved for API compat.
2. `ManifestActionExpander.LevelToActionName` / `ActionNameToLevel` — the
   compat shim between the DTO and the storage.
3. `DataSeeder.Grant(role, module, resource, ActionLevel.X)` — ergonomic
   seeder input. Expands to per-action rows via the CRUD ladder.

Nothing else reads `ActionLevel`. No runtime evaluation arithmetic touches it.
Custom actions (Approve, Refund, …) are **not representable on the legacy
`Level` field**; they flow through `PermissionOverrideModel.Actions` instead.

---

## 5. Runtime evaluation

The single source of effective permissions is
`PermissionManagementService.GetPermissionLookupAsync(userId, ct)`. It returns a
`HashSet<string>` of canonical `module.resource.action` identities, cached.

### 5.1 The algorithm

```
1. Resolve active scope from IRequestContext:
     year, semester, structureNodeId, structureNodePath

2. Load every row that survives the scope filter:
     - StaffRoles matching the user
     - RolePermissions for those roles                  (per-action grants)
     - StaffPermissions for the user                     (per-action overrides)

3. Build allowed/denied sets directly from rows:
     allowed = ∪ canonical(rp)            over role permissions
              ∪ canonical(sp) where Type=Allow
     denied  = ∪ canonical(sp) where Type=Deny

4. effective = allowed − denied
```

There is **no arithmetic, no `MAX(level)`, no `Level - 1`**. Storage is
already per-action and implies were folded in at write time, so the runtime
does pure set operations.

### 5.2 Forward-implies on writes (allow)

When an allow grant is persisted, the implies graph runs **forward**:

| Caller writes | Stored rows |
|---|---|
| `Allow Level=Delete` on `payments.invoices` | `View`, `Insert`, `EditClose`, `Open`, `Delete` |
| `Allow Actions=["Approve"]` on `payments.invoices` | `Approve` |

So holding `Delete` on a CRUD resource genuinely lets you `View`, `Insert`,
`EditClose`, `Open` and `Delete` at the row level — because all five action
strings are in the user's effective set.

### 5.3 Reverse-implies on writes (deny)

Denies expand through the **reverse** implies graph. This is intentional and
asymmetric: denying a sub-action must also revoke any super-action that would
otherwise grant it transitively.

| Caller writes | Stored deny rows |
|---|---|
| `Deny Level=EditClose` on `payments.invoices` | `EditClose`, `Open`, `Delete` |
| `Deny Level=View` on `payments.invoices` | `View`, `Insert`, `EditClose`, `Open`, `Delete` |
| `Deny Level=Delete` on `payments.invoices` | `Delete` |

Worked example: user holds the `Invoices.Delete` grant via role, admin issues a
deny override for `EditClose`.

```
allowed = { View, Insert, EditClose, Open, Delete }   (from Delete grant, forward-expanded)
denied  = { EditClose, Open, Delete }                 (reverse-expanded from EditClose deny)
effective = allowed − denied = { View, Insert }
```

Without the reverse expansion, the user would keep `Delete` and silently
bypass the admin's intent — that's the audit's "fail-open deny" hazard.

`DenyTransitivityTests` pins this contract.

### 5.4 Cache

```
Key: perm_lookup_{epoch}_{userId}_{userVersion}_{year}_{semester}_{nodeId}
```

Three knobs invalidate without enumeration:

- `IPermissionCacheInvalidator.InvalidateUserAsync(userId)` rotates the
  per-user version stamp. Used by every write path in
  `PermissionManagementService`.
- `IPermissionCacheInvalidator.InvalidateAllAsync()` rotates the global
  epoch. Used by `UniversityStructureService.MoveNodeAsync` after a path
  repair (see §6).
- `IPermissionCacheInvalidator.InvalidateRoleAsync(roleId)` enumerates the
  role's current assignees and bumps each one's version.

The cache key embeds the epoch, so rotating it instantly orphans every
existing entry across every user without a delete sweep.

---

## 6. Scoping

Three axes on every grant: temporal (Year, Semester), structural
(`StructureNodeId` + `StructureNodePath`), and global (sentinel `"Global"`
string).

### 6.1 Scope match rule

`PermissionService.LoadAssignmentsAsync` and `LoadOverridesAsync` apply the
same predicate inline so EF can translate it to SQL:

```csharp
(sr.Year == ScopeKeys.Global || sr.Year == scope.Year) &&
(sr.Semester == ScopeKeys.Global || sr.Semester == scope.Semester) &&
(sr.StructureNodePath == null
   || (scope.StructureNodePath != null
       && scope.StructureNodePath.StartsWith(sr.StructureNodePath)))
```

A grant scoped to a node grants on that node and every descendant — the
`StartsWith` over the materialised path does the subtree match. A global
grant (no `StructureNodePath`) matches every scope.

### 6.2 Path-drift handling

`StaffRoles.StructureNodePath` and `StaffPermissions.StructureNodePath`
**snapshot** the node's path at the time the grant is written. Without
maintenance, a node move would orphan the snapshot — the user would silently
keep access to the old subtree and lose access to the new one. The audit
flagged this as the "path-staleness bypass."

Mitigation:

1. `IStructureNodeRepository.RepairPermissionPathPrefixAsync(oldPath,
   newPath, ct)` rewrites every snapshot whose prefix matches `oldPath` via
   `ExecuteUpdateAsync` (single round-trip, transactional with the move).
2. `UniversityStructureService.MoveNodeAsync` calls it whenever the
   recomputed path differs from the original.
3. Same method also calls `IPermissionCacheInvalidator.InvalidateAllAsync()`
   so any in-flight cached lookup is orphaned.

Regression covered by `StructureNodeMoveRepairsPermissionPathsTests`.

---

## 7. Closable entities

Some domain entities have a "closed" lifecycle state (academic year, semester,
…). Closure is enforced **in the domain**, not via repository checks alone.

```csharp
public class AcademicYear : BaseEntity
{
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }

    public void EnsureMutable()  // call before any mutation
    public void Close()          // requires EditClose permission upstream
    public void Reopen()         // requires Open permission upstream
}
```

Service code calls `EnsureMutable()` before `Update*`/`Delete*`. Controllers
expose:

| Verb | Endpoint | Permission |
|---|---|---|
| Update | `PATCH /api/academic-years/{id}` | `academics.academic-years.EditClose` |
| Delete | `DELETE /api/academic-years/{id}` | `academics.academic-years.Delete` |
| Close  | `POST /api/academic-years/{id}/close` | `academics.academic-years.EditClose` |
| Reopen | `POST /api/academic-years/{id}/reopen` | `academics.academic-years.Open` |

Same shape for `SemestersController`. `ClosableEntityTests` cover the domain
invariants.

---

## 8. The HasPermission attribute

```csharp
[HasPermission(PermissionNames.AcademicTimeline.EditClose)]
public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcademicYearRequest req)
```

Pipeline:

1. ASP.NET resolves the requirement → `PermissionHandler`.
2. Handler calls `IPermissionManagementService.GetPermissionLookupAsync` and
   `Contains` on the canonical string.
3. On miss it logs via `IAuthAuditLogger.LogPermissionDeniedAsync` and lets
   the framework return 403.

`PermissionNames` is the **only** place attribute literals are sourced from.
Two coverage tests guard it:

- `PermissionNamesCoverageTests` scans every `.cs` under `src/` for
  `[HasPermission("...")]` literals and requires each to exist on
  `PermissionNames`.
- `PermissionNamesManifestCoverageTests` constructs the live registry and
  requires every `PermissionNames` constant to round-trip against a manifest.

If you add an attribute usage with a typo, the source-scan fails. If you add a
constant without a matching manifest entry, the manifest cross-check fails.

---

## 9. DTO contract (frontend-facing — do not break)

```csharp
public class PermissionOverrideModel
{
    public Guid       ResourceId { get; set; }
    public ActionLevel Level     { get; set; }     // legacy ladder
    public OverrideType Type      { get; set; }     // Allow | Deny
    public List<string>? Actions  { get; set; }     // optional explicit set
}
```

- **`Level`** is the legacy ladder (`View=1 … Delete=5`). On write we expand
  it through the manifest (forward for Allow, reverse for Deny). On read we
  derive it via `ManifestActionExpander.CollapseToMaxLevel` — the highest
  ladder step whose forward closure is a subset of the stored action rows.
- **`Actions`** is the explicit list. When populated on a write it takes
  precedence over `Level`. On read it always reflects the per-action rows.
- Custom verbs (Approve, Refund, OverrideGrades) **cannot be represented by
  `Level`** and only appear via `Actions`. The DTO retains both fields so
  legacy clients keep working without losing fidelity.

`PermissionTreeDto` (`Modules → Resources → PermissionActionDto`) shape is
unchanged. `PermissionActionDto.Action` carries the manifest's action name and
`IsAssigned` is computed from per-action rows.

---

## 10. How to add a new permission

```text
1. Decide the canonical identity      module.resource.action
2. Add the action to the manifest     IPermissionManifest.Resources[*].Actions
3. Add a const on PermissionNames     public const string Action = "module.resource.action"
4. Use it on the controller           [HasPermission(PermissionNames.X.Action)]
5. (Optional) Add Implies edges       ActionDefinition.Hierarchical("Edit", 0, "View")
6. Run tests                          coverage tests catch all four guard rails
7. Deploy                             synchroniser materialises rows; existing grants pick it up
```

The synchroniser is **additive** — it never deletes rows. Manifests grow
forwards-compatibly; retiring a verb is a deliberate three-step process
(remove from manifest, write a one-off cleanup migration, drop the constant).

---

## 11. Migration notes

Two schema migrations land the refactor:

- `20260521190611_Phase3_PerActionGrants_AndClosable` — drops the legacy
  `RolePermissions.Level` + `StaffPermissions.Level` columns, drops the dead
  `PermissionId` columns, adds the per-action `Action` column, backfills via a
  SQL implies expansion, and adds the new `(RoleId, ResourceId, Action)`
  unique index. Also adds `IsClosed` + `ClosedAt` to `AcademicYears` and
  `Semesters`.
- `20260521191923_Phase5_DropDeadScopeTables` — drops the unused
  `RolePermissionScopes` and `StaffPermissionScopes` tables.

For pre-existing rows the migration only knows the *forward-implies*
expansion (`Level=N` → all actions up to N). If your environment has real
deny-override data this is a benign over-grant of denials; for production,
write a one-off SQL cleanup that re-derives deny rows from the original
intent before deploying. Allow data round-trips perfectly.

The migration does **not** auto-bump the global cache epoch. Deploy steps
should call `IPermissionCacheInvalidator.InvalidateAllAsync()` (or simply
flush Redis) on first start after the migration so cached lookups computed
against the old schema are orphaned.

---

## 12. What is intentionally not in this system

- **No integer hierarchy in storage.** Removed in Phase 3+5.
- **No implicit global CRUD ladder.** Implies are per-resource, declared.
- **No deny-by-decrement arithmetic.** Set operations only.
- **No scope tables.** Scope is inline on grant rows.
- **No `ResourceId` magic strings** (`"*"` etc.). Split into
  `IPermissionService.GetAllPermissionsAsync` /
  `GetResourcePermissionsAsync`.
- **No auth-owned business permissions.** Manifests live with the domain
  that owns them.

---

## 13. Test inventory

| Test class | Pins |
|---|---|
| `ManifestImpliesTests` | implies graph (forward closure, cycles, undeclared refs, explicit-only) |
| `DenyTransitivityTests` | reverse-implies for denies; deny `EditClose` removes `Delete` |
| `ClosableEntityTests` | `IsClosed`, `Close`, `Reopen`, `EnsureMutable` |
| `PermissionManagementServiceTests` | per-action write fan-out; CRUD ladder via DTO Level |
| `PermissionTreeQueryHandlerTests` | tree shape, IsAssigned per action |
| `PermissionManifestRegistryTests` | startup validation surface |
| `PermissionManifestSynchronizerTests` | additive sync, DisplayName refresh, FK preservation |
| `PermissionNamesCoverageTests` | every `[HasPermission(...)]` literal is a `PermissionNames` constant |
| `PermissionNamesManifestCoverageTests` | every `PermissionNames` constant round-trips to a manifest |
| `PermissionLookupCacheTests` | hit/miss, version-stamp invalidation |
| `StructureNodeMoveRepairsPermissionPathsTests` | path drift fix wiring |
| `AuthorizationResultTests`, `AuthorizationScopeTests` | scope object semantics |
| `DataSeederSelfHealingTests` | seeder runs idempotently against manifest |

---

## 14. Related docs

- `Authorization_Model.md` — conceptual scope-first / RBAC + ABAC model
  (unchanged; read first if you're new to the system).
- `Master_Refactor_Plan.md` — the design rationale for the refactor.
- `caching-strategy.md` — the broader cache layer this builds on.
- `RequestPipeline_Context_Authorization_and_Localization.md` — where the
  request-scoped context that feeds the scope resolver comes from.
