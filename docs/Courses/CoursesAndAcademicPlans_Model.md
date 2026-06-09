# Courses + Academic Plans Module — AI Context Model

> Compressed architectural context for AI agents. Do **not** treat this as user-facing documentation. It encodes design intent, invariants, and anti-assumptions so future AI sessions reason about this module without re-scanning `src/2.Core/.../Courses/**`.
>
> Sources read:
> [Course.cs](src/2.Core/CapitalUniversity.Core.Domain/Courses/Course.cs),
> [CourseCategory.cs](src/2.Core/CapitalUniversity.Core.Domain/Courses/CourseCategory.cs),
> [AcademicPlan.cs](src/2.Core/CapitalUniversity.Core.Domain/Courses/AcademicPlan.cs),
> [AcademicPlanCourse.cs](src/2.Core/CapitalUniversity.Core.Domain/Courses/AcademicPlanCourse.cs),
> [CourseService.cs](src/2.Core/CapitalUniversity.Core.Application/Courses/CourseService.cs),
> [AcademicPlanService.cs](src/2.Core/CapitalUniversity.Core.Application/Courses/AcademicPlanService.cs),
> [CourseValidators.cs](src/2.Core/CapitalUniversity.Core.Application/Courses/Validators/CourseValidators.cs),
> [AcademicPlanValidators.cs](src/2.Core/CapitalUniversity.Core.Application/Courses/Validators/AcademicPlanValidators.cs),
> [CourseConfiguration.cs](src/2.Core/CapitalUniversity.Core.Infrastructure/Persistence/Configurations/CourseConfiguration.cs),
> [AcademicPlanConfiguration.cs](src/2.Core/CapitalUniversity.Core.Infrastructure/Persistence/Configurations/AcademicPlanConfiguration.cs),
> [AcademicPlanCourseConfiguration.cs](src/2.Core/CapitalUniversity.Core.Infrastructure/Persistence/Configurations/AcademicPlanCourseConfiguration.cs),
> [ICourseRepository.cs](src/2.Core/CapitalUniversity.Core.Abstractions/Repositories/ICourseRepository.cs),
> [IAcademicPlanRepository.cs](src/2.Core/CapitalUniversity.Core.Abstractions/Repositories/IAcademicPlanRepository.cs),
> DTOs under `src/2.Core/CapitalUniversity.Core.Abstractions/Courses/DTOs/`.

---

## 1. Executive Summary

This module owns two stable concerns and **deliberately nothing else**:

1. **The school-wide course catalog** (`Course`). Every course is a single, reusable, globally unique catalog entry.
2. **Curriculum composition** (`AcademicPlan` + `AcademicPlanCourse`). A plan is an organizational container, owned by a `StructureNode`, declaring which courses appear at which level/semester and whether they are mandatory.

**Out of scope (enforced by absence of fields/relations):** prerequisites, blocking rules, registration, enrollment, transcripts, GPA, schedule resolution, course offerings per term, capacity, sectioning. These belong to a future Registration module and must not be added here.

The module's binding to the academic hierarchy is intentionally indirect: a plan points at a `StructureNodeId`. The plan **does not know** whether that node is a Program, Department, Faculty, or any other type — that is a downstream policy decision.

---

## 2. Core Design Philosophy

- **Catalog is global; plans are organizational.** A `Course` is school-wide and lives once. A plan is a *use* of catalog entries under one organizational owner.
- **Plans bind to the hierarchy at a single, abstract point.** A plan stores `StructureNodeId` (an opaque GUID) — never `ProgramId`, `DepartmentId`, etc. The owning node may currently *typically* be a Program or Department, but the module does not enforce this.
- **A plan is a curriculum sketch, not a runtime engine.** It says *what courses make up this plan and roughly when in the journey*. It does not say *who is taking which course this term*.
- **Cross-module references are by ID, never by EF navigation.** `AcademicPlan.StructureNodeId` is an FK with no `.StructureNode` property. This enforces module isolation.
- **Caching follows a shared-object pattern.** Single object payloads (`course:object:{id}`, `academicplan:object:{id}`) are cached, scoped per read. Lists are not cached.
- **Scope (visibility) is layered on top.** `IEffectiveScope.CanAccessStructureNodeAsync(StructureNodeId)` gates every read/mutation. Out-of-scope reads return `null` (controller maps to 404) so the module cannot leak across organizational boundaries even through a warm cache.

---

## 3. Architectural Intent

| Concern | Location | Rationale |
|---|---|---|
| Stable, globally unique course identity | `Course.Code` (unique, upper-cased, trimmed) | Catalog must not drift on casing/whitespace |
| Reusability of courses across plans | No FK from `Course` to plan; plans reference `CourseId` | A course can appear in many plans, at different levels/semesters |
| Plan ownership | `AcademicPlan.StructureNodeId` (FK, no nav) | Plan answers "who curates this curriculum?" |
| Plan composition | `AcademicPlanCourse` join with `(Level, Semester, IsMandatory)` | Curriculum layout, not enrollment |
| Plan versioning | `EffectiveFrom`, `EffectiveTo` | Allows multiple plans per node across catalog years |
| Plan activation | `IsActive` (application-level "one active per node" expectation) | Schema does not enforce single-active; policy does |
| Optimistic concurrency | `AcademicPlan.RowVersion` (`rowversion`) | Plans are co-edited; prevents lost updates |
| Cache invalidation | Per-id removal on mutation | Composition edits invalidate the parent plan's key, never an individual entry |
| Authorization scoping | `IEffectiveScope` checks on every read/write | Tenant/role boundary enforcement |

---

## 4. Database Behavioral Model

### Table: `Courses`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` PK | |
| `Code` | `nvarchar(32)` required, **unique**, case-insensitive collation | Normalized to uppercase + trimmed by entity setter |
| `Title` | `nvarchar(200)` required | |
| `CreditHours` | `int` required | Validator range: `0..12` |
| `Category` | `int` (enum `CourseCategory`) required | Stable contract — never reorder enum values |
| `IsActive` | `bit`, indexed | Soft-disable; deletion is hard at API level |
| audit | `BaseEntity` columns | |

### Table: `AcademicPlans`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` PK | |
| `StructureNodeId` | `uniqueidentifier` required, FK→`StructureNodes.Id`, `OnDelete=Restrict`, **no EF nav** | |
| `Name` | `nvarchar(200)` required | |
| `EffectiveFrom` | `datetime` required (inclusive) | |
| `EffectiveTo` | `datetime` nullable (inclusive); `null` = open-ended | Service enforces `EffectiveTo > EffectiveFrom` |
| `IsActive` | `bit` | |
| `RowVersion` | `rowversion` | Optimistic concurrency |
| Composite index | `(StructureNodeId, IsActive)` | Plan-picker hot path |

### Table: `AcademicPlanCourses`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` PK | |
| `AcademicPlanId` | FK → `AcademicPlans.Id`, **`OnDelete=Cascade`** | |
| `CourseId` | FK by ID; no EF nav | |
| `Level` | `int` required, validator `1..10` | Plan-relative; semantic depends on plan |
| `Semester` | `int` required, validator `1..4` | Plan-relative ordinal |
| `IsMandatory` | `bit` | Required vs elective in the plan |
| **Unique index** | `(AcademicPlanId, CourseId)` | A course appears at most once per plan |
| Index | `(AcademicPlanId, Level, Semester)` | Layout query |

### Behavioral guarantees

- **Course code is normalized in the entity setter.** Whitespace stripped, upper-cased. Collation belt-and-braces for raw SQL inserts.
- **Cascade rules:**
  - `AcademicPlan` → `AcademicPlanCourse`: **cascade delete** (the composition is owned by the plan).
  - `AcademicPlan` → `StructureNode`: **restrict** (cannot delete a node that owns plans).
  - `AcademicPlanCourse` → `Course`: no EF-managed FK; a course cannot be deleted from the catalog while referenced (the repository's `Delete` is hard-delete via EF and will fail if FK constraint exists at DB level; verify before relying on this in callers).
- **No soft delete on courses or plans.** `CourseService.DeleteAsync` and `AcademicPlanService.DeleteAsync` perform repository-level deletes followed by `SaveChanges`. (Contrast with `StructureNode`, which is soft-delete only.)
- **`IsActive` ≠ deleted.** `IsActive=false` on a course keeps history visible but excludes from active listings.

---

## 5. Entity Interaction Map

```
   StructureNode  ◄────── (FK by ID, no navigation)  AcademicPlan
                                                        │
                                                        │ 1..N (cascade)
                                                        ▼
                                              AcademicPlanCourse
                                                        │
                                                        │ FK by ID (no navigation)
                                                        ▼
                                                     Course
```

- **`AcademicPlan` knows its `StructureNode` only as an ID.** It cannot navigate to it via EF. All visibility checks go through `IEffectiveScope`.
- **`AcademicPlanCourse` knows its `Course` only as an ID.** Course details are fetched via `ICourseRepository` (read-through), not via Include.
- **`AcademicPlanCourse` has a navigation back to `AcademicPlan`** (intra-module — allowed). It is the only navigation property that crosses entity boundaries in this module.

---

## 6. Invariants

These are guaranteed (or close to guaranteed) by the module and may be relied on:

1. **`Course.Code` is globally unique, case-insensitive.** Enforced by DB unique index + collation + entity-level normalization + `CodeExistsAsync` precheck.
2. **A course appears at most once in a single plan.** Enforced by `(AcademicPlanId, CourseId)` unique index + `ContainsCourseAsync` precheck.
3. **A plan must own a real, accessible `StructureNodeId`.** Create rejects if `IEffectiveScope.CanAccessStructureNodeAsync` returns false (mapped to `NotFoundException` to avoid leaking existence).
4. **`EffectiveTo > EffectiveFrom`** when set. Enforced in create validator + update path.
5. **Plan composition `Level ∈ [1, 10]`, `Semester ∈ [1, 4]`.** Validator-level invariant; not enforced at schema level.
6. **`CreditHours ∈ [0, 12]`.** Validator-level invariant.
7. **All reads filter through scope.** A cached plan whose owning node becomes out-of-scope is *not* returned — scope is re-checked against the cached `StructureNodeId` post-fetch.
8. **Mutations invalidate exactly the `:object:{id}` cache entry.** No broader sweep — list endpoints are not cached.
9. **`AcademicPlan.RowVersion` participates in EF optimistic concurrency.** Concurrent updates fail with `DbUpdateConcurrencyException` at `SaveChanges`.
10. **Plans are deleted (hard) only via service path** — `AcademicPlanCourse` rows cascade away with them.

---

## 7. Allowed Flexibility (Intentionally Loose)

- **The owning `StructureNodeId` is not constrained to a specific node type.** Today plans typically sit under a Program or Department, but the schema permits any node. Future expansion may legitimately put a plan under a Level, Specialization, or even a Faculty.
- **A `StructureNode` may own zero, one, or many plans.** "One active plan per node" is a *typical* expectation enforced in policy/UI, not in the schema. Two active plans is structurally valid (e.g. during transition between catalog years).
- **`Level` and `Semester` are plan-relative.** Two plans may use the numbers differently — one may use `Level 1..4` to mean academic years; another may use `Level 1` to mean a foundation phase. **Do not derive duration in years from `Level` values.**
- **`Category` is informational.** It drives UI filtering and may inform plan composition rules, but the module enforces no relationship between `Category` and `IsMandatory` or position in the plan.
- **Courses are reusable.** The same `Course` may live in many plans at different `(Level, Semester, IsMandatory)` triplets.
- **Plan versioning is permissive.** Overlapping `EffectiveFrom`/`EffectiveTo` ranges across plans on the same node are allowed; the resolver picks among them by `IsActive` + caller intent.

---

## 8. Dangerous Assumptions To Avoid

> Future modules and AI agents **must not** assume any of these:

1. **A plan's `StructureNodeId` is NOT guaranteed to point at a Program.** It may be any node type. Always resolve the node via the structure service if you need type-aware behavior.
2. **`AcademicPlanCourse` is NOT a registration record.** It does not represent a student taking a course. Do not infer enrollment from it.
3. **`Level`/`Semester` in `AcademicPlanCourse` do NOT define when a student must register.** They define curriculum layout. Registration cadence belongs to the future Registration module.
4. **`IsMandatory` does NOT block a student from completing the plan.** It is a curriculum classification, not a graduation rule (no graduation engine exists here).
5. **`Course` has NO prerequisites field, NO co-requisites, NO restrictions list.** Do not add them to `Course`; add them in the future Registration module with their own entity.
6. **Course `IsActive=false` does NOT cascade-remove the course from existing plans.** Plans retain historical composition; the UI is responsible for surfacing inactive entries appropriately.
7. **Deleting a course is NOT soft.** Unlike `StructureNode`, course deletion is hard. If a course is referenced by `AcademicPlanCourse`, deletion will likely fail at the DB layer or orphan rows — callers must check usage first. (There is currently no explicit "is course in use?" guard inside `CourseService.DeleteAsync`.)
8. **The cache cannot be relied upon to reflect a structure-node-level visibility change.** Scope is re-checked on every read, but cached payloads themselves do not auto-evict on node moves. If a node moves between scopes, the cache continues to serve until TTL or explicit mutation.
9. **Do not navigate `AcademicPlan.StructureNode`.** It does not exist as an EF navigation. Adding one breaks the modularity rule.
10. **Do not add a `CourseId` foreign key with cascade to a future enrollment table directly off `Course`.** Registration belongs elsewhere; keep `Course` clean.
11. **The list response (`GetForStructureNodeAsync`) does NOT include `PlanCourses`.** Callers wanting full composition must re-fetch by ID. Do not "optimize" by Including — it triggers N+1.
12. **Out-of-scope reads return `null`, not an empty `IsActive=false` payload.** Treat `null` as "either does not exist or you may not see it" — never differentiate.

---

## 9. Integration Guidance For Future Modules

When building modules that interact with courses/plans (esp. Registration, Scheduling, Transcript):

### Registration module (future)

- **Prerequisites:** introduce a new entity (`CoursePrerequisite { CourseId, PrerequisiteCourseId, Kind, ... }`) in the Registration module, not here. `Course` stays free of registration concerns.
- **Enrollment:** create an `Enrollment { StudentId, CourseOfferingId, ... }` entity referencing a future `CourseOffering` (a per-term instantiation of a `Course`). Do not reference `AcademicPlanCourse` directly — composition ≠ offering.
- **Transcript / GPA:** belong to Registration's read models; never touch `Course` or `AcademicPlan`.

### Scheduling / offerings (future)

- **Course offerings** (per term, per section) must be a new entity that *references* `Course.Id`. Capacity, instructor, room, time slot belong there.
- **Schedule resolution** may consult `AcademicPlanCourse.(Level, Semester)` as a recommendation hint, but never as a hard rule.

### Permissions / scoping

- **Always pass through `IEffectiveScope`.** Reuse the existing `CanAccessStructureNodeAsync` check rather than building parallel checks.
- For permission checks involving a plan, resolve `plan.StructureNodeId` first, then scope against that node.

### Caching

- **Adopt the `<entity>:object:{id}` pattern.** Cache the shared payload, scope-check on read.
- **Never cache lists keyed by user/scope.** The visibility cache layer belongs elsewhere.
- **Invalidate by id on mutation.** Do not implement broader sweeps.

---

## 10. Query & Traversal Expectations

| Need | Method | Behavior |
|---|---|---|
| Course by id | `CourseService.GetByIdAsync` | Cache-aside on `course:object:{id}`. |
| All active courses | `CourseService.GetActiveAsync` | Not cached; repository scans. |
| Course code uniqueness | `ICourseRepository.CodeExistsAsync` | Used both in create precheck and as schema constraint. |
| Plan by id (with composition) | `AcademicPlanService.GetByIdAsync` | Cache-aside; eager-loads `PlanCourses`; scope-checked. |
| Plans for a node | `AcademicPlanService.GetForStructureNodeAsync` | Slim list; **no eager composition** (avoids N+1). |
| Add course to plan | `AcademicPlanService.AddCourseAsync` | Validator + scope check + existence + uniqueness (`ContainsCourseAsync`). |
| Remove course entry | `AcademicPlanService.RemoveCourseAsync` | Resolves entry, scope-checks via owning plan, deletes. |

**Read pattern contract:**
- Single-id reads are cached and scope-checked.
- List reads are slim and uncached.
- Composition is only loaded on by-id reads.

---

## 11. Future Expansion Considerations

### Likely / planned

- **Registration module.** Will introduce prerequisites, offerings, sections, enrollment, transcript, GPA — all with their own entities. This module must **not** absorb any of them.
- **"One active plan per node" enforcement.** Currently policy; may be promoted to a filtered unique index `(StructureNodeId) WHERE IsActive = 1`. If/when this happens, the create/update paths must enforce a "deactivate the previous active plan" handshake to avoid mid-transaction conflicts.
- **Plan copy / clone.** A common request — copy a plan as a new version with `EffectiveFrom = next year`. Add a dedicated service method; do not let callers reach in via repository.
- **Plan publication workflow.** Currently `IsActive` is a single boolean; a `Draft → Review → Published → Archived` state machine may be introduced. Implement as an enum on `AcademicPlan`, keep `IsActive` as a projection if needed for back-compat.
- **Localization of `Course.Title` and `AcademicPlan.Name`.** Both are single-string today. Localization (likely Arabic + English given the codebase) will require either translation columns or a side table — design it consistently with the rest of the system.

### Risk areas

- **Course deletion vs plan integrity.** Currently `CourseService.DeleteAsync` performs hard delete without checking plan references. If a course is referenced, this will throw at the DB level or silently orphan. **Add a usage-guard** before exposing this in production UIs. (Likely candidate: an `ICourseRepository.IsReferencedAsync` check.)
- **Cache staleness across scope changes.** If a `StructureNode` is moved into a scope a user can suddenly access, the cached plan payload remains scope-keyed by `StructureNodeId` — the scope check re-runs, so reads stay correct. But if a *node's identity* gains/loses access via a permission change, that is handled at the scope layer, not here. This is by design but worth remembering.
- **Schema drift between domain validators and DB.** `Level 1..10` and `Semester 1..4` are validator-only. Inserting outside these ranges via raw SQL will succeed; the schema is permissive.

### Things to NOT do during expansion

- Do not add `Prerequisites` / `Corequisites` / `Restrictions` collections to `Course`.
- Do not add `StudentId` or any per-student field to `AcademicPlan` or `AcademicPlanCourse`.
- Do not add an EF navigation property from `AcademicPlan` to `StructureNode` for "convenience".
- Do not add a `FacultyId` / `DepartmentId` / `ProgramId` typed FK as a shortcut to the structure module — the only correct anchor is `StructureNodeId`.
- Do not stop caching by single-id, scope-checked pattern. Do not introduce list-level caching keyed by user.

---

## 12. Domain Abstractions vs Concrete Rules

| Concept | Status | Why |
|---|---|---|
| `Course` as a catalog row with `Code`, `Title`, `CreditHours`, `Category` | Concrete | These are stable, school-wide facts. |
| `CourseCategory` enum values | Stable contract | Stored as `int`; reordering breaks data. New values are additive. |
| `AcademicPlan` as a *container* anchored at one node | Concrete | The plan must own one node; that part is fixed. |
| *Which node type* a plan sits under | **Abstract** | Intentionally policy-driven; may evolve. |
| `Level` / `Semester` semantics | **Abstract** | Plan-relative integers; no fixed real-world meaning. |
| `IsMandatory` as a curriculum classification | Concrete (within plan composition) | But it does NOT model graduation requirements. |
| `EffectiveFrom`/`EffectiveTo` as catalog-year windows | Concrete | Inclusive bounds, `EffectiveTo` open-ended when null. |
| "One active plan per node" | **Policy, not invariant** | Schema permits multiple; application currently allows it. |
| Scope checks via `IEffectiveScope` | Concrete | Every read & mutation goes through it. |
| Cache key layout `course:object:{id}` / `academicplan:object:{id}` | Concrete contract | Documented in `docs/caching-strategy.md`. |

---

## 13. Recommended AI Context Summary (compressed)

> **Drop-in context block for future prompts. ~190 tokens.**

```
Courses + Academic Plans module owns ONLY: a school-wide course catalog
(`Course`: globally unique upper-cased `Code`, `Title`, `CreditHours` 0..12,
`CourseCategory` int-enum, `IsActive`) and curriculum composition
(`AcademicPlan` with `StructureNodeId` FK-by-id-no-nav, `Name`,
`EffectiveFrom`/`EffectiveTo`, `IsActive`, `RowVersion` for optimistic
concurrency; composition via `AcademicPlanCourse(CourseId, Level 1..10,
Semester 1..4, IsMandatory)` with UNIQUE(`AcademicPlanId,CourseId`),
cascade-deleted with the plan). OUT OF SCOPE — prerequisites, enrollment,
offerings, transcripts, GPA, scheduling: all belong to a future Registration
module and MUST NOT be added here. Plans bind to the hierarchy via an
opaque `StructureNodeId` — never assume that node is a Program/Department/
etc. Multiple plans per node are schema-legal; "one active per node" is
policy-only. `Level`/`Semester` are plan-relative — no fixed real-world
semantics. Cache pattern: `<entity>:object:{id}` cache-aside, scope-checked
on read via `IEffectiveScope.CanAccessStructureNodeAsync`. Mutations
invalidate by id; lists are uncached. Course deletion is hard and lacks a
usage guard against plan references — verify before exposing.
```

---
