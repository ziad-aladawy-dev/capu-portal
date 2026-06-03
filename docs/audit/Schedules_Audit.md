# Schedule Module — Documentation & Logic Verification Audit

**Model document:** `docs/Schedules_Model.md`
**Implementation:** `src/4.Modules/CapitalUniversity.Module.Schedule` (+ `.Schedule.Abstractions`)
**Branch:** `sync_platform_hardening`
**Audited:** 2026-06-02

---

## 1. Model Extract

**Purpose.** A passive timetable-metadata module. A `ScheduleSlot` describes one
scheduled session attached to a `CourseOffering`: *when* (day + start/end time),
*where* (location), and *what type* (session kind). It exists to provide
timetable data safely, nothing more (Model §Goal, lines 5–21).

**Assumptions.**
- One `CourseOffering` may own many `ScheduleSlot`s (§Core Concept, line 42).
- The module is descriptive/passive/metadata-focused and is explicitly **not** a
  scheduling engine, registration engine, conflict engine, or calendar
  orchestrator (§Important Architectural Direction, lines 48–59).
- Future modules consume schedule data for clash detection / registration, but
  Schedule itself must not own those behaviors (lines 61–69).

**Invariants the module *should* validate (§Important Invariants, lines 182–186).**
1. `EndTime > StartTime`.
2. Valid `DayOfWeek`.
3. Slot belongs to an existing `CourseOffering`.
4. Optional duplicate prevention "if requested later".

**Invariants the module *should not* validate (lines 188–195).**
- Student / instructor / room / semester conflicts; registration eligibility.

**Expected minimal shape (§Minimal Version, lines 124–133).**
`CourseOfferingId`, `DayOfWeek`, `StartTime`, `EndTime`, `SessionType`,
`Location?`, `DeliveryMode?`, `Notes?`.

**Responsibilities.** Persistence, querying, invariant validation, future-safe
structure (lines 135–141).

**Dependencies (§Allowed Dependencies, lines 90–101).** May reference
`CourseOffering` + shared abstractions/common infra. Must **not** depend on
Students, Registration, Payments, Fees, Transcript, Attendance, Notifications.

**Critical design rules.**
- Rule 1 — descriptive only; does not decide conflicts (lines 201–209).
- Rule 2 — no `ScheduleOrchestrator` / `TimetableEngine` / `ConflictResolver` /
  `CalendarCoordinator` (lines 212–220).
- Rule 3 — no speculative abstractions / recurrence engines / DSLs (224–232).
- Rule 4 — keep `CourseOffering -> ScheduleSlots`; correct direction (236–246).

**Deferred-until-requested (Full Version, lines 145–176).** Recurrence, room
metadata, instructor metadata, delivery metadata, and **synchronization
metadata (external ids, sync timestamps, source systems)** — "DO NOT implement
these unless explicitly requested" (line 176).

**Performance direction (lines 299–314).** Read-heavy, admin-managed. "Do NOT
add: caching layers, distributed scheduling systems, event streaming, real-time
timetable synchronization — unless actual production pressure appears."

**State transitions.** None specified in the model. The model describes no
draft/published/closed lifecycle for a slot.

---

## 2. Implementation Files

| File | Role |
|------|------|
| `Domain/ScheduleSlot.cs` | Entity, invariants, closable lifecycle |
| `Schedule.Abstractions/Schedule/ScheduleSlotKind.cs` | Session-type enum |
| `Persistence/Configurations/ScheduleSlotConfiguration.cs` | EF mapping, indexes |
| `Application/ScheduleSlotService.cs` | CRUD, scope, conflict policy, cache, outbox |
| `Application/Validators/ScheduleSlotValidators.cs` | Create/Update FluentValidation |
| `Repositories/ScheduleSlotRepository.cs` | Queries, `HasConflictAsync`, bulk delete |
| `Application/Outbox/ScheduleSlotEventHandler.cs` | Lifecycle + orphan-cleanup handlers |
| `Schedule.Abstractions/Manifest/SchedulePermissionManifest.cs` | Permission surface |
| `ScheduleModuleExtensions.cs` | DI registration |

---

## 3. Per-Assumption Verification

| # | Assumption (Model) | Expected Behavior | Actual Implementation | Match |
|---|--------------------|-------------------|-----------------------|-------|
| 1 | `EndTime > StartTime` | Enforced as an invariant | `ScheduleSlot.SetTimeRange` rejects `end <= start` (ScheduleSlot.cs:94–103); both validators re-check (ScheduleSlotValidators.cs:13–16, 37–41) | ✅ |
| 2 | Valid `DayOfWeek` | Only legal weekday values | Native `DayOfWeek` enum, `HasConversion<int>` (ScheduleSlot.cs:42; Config:22). No explicit range guard, but the CLR enum type constrains the API surface | ⚠️ |
| 3 | Slot belongs to existing offering | Existence checked | Every op calls `_offerings.GetByIdAsync(...)`; create throws `NotFound` if missing (Service.cs:151–154, 133–136, 488–496) | ✅ |
| 4 | Optional duplicate prevention | Allowed if requested | Exact-tuple `ExistsAsync` + unique index `(CourseOfferingId, DayOfWeek, StartTime, EndTime)` (Repo:94–101; Config:42–43) | ✅ |
| 5 | Must **not** validate student/instructor/room/semester conflicts | None of these checks present | None present. No student/instructor/room/semester references anywhere in module | ✅ |
| 6 | Minimal shape: `SessionType` | A session-type field | Present, but named `Kind` (`ScheduleSlotKind`), not `SessionType` (ScheduleSlot.cs:50) | ⚠️ |
| 7 | Minimal shape: `DeliveryMode?` (optional) | An optional delivery-mode field | **Absent** — no `DeliveryMode` on entity, config, DTO, or response map | ❌ |
| 8 | Minimal shape: `Location?`, `Notes?` | Optional free text | Present, max 128 / 512, bilingual-JSON normalized (ScheduleSlot.cs:53,56; Config:32–33) | ✅ |
| 9 | Rule 1 — descriptive only; does not decide conflicts | No conflict adjudication | Module **does** reject overlapping slots (`HasConflictAsync`, half-open, Service.cs:179–183) — see Divergence 1 | ⚠️ |
| 10 | Rule 2 — no orchestrator/engine/resolver classes | None exist | None exist. No `ScheduleOrchestrator`/`TimetableEngine`/`ConflictResolver`/`CalendarCoordinator` | ✅ |
| 11 | Rule 3 — no speculative abstractions | No recurrence engine / DSL / plugin | None present | ✅ |
| 12 | Rule 4 — `CourseOffering -> ScheduleSlots` | Slot owned by offering, correct direction | `CourseOfferingId` FK-by-id, no nav, set at creation, never reassigned (ScheduleSlot.cs:39–40; Config:15–20) | ✅ |
| 13 | Allowed deps only (CourseOffering + shared infra) | No Students/Payments/etc. | Depends on `ICourseOfferingService` + core cross-cutting only (Service.cs:1–17; ModuleExtensions.cs:23–25) | ✅ |
| 14 | Sync metadata — do **not** implement unless requested | No external-id / sync fields | `IExternallySourced` + `ExternallySourced` block + `ExternalId` unique index present (ScheduleSlot.cs:30,37; Config:47–57) — see Divergence 2 | ❌ |
| 15 | Performance — do **not** add caching unless production pressure | No cache layer | Shared-object cache, 15-min TTL (Service.cs:67–68, 104–128) — see Divergence 3 | ❌ |
| 16 | No state-lifecycle specified | Slot has no draft/closed lifecycle | `IsClosed`/`Close`/`Reopen` + `CloseRecordAsync`/`OpenRecordAsync` present (ScheduleSlot.cs:63–86; Service.cs:427–445) — see Divergence 4 | ❌ |

---

## 4. Divergence Blocks

### Divergence 1 — Overlap conflict detection vs. "not a conflict engine"

- **Model.** "The Schedule module is intentionally NOT responsible for: …
  conflict resolution" (lines 12–14). "The module is NOT: … a conflict engine"
  (53–57). Rule 1: "It does NOT decide … whether a conflict exists" (lines
  201–209). The module *should not* validate student/instructor/room/semester
  conflicts (188–193).
- **Implementation.** `ScheduleSlotRepository.HasConflictAsync` runs a half-open
  interval overlap query — `StartTime < end && EndTime > start` — scoped to the
  same `(CourseOfferingId, DayOfWeek)` (Repo:103–129). `CreateAsync` /
  `UpdateAsync` / `BatchCreateAsync` reject overlaps with
  `ConflictException(SlotConflict)` inside a SERIALIZABLE transaction that closes
  the TOCTOU window (Service.cs:166–203, 246–264, 383–399).
- **Evidence.** Service.cs:179–183; Repo:111–128; Config:39–43.
- **Impact.** The module rejects two time-overlapping slots **within one
  offering on one day**. This is *offering-local* overlap prevention, not any of
  the cross-entity conflicts the model forbids (student/instructor/room/
  semester) — those are absent. The model itself carves out "optional duplicate
  prevention if requested later" (line 186) and lists "overlapping-slot policy
  (if implemented)" as a valid test (line 265), so the behavior sits inside the
  permitted optional-policy envelope. The tension is with the model's strong
  Rule 1 wording ("does NOT decide whether a conflict exists"), which the literal
  implementation contradicts even though the *kind* of conflict is the allowed
  intra-offering one.
- **Severity.** Low.
- **Notes.** Behavior is internally consistent and race-safe; the divergence is
  one of documentation framing, not a logic defect. The DB unique index guards
  exact duplicates only; the service comment correctly notes the index does not
  model overlap (Config:39–43; Service.cs:156–164).

### Divergence 2 — Synchronization metadata implemented despite "DO NOT implement unless requested"

- **Model.** Synchronization metadata (external system identifiers, sync
  timestamps, source systems) is a Full-Version/future item: "DO NOT implement
  these unless explicitly requested" (lines 171–176).
- **Implementation.** `ScheduleSlot : … IExternallySourced` carries an
  `ExternallySourced` block (ExternalId, ExternalUpdatedAt, ExternalVersion,
  LastSyncedAt, OriginSystem), mapped via `OwnsOne` with a filtered unique
  `ExternalId` index (ScheduleSlot.cs:30,37; Config:47–57).
- **Evidence.** ScheduleSlot.cs:30–37; ScheduleSlotConfiguration.cs:47–57.
- **Impact.** The entity participates in the platform sync gateway. This is the
  same cross-cutting `IExternallySourced` pattern applied to every syncable
  entity on the `sync_platform_hardening` branch, so the deviation is a
  deliberate platform-wide decision rather than a one-off. Still, it is a literal
  violation of an explicit model prohibition; the model has not been updated to
  reflect that the platform-hardening work opted every module into sync.
- **Severity.** Low.
- **Notes.** No functional risk — provenance fields are inert unless the sync
  write gateway populates them. Recommend reconciling the model to acknowledge
  the platform-wide sync provenance contract.

### Divergence 3 — Caching layer present despite "Do NOT add caching layers"

- **Model.** §Performance Direction: "Do NOT add: caching layers … unless actual
  production pressure appears" (lines 308–314).
- **Implementation.** `ScheduleSlotService` uses `ICacheService` with prefix
  `schedule-slot:object:` and a 15-minute TTL; reads populate, writes/close/open
  evict (Service.cs:67–68, 104–128, 271, 280, 434, 444).
- **Evidence.** Service.cs:63–68, 111–128, 270–272.
- **Impact.** The cache is the platform's standard culture-neutral shared-object
  cache (raw DTO cached, localize-on-read), matching Invoice/AcademicPlan. Scope
  re-check still runs on every cache hit (Service.cs:115, 124), so a revoked
  grant cannot serve stale data — no authorization regression. The divergence is
  again model-vs-platform: the doc forbids a cache the platform pattern adds.
- **Severity.** Low.
- **Notes.** Correctness is sound; only the model's "no caching" directive is
  contradicted.

### Divergence 4 — Closable lifecycle + permission `Open` verb, undocumented and self-contradicting comment

- **Model.** Specifies no slot lifecycle; a slot is plain timetable metadata.
- **Implementation.** `ScheduleSlot` has `IsClosed`/`ClosedAt`, `Close()`,
  `Reopen()`, and `EnsureMutable()` gating all writes (ScheduleSlot.cs:63–103);
  the service exposes `CloseRecordAsync` / `OpenRecordAsync` (Service.cs:427–445)
  and blocks edits on closed rows via `LoadForWriteAsync` (Service.cs:498).
- **Evidence.** ScheduleSlot.cs:58–103; Service.cs:427–445, 488–501.
- **Impact (lifecycle).** Adds an undocumented draft/closed-style lifecycle to a
  module the model frames as stateless metadata. Functionally self-consistent
  (close → immutable → reopen), but absent from the model.
- **Secondary defect (manifest comment).** `SchedulePermissionManifest` builds
  its resource with `ResourceDefinition.WithCrudActions(...)`
  (SchedulePermissionManifest.cs:22–25), which **includes** the `Open` verb
  (`View < Insert < EditClose < Open < Delete`, ResourceDefinition.cs:56–63).
  Yet the manifest's own XML doc-comment states: *"No `Open` verb: schedule slots
  have no draft/published lifecycle in this iteration."*
  (SchedulePermissionManifest.cs:10–11). The comment is factually wrong on two
  counts: (a) the factory it calls **does** emit an `Open` verb — the variant
  that omits it is `WithCrudActionsNoOpen` (ResourceDefinition.cs:70–82), which
  is *not* used; and (b) the slot **does** have a close/open lifecycle. So the
  `Open` permission is in fact correctly provisioned for `OpenRecordAsync`; only
  the comment is stale/contradictory.
- **Severity.** Low (lifecycle undocumented); Low (misleading comment, no runtime
  effect — the permission surface is actually correct for the implemented
  lifecycle).
- **Notes.** This is the inverse of a gap: the code is consistent (lifecycle
  exists ⇒ `Open` verb exists), but the comment denies both. Fixing the comment
  (or the model) resolves it.

### Divergence 5 — `SessionType`/`DeliveryMode` naming & field gaps

- **Model.** Minimal shape names `SessionType` and an optional `DeliveryMode?`
  (lines 130, 132). Session-type examples: Lecture, Lab, Tutorial, Practical,
  Exam Session (lines 113–116).
- **Implementation.** Field is `Kind : ScheduleSlotKind` (not `SessionType`).
  Enum values: `Lecture, Lab, Tutorial, Seminar, Exam, Other`
  (ScheduleSlotKind.cs:9–17) — "Practical" is absent, `Seminar`/`Other` added.
  `DeliveryMode?` is not implemented at all.
- **Evidence.** ScheduleSlot.cs:50; ScheduleSlotKind.cs:9–17.
- **Impact.** `SessionType` vs `Kind` is cosmetic naming. The enum list is
  labeled "Examples" in the model (non-binding). `DeliveryMode?` is an
  explicitly *optional* minimal-shape field; its absence narrows the minimal
  surface but breaks no invariant — delivery mode is also a Full-Version
  "Delivery Metadata" item (lines 166–169) deferred until requested, so the
  model is internally ambivalent about it.
- **Severity.** Low.
- **Notes.** No corrective action required beyond optional model/code naming
  alignment.

### Divergence 6 — Outbox lifecycle events (undocumented, but within passive contract)

- **Model.** No event/outbox mechanism described; module is passive.
- **Implementation.** Create/Update/Delete/Close/Open enqueue
  `schedule.slot.{created|updated|deleted}` outbox facts on the same DbContext
  (Service.cs:200, 268, 278, 432, 442, 508–518). Handlers **log only** — no
  business logic (ScheduleSlotEventHandler.cs:29–64). A `CourseOfferingDeleted`
  consumer cleans up orphan slots via `DeleteForOfferingAsync`
  (ScheduleSlotEventHandler.cs:117–161; Repo:138–158).
- **Evidence.** Service.cs:503–518; ScheduleSlotEventHandler.cs:40–64, 134–153.
- **Impact.** Events are audit/log sinks and an orphan-reaper; the handler doc
  explicitly forbids business logic in sinks (ScheduleSlotEventHandler.cs:23–27),
  so the passive-metadata contract is preserved. Undocumented in the model but
  not a behavioral violation; the orphan cleanup is a correctness *benefit*
  (compensates for the deliberate no-FK design).
- **Severity.** Low.

---

## 5. Hidden-Logic Review

- **TOCTOU on overlap insert.** Concurrent `CreateAsync` calls for the same
  `(offering, day)` could each pass the overlap query then both insert. Guarded:
  the conflict-check + insert run inside
  `ExecuteInSerializableTransactionAsync`, forcing the second writer to block on
  the range lock until the first commits (Service.cs:156–203). The unique index
  catches only exact duplicates; the SERIALIZABLE wrapper is what catches
  partial overlaps. **Verified safe.**
- **Self-match on update.** A no-op or single-field edit could make the overlap
  check match the row against itself. Guarded by `tupleMoved` short-circuit
  (skips checks when day/start/end unchanged) and `excludeId: slot.Id` on the
  overlap query (Service.cs:246–264; Repo:123–126). **Verified safe.**
- **Cache staleness after revoked scope.** Cache hit path still calls
  `_offerings.GetByIdAsync` before returning, so a revoked structure-node grant
  hides the slot even on a warm cache (Service.cs:111–116, 124). **Verified
  safe.**
- **Orphan slots after offering delete.** No DB FK (deliberate, Config:15–20).
  Compensated by `CourseOfferingDeletedHandler` → `DeleteForOfferingAsync`
  (`ExecuteDeleteAsync` on relational, idempotent) (ScheduleSlotEventHandler.cs:
  134–153; Repo:138–158). **Verified** — but depends on the CourseOffering
  producer actually enqueuing `course_offering.deleted`; the handler comment
  notes the producer "when … ships a delete path" (line 156–159), i.e. the
  producer side is assumed, not verified here. No defect proven in the Schedule
  module itself.
- **`SearchAsync` total-count vs. per-row visibility.** Cross-offering search
  filters materialized rows by parent visibility but reports the raw
  `TotalCount` (Service.cs:301–321). Documented as intentional (admins typically
  unrestricted). Could over-report totals for a scope-limited caller, but this
  matches the established project pattern and is explicitly commented. **No
  verified defect.**

No other verified issue found.

---

## 6. Flow Verification

**Flow: Create a schedule slot**
- *Expected (model).* Validate `EndTime > StartTime` + valid day; confirm parent
  offering exists; persist; optionally prevent duplicates.
- *Actual.* Validate payload → scope/existence check on offering → SERIALIZABLE
  txn { exact-duplicate check → overlap check → `SetTimeRange` invariant → insert
  → enqueue lifecycle fact → save } → return id (Service.cs:142–205).
- *Match.* YES.
- *Differences.* Adds overlap rejection (Divergence 1) + outbox fact (Divergence
  6) beyond the minimal model.

**Flow: Update a slot**
- *Expected.* Apply changes, keep `EndTime > StartTime`, keep parent valid.
- *Actual.* Validate → load tracked + scope check + `EnsureMutable` → compose
  start/end through invariant → conditional duplicate/overlap re-check only if
  the unique tuple moved → save → evict cache (Service.cs:207–272).
- *Match.* YES.
- *Differences.* Closed slots are immutable (Divergence 4); overlap re-check
  (Divergence 1).

**Flow: Delete a slot**
- *Expected.* Remove the row.
- *Actual.* Load + scope check → remove → enqueue deleted fact → save → evict
  (Service.cs:274–281).
- *Match.* YES.

**Flow: Offering deleted (orphan cleanup)**
- *Expected (model).* Not specified (no FK guidance).
- *Actual.* Consumer handler bulk-deletes all slots for the offering, idempotent
  (ScheduleSlotEventHandler.cs:117–153; Repo:138–158).
- *Match.* N/A (beyond model) — correctness benefit.

---

## 7. Final Verdict

### Scores
- **Architecture fidelity:** 8/10 — Rules 1–4 honored in structure (FK-by-id
  correct direction, no orchestrator/engine classes, allowed deps only). Points
  off for three platform features (sync provenance, cache, outbox) the model
  explicitly defers/forbids, plus an undocumented lifecycle.
- **Logic correctness:** 9/10 — invariants enforced at the entity (single source
  of truth), race conditions closed with SERIALIZABLE + `excludeId` + cache
  re-check. No proven logic defect.
- **Operational safety:** 9/10 — TOCTOU guarded, orphan cleanup present, scope
  re-checked on cache hits, partial-success batch isolation. Minor reliance on an
  external producer for orphan-cleanup events.
- **Maintainability:** 8/10 — clear comments and conventions; one stale/wrong
  manifest comment (Divergence 4) and model drift on sync/cache/lifecycle reduce
  it.

### Confirmed Issues
1. **Manifest comment is factually wrong** (Divergence 4): claims "No `Open`
   verb … no draft/published lifecycle" while calling `WithCrudActions` (which
   emits `Open`) on an entity that *does* have a close/open lifecycle.
   No runtime impact — the permission surface is actually correct — but the
   comment misleads maintainers. (SchedulePermissionManifest.cs:10–11 vs.
   ResourceDefinition.cs:56–63.)
2. **`DeliveryMode?` from the minimal shape is unimplemented** (Divergence 5,
   item 7) — optional field, no invariant impact.

### Model Violations (literal, low-risk — model is stale vs. platform direction)
- Sync provenance implemented despite "DO NOT implement unless requested"
  (Divergence 2).
- Caching layer added despite "Do NOT add caching layers" (Divergence 3).
- Undocumented closable lifecycle + outbox events (Divergences 4, 6).
- Overlap conflict detection vs. Rule 1's "does not decide conflicts" wording —
  though within the model's own "optional overlapping-slot policy" carve-out
  (Divergence 1).

### False Positives Cleared
- **"No FK to CourseOffering = orphan risk"** — deliberate modularity rule, fully
  compensated by `CourseOfferingDeletedHandler` + `DeleteForOfferingAsync`. Not a
  defect.
- **Overlap rejection = forbidden conflict engine** — the implemented check is
  intra-offering duplicate/overlap prevention, explicitly permitted by the model
  (lines 186, 265); the forbidden cross-student/instructor/room/semester checks
  are all absent.
- **`Open` permission verb present** — correct, because the lifecycle exists; the
  only error is the comment that denies it.

### Findings Summary
The Schedule module is a **faithful and well-engineered implementation** of the
model's core intent: a passive, offering-attached timetable with entity-enforced
invariants and race-safe writes. It **resolves the CourseOffering audit's
deferred Divergence 2** — `ScheduleSlot.CourseOfferingId` (FK-by-id, no nav,
never reassigned) confirms the correct `CourseOffering -> ScheduleSlots`
direction (Rule 4 ✅).

Every divergence is **low severity** and falls into two buckets: (1) **model
staleness** — the `sync_platform_hardening` branch opted the module into
platform-wide cross-cutting features (sync provenance, shared-object cache,
outbox lifecycle, closable records) that the model document still lists as
deferred or forbidden; and (2) a **single misleading manifest comment** that
contradicts both the factory it calls and the entity's own lifecycle. No logic
or operational-safety defect was proven. Recommended (non-code) follow-ups:
update `Schedules_Model.md` to acknowledge the platform sync/cache/lifecycle
contracts, and correct the `SchedulePermissionManifest` comment.
