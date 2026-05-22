# Authorization System Refactor Master Plan

## Context

The authorization system has already undergone a partial migration:

* `Service` → `Resource`
* `Domain` removed
* `Resource.Key` introduced
* manifest structure moved toward:

```text
Module -> Resource -> Actions
```

However, the runtime authorization model still behaves like a hardcoded CRUD ladder:

```text
View < Insert < EditClose < Open < Delete
```

The audit revealed that:

* manifests are partially decorative
* custom actions are ignored at runtime
* inheritance is implicit and global
* authorization semantics depend on integer arithmetic
* deny behavior is fragile
* action flexibility is fake
* runtime logic and manifest logic drifted apart

This document defines the target architecture and phased migration strategy.

---

# Critical Clarification About `Open`

The audit incorrectly assumed `Open` was nonsense globally.

It is NOT.

The domain actually contains a real business concept:

## Closable Entities

Examples:

* academic year
* semester
* invoice batch
* grading period
* registration period
* course offering state
* financial transaction window

A resource may have lifecycle state:

```text
Open / Closed
```

meaning:

* closed entities are immutable under normal permissions
* only privileged users may reopen
* closure is an auditable domain action

---

# Required Domain Model Addition

## Add explicit closure state

Every closable entity MUST contain domain state:

```csharp
bool IsClosed
```

Optional metadata:

```csharp
DateTime? ClosedAt
Guid? ClosedBy
string? CloseReason
```

---

# Runtime Rule: Editing

If:

```text
entity.IsClosed == true
```

then:

* update operations must be rejected
* domain service must enforce invariant
* repository checks alone are NOT sufficient

---

# Runtime Rule: Reopen

Only users with:

```text
Resource.Open
```

may reopen a closed entity.

---

# Runtime Rule: Close

Only users with:

```text
Resource.EditClose
```

may close an entity.

---

# Core Architecture Goal

Target:

```text
Module
 └── Resource
      └── ActionDefinition
```

Where actions are:

* explicit
* manifest-defined
* resource-local
* independently evaluatable
* optionally hierarchical

NOT globally integer-ranked.

---

# ActionDefinition Model

```csharp
public sealed record ActionDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }

    public IReadOnlyCollection<string> Implies { get; init; } = Array.Empty<string>();

    public bool IsDangerous { get; init; }

    public bool IsHierarchical { get; init; }

    public int? DisplayOrder { get; init; }
}
```

---

# Critical Rule: No Global Inheritance

Inheritance MUST be:

* explicit
* resource-local
* deterministic

### Example

```json
{
  "Delete": {
    "implies": ["View", "EditClose", "Open"]
  },
  "Approve": {
    "implies": []
  }
}
```

---

# Action Categories

## CRUD-like (may inherit)

* View
* Create
* EditClose
* Open
* Delete

ONLY when explicitly declared.

---

## Explicit-only actions

* Approve
* Publish
* AssignRole
* OverrideGrades
* Export
* Impersonate
* ManagePermissions
* Refund

NEVER inherit automatically.

---

# Current System Failures

* manifest actions ignored at runtime
* integer ladder is load-bearing
* deny logic uses arithmetic
* duplicated action logic exists
* custom actions are not functional

---

# Forbidden Patterns

* ActionLevel arithmetic
* MAX(level)
* global CRUD assumption
* implicit inheritance
* duplicated action arrays
* persisted implied permissions

---

# Persistence Model

## RolePermissionAction

```text
RoleId
ResourceId
Action
```

---

## StaffPermissionOverrideAction

```text
StaffId
ResourceId
Action
Type (Allow | Deny)
Year
Semester
StructureNodePath
```

---

# Effective Permission Algorithm

```text
allowSet = explicit allows + implied(allows)
denySet  = explicit denies + implied(denies)

effective = allowSet - denySet
```

---

# API COMPATIBILITY (CRITICAL)

MUST NOT BREAK:

* DTO shapes
* API routes
* frontend payload structure
* PermissionActionDto
* PermissionOverrideModel
* module.resource.action strings
* PermissionNames constants
* Swagger contracts

---

# Compatibility Strategy

* use adapter layer
* preserve DTOs unchanged
* map internal model → legacy shape
* dual-read during migration

---

# Migration Plan

## Phase 1 — Manifest becomes source of truth

* remove hardcoded action arrays
* remove duplicate runtime tables
* introduce ActionDefinition.Implies
* runtime uses manifest only

---

## Phase 2 — Closable resource model

* add IsClosed
* enforce domain-level immutability
* restrict Open/EditClose exposure

---

## Phase 3 — Explicit action persistence

* introduce RolePermissionAction tables
* introduce StaffPermissionOverrideAction
* dual write with legacy Level

---

## Phase 4 — Remove integer ladder

* remove ActionLevel usage
* remove arithmetic deny logic
* replace with set operations

---

## Phase 5 — Cleanup

* remove scopes tables
* remove dead PermissionId column
* unify permission sources

---

# StructureNode Risk

Problem:

* path snapshot becomes stale when hierarchy changes

Fix:

* transactional path updates
* cache invalidation on structure mutation

---

# Required Tests

## Permission logic

* Delete implies View
* EditClose implies View
* Approve does NOT imply View
* deny overrides implied allow

---

## Closable entities

* closed entity rejects update
* Open permission required for reopen
* EditClose does not reopen

---

## API stability

* permission tree unchanged shape
* DTO backward compatibility
* serialization stability

---

# Final Target Architecture

```text
Module
 └── Resource
      ├── Actions (manifest)
      ├── Grants (DB)
      └── Closure capability
```

Runtime:

```text
effective = allow + implied(allow) - deny - implied(deny)
```

---

# Key Guarantees

* no integer-based auth
* no global inheritance
* explicit action semantics
* resource-local rules
* deterministic evaluation
* API compatibility preserved
* No Modifications in DTOs exposed to frontend
* No modifications in Apis contracts/signature 
* safe migration path
