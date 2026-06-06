# Course Offering Module — Verification Audit

**Doc:** `docs/CourseOffering_Model.md`
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Represent a runtime academic offering of a course — a course opened for registration, in a specific term, for a specific academic context (`StructureNode`), with runtime registration behavior. Not the static course definition.
- **Owns:** runtime availability, registration state, academic targeting, offering-level constraints, offering lifecycle.
- **Does NOT own:** schedule conflict logic, transcript, fee, prerequisite engine, workflow orchestration, timetable details.
- **Isolation:** may reference Courses, AcademicTerms, StructureNodes; must NOT manipulate students, fees, transcripts, payments, or orchestrate workflows.
- **`OfferingStatus`:** `Draft=0, Open=1, Closed=2, Cancelled=3`.
- **ScheduleSlot direction:** `ScheduleSlot` must belong to `CourseOffering` (not `Course → Schedule`).
- **Minimal initial model:** entity, core relationships, minimal lifecycle, safe extension points; follow existing base-entity / EF-config / validation / manifest conventions; avoid speculative abstractions.
- **Manifest:** lightweight permission registration + module metadata; no plugin/dynamic-loading frameworks.
- **Future (do NOT implement now):** instructor, room, online/hybrid delivery, registration windows.

## Step 2 — Implementation Files

- `Module.CourseOffering/Domain/CourseOffering.cs` (entity + lifecycle state machine)
- `Module.CourseOffering.Abstractions/CourseOffering/{OfferingStatus, RegistrationState, ICourseOfferingService, DTOs/*}.cs`
- `Module.CourseOffering/Application/CourseOfferingService.cs`
- `Module.CourseOffering/Application/Validators/CourseOfferingValidators.cs`
- `Module.CourseOffering/Repositories/{ICourseOfferingRepository, CourseOfferingRepository}.cs`
- `Module.CourseOffering/Persistence/Configurations/CourseOfferingConfiguration.cs`
- `Module.CourseOffering.Abstractions/Manifest/CourseOfferingPermissionManifest.cs`

## Step 3 — Verification Table

| Assumption | Expected | Actual Implementation | Match |
| --- | --- | --- | --- |
| `OfferingStatus` enum values | `Draft=0,Open=1,Closed=2,Cancelled=3` | exact (`OfferingStatus.cs:9-12`) | ✅ |
| Offering = runtime availability bridge, not static course | distinct entity | `CourseOffering : BaseEntity, ISoftDeletable, IExternallySourced` referencing Course/Semester/Node by id (`CourseOffering.cs:27-43`) | ✅ |
| References Course, AcademicTerm, StructureNode (by id, no nav) | FK-by-id only | `CourseId/SemesterId/StructureNodeId`; FKs `HasOne<...>().WithMany()` no nav (`CourseOfferingConfiguration.cs:42-55`) | ✅ |
| Cross-module FKs `OnDelete=Restrict` | cannot remove underneath | Course/Semester/StructureNode all `Restrict` (`:45,50,55`) | ✅ |
| Owns offering lifecycle | state machine | `Activate/Close/Cancel/Reopen` with legal-transition guards on entity (`CourseOffering.cs:165-211`) | ✅ |
| Owns registration state | accept/closed/waitlist | `RegistrationState` enum + `OpenRegistration/CloseRegistration/SetWaitlist` gated on `Status==Open` (`CourseOffering.cs:214-240`) | ✅ |
| Owns offering-level constraints (capacity) | non-negative, no shrink below count | `InitializeCapacity`/`AdjustCapacity` reject `<0` and `<RegisteredCount` (`:100-121`) | ✅ |
| Does NOT own registration engine | counter only, no orchestration | `RegisteredCount` private-set; in-memory `IncrementRegistration` + repo `TryIncrementRegistrationAsync` primitive; comments delegate registration rules to future module (`CourseOffering.cs:51,123-148`; `CourseOfferingRepository.cs:135-175`) | ✅ |
| Does NOT manipulate students/fees/transcripts/payments | no such coupling | service injects only UoW + repo + validators + `IEffectiveScope` (`CourseOfferingService.cs:30-48`) | ✅ |
| Schedules NOT in the entity | ScheduleSlot is a child, not here | no schedule fields; only a future-readiness comment for a `Slots` child (`CourseOffering.cs:242-247`) | ✅ |
| Scope enforced on reads/mutations | node + term scope | every path checks `CanAccessStructureNodeAsync` + `CanAccessSemesterAsync`; out-of-scope → null/NotFound (`CourseOfferingService.cs:50-55,289-297`) | ✅ |
| Section uniqueness per (course,term,node) | one section code | unique filtered index `(CourseId,SemesterId,StructureNodeId,SectionCode) WHERE IsDeleted=0` + `SectionExistsAsync` precheck (`CourseOfferingConfiguration.cs:60-62`; `CourseOfferingService.cs:121,177`) | ✅ |
| Optimistic concurrency on co-edits | rowversion | `RowVersion IsRowVersion()`; `Try*Registration` retry on `DbUpdateConcurrencyException` (`:32`; `CourseOfferingRepository.cs:135-200`) | ✅ |
| Soft delete | `ISoftDeletable` + filter | `HasQueryFilter(!IsDeleted)` (`CourseOfferingConfiguration.cs:37`) | ✅ |
| Manifest is minimal CRUD permission descriptor | lightweight | one resource, CRUD actions, no plugin infra (`CourseOfferingPermissionManifest.cs:12-26`) | ✅ |
| Follows existing patterns (base entity, EF config, validators) | convention reuse | mirrors `AcademicPlan`/`Invoice` patterns; FluentValidation validators (`CourseOfferingValidators.cs`) | ✅ |
| "Intentionally minimal initial model" | entity + core rels + minimal lifecycle | current build adds RegistrationState, capacity/count invariants, Search, Bulk publish/cancel, sync provenance — beyond first-cut minimal | ⚠️ (Divergence 1) |
| Module references "AcademicTerms" | term entity | implemented as `Semester` (`SemesterId`, `HasOne<Semester>`) (`CourseOfferingConfiguration.cs:47-50`) | ✅ (naming Note) |
| `ScheduleSlot` belongs to `CourseOffering` | child relationship exists | not implemented in this module (comment only); resolution depends on Schedule module wiring | ⚠️ (Divergence 2 — cross-module, pending) |

## Step 4 — Divergence Analysis

### Divergence 1 — Implementation exceeds the "intentionally minimal initial model"
- **Model:** §"Minimal Initial Model" — establish only the entity, core relationships, minimal lifecycle, and safe extension points; "Avoid speculative abstractions. Avoid introducing infrastructure that the project does not already use."
- **Implementation:** The module is mature, not first-cut: a full `RegistrationState` enum + transition guards, capacity/`RegisteredCount` invariants with optimistic-concurrency `Try*RegistrationAsync` primitives, paged `SearchAsync` with sort whitelist, `BulkPublishAsync`/`BulkCancelAsync`, sync provenance (`IExternallySourced`), soft delete, and rowversion.
- **Evidence:** `CourseOffering.cs:48-240`; `CourseOfferingService.cs:329-426`; `CourseOfferingRepository.cs:135-200`.
- **Impact:** Documentation/Low. Every added piece falls **within** the doc's enumerated owned responsibilities (runtime availability, registration state, offering-level constraints, lifecycle) and reuses existing project infrastructure (no speculative frameworks). The divergence is that the doc describes a first iteration the code has already grown past — the model reads as a snapshot, not the current state.
- **Severity:** Low
- **Notes:** No violation of the "must NOT own" list. The registration **counter** + concurrency primitive sit at the boundary of "not a registration engine"; the code stays on the correct side by exposing only a guarded count primitive and explicitly delegating registration policy to the future module.

### Divergence 2 — `ScheduleSlot → CourseOffering` relationship not present in this module (pending)
- **Model:** "ScheduleSlot must belong to CourseOffering … Correct: CourseOffering → ScheduleSlot."
- **Implementation:** `CourseOffering` has no `ScheduleSlot`/`Slots` child collection — only a future-readiness comment describing how it *would* attach (`CourseOffering.cs:242-247`). The directional rule is therefore neither satisfied nor violated **within this module**; correctness depends on whether the Schedule module models `ScheduleSlot.CourseOfferingId` (audited separately — Task 7).
- **Evidence:** `CourseOffering.cs:242-247`; absence of any `ScheduleSlot` type under `Module.CourseOffering/**`.
- **Impact:** Operational/Low (deferred). The doc presents the direction as a core architectural constraint, but also instructs "Do NOT implement these now" for downstream concerns. Resolution is cross-module.
- **Severity:** Low
- **Notes:** Flag for cross-check against `docs/audit/Schedules_Audit.md` — the slot must reference `CourseOfferingId`, not `CourseId`.

## Step 5 — Hidden-Logic Review

- **Last-seat race (`TryIncrementRegistrationAsync`):** load → in-memory guard (`Cancelled` / `RegisteredCount >= Capacity`) → `SaveChanges` under rowversion; on `DbUpdateConcurrencyException`, `ChangeTracker.Clear()` + re-fetch + re-evaluate, bounded to 3 attempts (`CourseOfferingRepository.cs:135-175`). Two callers racing the final seat: only one `SaveChanges` wins; the loser reloads, sees full, returns false. **No over-increment possible. No verified issue.**
- **ChangeTracker.Clear isolation (H9 note):** clears tracker each retry so a sibling entity left `Modified` by a higher caller cannot tag along on the retry `SaveChanges` (`:152`). Correct isolation. **No verified issue.**
- **Lifecycle guards on entity, not service:** `Activate/Close/Cancel/OpenRegistration/SetWaitlist` all enforce legal transitions on the entity, so the service (and any future caller) cannot bypass via raw assignment (`init`-only setters) (`CourseOffering.cs:57-69,165-240`). Service maps `InvalidOperationException` → `ConflictException`/`IllegalStateTransition` (`CourseOfferingService.cs:203-222`). **No verified issue.**
- **Update field order:** `Status` applied before `RegistrationState` because the registration-state guard reads post-transition `Status` (`CourseOfferingService.cs:155-160`). Correct ordering. **No verified issue.**
- **Capacity shrink guard:** `AdjustCapacity` rejects `< RegisteredCount` rather than silently truncating (`CourseOffering.cs:112-121`) — surfaced as 409. **No verified issue.**
- **`Close()` side effects:** forces `RegistrationState=Closed` + sets `IsClosed`/`ClosedAt` so a closed offering can't dangle an "open registration" flag (`CourseOffering.cs:177-193`). **No verified issue.**
- **Scope on every read path including cross-node `GetForCourseAsync`/`SearchAsync`:** per-row `CanAccessStructureNodeAsync` filtering avoids leaking that a course runs under an invisible node (`CourseOfferingService.cs:95-103,351-356`). Note: `SearchAsync` keeps `TotalCount` as the raw pre-filter count (documented trade-off, `:346-350`) — a narrow-scoped reader could see `TotalCount` > visible items. Documented, not a security leak (only a count). **No verified security issue.**

## Step 6 — Flow Verification

### Create offering
- **Expected:** validate → scope (node + term) → section-uniqueness → persist; minimal lifecycle defaults.
- **Actual:** `CourseOfferingService.CreateAsync:106-144` — validator, both scope checks, `SectionExistsAsync`, `InitializeCapacity`. Matches.
- **Match:** YES.

### Update offering
- **Expected:** partial-field update through lifecycle guards; illegal transitions rejected.
- **Actual:** `UpdateAsync:146-166` + `Apply*` helpers — section re-uniqueness, capacity guard, status-then-registration transitions, passive sync metadata. Matches.
- **Match:** YES.

### Lifecycle (publish/cancel/close/reopen)
- **Expected:** state machine, no illegal jumps.
- **Actual:** entity transitions + service bulk drivers (`BulkPublish/BulkCancel`, `CloseRecord/OpenRecord`); independent per-row commit so a failed peer doesn't roll back successes (`CourseOfferingService.cs:261-426`). Matches intent.
- **Match:** YES.

### Registration count maintenance
- **Expected:** offering owns the count value; rules belong to future Registration module.
- **Actual:** entity holds guarded count; repo exposes concurrency-safe `Try*` primitives; no registration orchestration in-module.
- **Match:** YES (boundary respected).

### Schedule attachment
- **Expected:** `CourseOffering → ScheduleSlot`.
- **Actual:** Not implemented here (comment only). Deferred — see Divergence 2.
- **Match:** PARTIAL (cross-module, pending Task 7).

## Step 7 — Final Verdict

### Scores
- Architecture Match: 9/10
- Logic Correctness: 9/10
- Operational Safety: 9/10
- Maintainability: 9/10

### Confirmed Issues
1. Model doc describes an "intentionally minimal initial model"; the implementation has matured well past it (RegistrationState, capacity invariants, concurrency primitives, Search, Bulk ops, sync provenance). All within owned scope — doc is a stale snapshot — Low.
2. `ScheduleSlot → CourseOffering` direction is stated as a core rule but not realized in this module (future-comment only); correctness depends on the Schedule module — Low, deferred/cross-module.
3. Doc says references "AcademicTerms"; implemented as `Semester`/`SemesterId` — naming mismatch, semantically equivalent — Low.

### Model Violations
- None. The "must NOT own" list (registration engine, scheduling engine, transcript, workflow, fees, students, payments) is fully respected.

### False Positives
- "Owns lifecycle + registration state + offering-level constraints" — upheld (entity state machine).
- "References only Course/Term/Node by id, no nav" — upheld.
- "Schedules not in the course/offering entity" — upheld (no schedule fields).
- "Manifest minimal" — upheld (single CRUD resource).
- "Isolated module, no cross-domain manipulation" — upheld.

### Findings Summary
- **Matches:** Offering as runtime bridge, exact `OfferingStatus` enum, id-only cross-module FKs with `Restrict`, entity-owned lifecycle + registration-state machine, capacity invariants, section uniqueness, dual scope enforcement (node + term), optimistic-concurrency registration primitives, soft delete, minimal permission manifest.
- **Does not match:** Doc's "minimal initial" framing vs the mature build; `ScheduleSlot` relationship not yet present here; AcademicTerm/Semester naming.
- **Needs attention:** Refresh the model doc to reflect the current (post-minimal) feature set, and confirm in the Schedules audit that `ScheduleSlot` references `CourseOfferingId`. No security or correctness defect found — strongest-matching module audited so far.
