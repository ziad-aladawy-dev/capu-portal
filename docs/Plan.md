# Scoped Infrastructure & Authorization Hardening Plan

# Ownership & Boundary Rules

## My Scope ONLY

The implementation scope is strictly limited to:

- Cross-Cutting infrastructure
- Authentication
- Authorization
- Session/token lifecycle
- Permission infrastructure
- Logging/audit infrastructure
- Notification infrastructure
- Localization infrastructure
- Semesters module
- AcademicYears module

---

# Explicitly Out of Scope

The following domains and implementations must NOT be modified:

- Students module
- StructureNode / UniversityStructure module
- Staff/Admin business workflows
- Teammate-owned services
- Teammate-owned repositories
- Teammate-owned controllers
- Teammate domain entities
- Teammate DTOs
- Teammate validation flows

This includes:

- No business logic changes
- No transactional changes
- No service refactors
- No repository refactors
- No middleware injection into teammate flows
- No endpoint behavior changes
- No hierarchy redesign
- No CQRS migration
- No aggregate redesign
- No query redesign

---

# Middleware Restriction

Infrastructure middleware/components MAY be created and fully prepared for future usage, BUT:

- they must NOT be attached to teammate pipelines
- they must NOT modify teammate request flows
- they must NOT enforce behavior automatically outside my scope
- they must NOT require teammate code modifications

Allowed:
- creating middleware
- creating extension methods
- creating interfaces/contracts
- registering optional infrastructure

NOT allowed:
- enabling middleware globally for teammate modules
- forcing controller behavior outside my scope
- modifying teammate endpoint execution order

---

# Core Authorization Architecture Fix

## Problem

The current authorization model relies on:
- scattered permission constants
- manually seeded permissions
- disconnected `[HasPermission(...)]` usage

This creates:
- permission drift
- missing DB permissions
- inconsistent authorization mapping
- runtime mismatches between code and database

---

# New Permission Manifest Architecture (P0)

## Goal

Every module owns its permissions through a centralized manifest definition.

The system should work similarly to:
- modular DbContext registration
- schema/module wiring

Each module declares:
- its permissions
- its groups
- its scopes
- its metadata

The infrastructure layer auto-discovers and syncs them into the database.

---

# Permission Manifest Design

## Introduce

```csharp
IPermissionManifest
```

OR

```csharp
PermissionManifestBase
```

---

# Example

```csharp
public sealed class SemesterPermissionManifest : IPermissionManifest
{
    public string Module => "Semesters";

    public IReadOnlyCollection<PermissionDefinition> Permissions => new[]
    {
        PermissionDefinition.Create("semesters.view"),
        PermissionDefinition.Create("semesters.create"),
        PermissionDefinition.Create("semesters.update"),
        PermissionDefinition.Create("semesters.delete")
    };
}
```

---

# Infrastructure Responsibilities

## Permission Discovery

At startup:

- scan assemblies
- discover manifests
- aggregate permissions
- validate duplicates
- validate naming consistency

---

## Permission Synchronization

Infrastructure auto-syncs:
- permissions
- groups/modules
- metadata

into database seed/state.

No manual scattered permission registration.

---

## Authorization Consistency

`[HasPermission(...)]` values MUST come only from:
- manifest-generated constants
OR
- strongly typed permission references

No raw literals.

---

# Scope Restriction

## IMPORTANT

Authorization attributes/tags may ONLY be added inside:

- Cross-cutting scope
- Semesters module
- AcademicYears module

---

# Forbidden Authorization Changes

Do NOT add:
- `[Authorize]`
- `[HasPermission]`
- scope validation

to:
- Students
- StructureNodes
- teammate modules

Even if insecure.

Document issues only.

---

# Phase A — Authentication Infrastructure

## A1. Real Refresh Tokens (P0)

### Allowed Scope
- `AuthController`
- `AuthenticationService`
- Token entities/config
- Cross-cutting auth infrastructure

### Tasks
- Create `RefreshToken` entity
- Store hashed refresh tokens
- Add expiration
- Add rotation
- Add revocation
- Revoke on logout/password change

### Forbidden
- Modifying teammate auth workflows
- Modifying student/admin logic

### Required Tests
- Refresh rotation
- Expired refresh rejection
- Replay attack rejection
- Revoked token rejection

---

## A2. SessionVersion Optimization (P1)

### Tasks
- Optional cache layer for SessionVersion lookup
- Infrastructure-only optimization

### Forbidden
- Modifying teammate repositories/services

---

# Phase B — Authorization Infrastructure

## B1. Permission Manifest System (P0)

### Allowed Scope
- Cross-cutting authorization infrastructure
- Semesters module
- AcademicYears module

### Tasks
- Create `IPermissionManifest`
- Create manifest discovery system
- Create permission synchronization pipeline
- Generate strongly typed permission constants
- Validate duplicates/conflicts
- Remove scattered manual permission definitions

### Goal
Single source of truth for permissions.

---

## B2. Scoped Authorization Attributes (P0)

### Allowed Modules
ONLY:
- Semesters
- AcademicYears
- Cross-cutting endpoints

### Tasks
Add:

```csharp
[HasPermission(...)]
```

ONLY inside owned modules.

### Forbidden
- Touching Students module
- Touching StructureNodes module
- Touching teammate controllers/services

---

## B3. Redis Permission Cache (P1)

### Allowed Scope
- `PermissionHandler`
- `PermissionManagementService`
- `RedisCacheService`
- Cross-cutting auth infrastructure

### Tasks
- Distributed permission cache
- Cache versioning
- Invalidation hooks

### Forbidden
- RBAC redesign
- Semester redesign
- Structure redesign

---

## B4. Authorization Audit Logging (P2)

### Tasks
Log:
- permission denied
- failed auth
- token revocation
- role assignment

### Constraint
Infrastructure-only logging.

No business/domain modifications.

---

# Phase C — Notification Infrastructure

## C1. Outbox Enforcement (P1)

### Allowed Scope
- Notification infrastructure
- Dispatcher
- Handlers
- Outbox abstractions

### Tasks
- Route notifications through outbox infrastructure

### Forbidden
- Modifying teammate workflows
- Changing domain behavior

---

## C2. Retry / Poison Queue Handling (P2)

### Tasks
- Retry caps
- Dead-letter handling
- Failure visibility

### Forbidden
- Notification business logic redesign

---

# Phase D — Logging / Audit Infrastructure

## D1. Async Audit Logging (P1)

### Tasks
- Buffered logging
OR
- Outbox-backed audit pipeline

### Constraint
Infrastructure-only.

No service/business modifications.

---

## D2. Correlation IDs (P2)

### Tasks
Add:
- Request correlation IDs
- Auth tracing IDs

### Constraint
No request-flow modifications outside owned modules.

---

## D3. Sensitive Logging Protection (P1)

### Tasks
Sanitize:
- JWTs
- Refresh tokens
- Passwords
- Claims

### Forbidden
- DTO redesign
- Domain modifications

---

# Phase E — Localization Infrastructure

## E1. Centralized Exception Localization (P2)

### Allowed Scope
- Middleware
- Localization service
- Exception mapper

### Tasks
Localize:
- auth errors
- permission errors
- infrastructure validation messages

### Forbidden
- Rewriting teammate business messages

---

## E2. Localization Key Safety (P3)

### Tasks
- Strongly typed localization keys
OR
- Validation tests for missing keys

Infrastructure-only.

---

# Explicitly Deferred (Document Only)

The following may be documented but must NOT be implemented:

- Student concurrency redesign
- Structure hierarchy redesign
- Semester business redesign
- CQRS migration
- Aggregate refactors
- UnitOfWork redesign
- Soft-delete redesign
- Repository cleanup outside owned scope
- Query redesign in teammate modules
- Domain encapsulation/private setters

---

# Safe Execution Order

| Phase | Scope | Risk |
|---|---|---|
| A1 | Real refresh tokens | Critical |
| B1 | Permission manifest system | Critical |
| B2 | Scoped permission attributes | Critical |
| B3 | Redis permission cache | High |
| C1 | Notification outbox enforcement | High |
| D1 | Async audit logging | High |
| D3 | Sensitive log sanitization | High |
| E1 | Exception localization | Medium |

---

# Mandatory Boundary Rule

If a fix requires:
- modifying teammate business logic
- changing domain behavior
- redesigning structure hierarchy
- touching Students module
- touching StructureNodes module
- changing teammate repositories/services/controllers
- changing request flow outside owned scope

Then:

```text
Document the issue.
Do NOT implement the change.
```

---

# Guiding Principle

```text
Infrastructure hardening only.
Scoped authorization only.
Module-owned permission manifests.
No teammate workflow modification.
No invasive domain refactoring.
```