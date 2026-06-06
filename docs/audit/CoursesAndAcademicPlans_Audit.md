# Courses + Academic Plans Module — Verification Audit

**Doc:** `docs/CoursesAndAcademicPlans_Model.md`
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Own two concerns only — (1) school-wide course catalog (`Course`, globally unique normalized `Code`), (2) curriculum composition (`AcademicPlan` + `AcademicPlanCourse`). Explicitly out of scope: prerequisites, enrollment, offerings, transcripts, GPA, scheduling.
- **Assumptions / Invariants (doc §4, §6):**
  - `Course.Code` `nvarchar(32)` required, **unique**, CI collation, entity-normalized (trim + upper).
  - `Course.CreditHours ∈ [0,12]` (validator); `Category` int-enum stable.
  - No soft delete on courses/plans — hard delete via service.
  - `AcademicPlan.StructureNodeId` FK-by-id, `OnDelete=Restrict`, **no EF nav**.
  - `EffectiveTo > EffectiveFrom` when set; `RowVersion` optimistic concurrency.
  - `AcademicPlanCourse`: `(AcademicPlanId,CourseId)` **unique**; `(AcademicPlanId,Level,Semester)` index; `Level∈[1,10]`, `Semester∈[1,4]` (validator-only); plan→course **cascade delete**.
  - Scope: `IEffectiveScope.CanAccessStructureNodeAsync` gates plan reads/mutations; out-of-scope → `null`/NotFound; cached payload re-scope-checked on read.
  - Cache: `course:object:{id}` / `academicplan:object:{id}` cache-aside; invalidate by id on mutation; lists uncached.
  - `AcademicPlanCourse → Course`: no EF-managed FK; course hard-delete has **no usage guard** (doc flags "verify").
- **Expected flow:** id reads cached + scope-checked + composition eager-loaded; list reads slim/uncached; mutations validate → scope → mutate → invalidate id.

## Step 2 — Implementation Files

- `Core.Domain/Courses/{Course, AcademicPlan, AcademicPlanCourse, CourseCategory}.cs`
- `Core.Application/Courses/{CourseService, AcademicPlanService}.cs`
- `Core.Application/Courses/Validators/{CourseValidators, AcademicPlanValidators}.cs`
- `Core.Application/Courses/Mappings/{CourseMapper, AcademicPlanMapper}.cs`
- `Core.Application/Courses/Authorization/CoursesPermissionManifest.cs`
- `Core.Infrastructure/Persistence/Configurations/{Course, AcademicPlan, AcademicPlanCourse}Configuration.cs`
- `Core.Abstractions/Courses/{ICourseService, IAcademicPlanService, DTOs/*}.cs`

## Step 3 — Verification Table

| Assumption | Expected | Actual Implementation | Match |
| --- | --- | --- | --- |
| `Course.Code` normalized in entity setter | trim + upper | `set => _code = (value ?? "").Trim().ToUpperInvariant()` (`Course.cs:23-27`) | ✅ |
| `Code` `nvarchar(32)` unique CI collation | schema | `HasMaxLength(32)` + `UseCollation(...CI_AS)` + `HasIndex(Code).IsUnique()` (`CourseConfiguration.cs:17-35`) | ✅ |
| `Title` `nvarchar(200)` required | schema | `:22-24` | ✅ |
| `CreditHours ∈ [0,12]` | validator | `InclusiveBetween(0,12)` create + update (`CourseValidators.cs:13,26-29`) | ✅ |
| `Category` int-enum, stable, additive | `HasConversion<int>` | `:29-31`; enum 0..5 (`CourseCategory.cs:8-16`) | ✅ |
| Course `CodeExistsAsync` precheck | create guard | `:114-117` ConflictException | ✅ |
| Plan `StructureNodeId` FK Restrict, no nav | schema | `HasOne<StructureNode>().WithMany().HasForeignKey(...).OnDelete(Restrict)` (`AcademicPlanConfiguration.cs:29-32`) | ✅ |
| Plan `RowVersion` optimistic concurrency | rowversion | `IsRowVersion()` (`:23`); `byte[] RowVersion` (`AcademicPlan.cs:25`) | ✅ |
| Composite index `(StructureNodeId,IsActive)` | hot path | `HasIndex(new{StructureNodeId,IsActive})` (`:26`) | ✅ |
| `EffectiveTo > EffectiveFrom` | create + update | create validator `GreaterThan` (`AcademicPlanValidators.cs:14-17`); update re-check (`AcademicPlanService.cs:207-210`) | ✅ |
| APC unique `(AcademicPlanId,CourseId)` | schema | `HasIndex(new{AcademicPlanId,CourseId}).IsUnique()` (`AcademicPlanCourseConfiguration.cs:21-22`) | ✅ |
| APC layout index `(AcademicPlanId,Level,Semester)` | schema | `:25` | ✅ |
| Plan→APC cascade delete | cascade | `HasMany(PlanCourses)...OnDelete(Cascade)` (`AcademicPlanConfiguration.cs:34-37`) | ✅ |
| `Level∈[1,10]`,`Semester∈[1,4]` validator-only | not schema | `AddPlanCourseValidator` `InclusiveBetween` (`AcademicPlanValidators.cs:36-37`); schema has no CHECK (`AcademicPlanCourseConfiguration.cs`) | ✅ |
| Course-in-plan uniqueness precheck | `ContainsCourseAsync` | `:278-281` ConflictException | ✅ |
| Scope gates plan reads/mutations | every plan op | `CanAccessStructureNodeAsync` on GetById/Search/GetForNode/Create/Update/Delete/AddCourse/Batch/Remove/Close/Open (`AcademicPlanService.cs:83,91,102,118,142,178,197,223,268,310,392,431`) | ✅ |
| Cached plan re-scope-checked on read | invariant 7 | cache hit → `CanAccessStructureNodeAsync(cached.StructureNodeId)` else null (`:80-86`) | ✅ |
| Out-of-scope read → null (→404) | no leak | GetById returns null (`:86,91`); list returns empty (`:144`) | ✅ |
| List read no eager composition | avoid N+1 | `GetForStructureNodeAsync` slim projection, no PlanCourses (`:147-158`) | ✅ |
| By-id read eager-loads composition | full | `GetByIdAsync(...,includeCourses:true)` + ordered map (`:88,443-446`) | ✅ |
| Cache key `*:object:{id}` cache-aside, invalidate by id | per doc | prefixes `course:object:`/`academicplan:object:` + `{id:N}`; RemoveAsync on every mutation (`CourseService.cs:27,209`; `AcademicPlanService.cs:38,438`) | ✅ (key uses dashless `{id:N}` — Note) |
| Course delete is hard, no usage guard | doc §8.7/§11 | `CourseService.DeleteAsync` → `_courses.Delete` hard, no plan-reference check (`CourseService.cs:150-160`) | ✅ (doc-accurate) |
| `AcademicPlanCourse → Course` no DB FK | doc §4:109 "verify" | **confirmed: no `HasOne<Course>` anywhere**; `CourseId` is a bare required Guid (`AcademicPlanCourseConfiguration.cs:15`) | ⚠️ (Divergence 1) |
| Course owns "deliberately nothing else" | catalog only | Course also carries `IExternallySourced`/sync block + `IsClosed` lifecycle (`Course.cs:11,18,46-69`) | ❌ (Divergence 2) |
| Plan has only documented fields | per §4 | Plan also has `IsClosed`/`Close`/`Reopen` lifecycle (`AcademicPlan.cs:44-73`) | ❌ (Divergence 3) |
| Localization of Title/Name is "future" (doc §11) | single-string today | already implemented: `Code`+`Title`+`Name` decoded via `ILocalizationService`/`LocalizedJson` (`CourseService.cs:97-102`; `AcademicPlanService.cs:166-170`) | ❌ (Divergence 4) |
| Scope gates **course** reads (doc §3 "every read/write") | every read | `CourseService` injects **no** `IEffectiveScope` — catalog unscoped by design (`CourseService.cs:38-52`) | ⚠️ (Divergence 5) |

## Step 4 — Divergence Analysis

### Divergence 1 — No DB FK `AcademicPlanCourse → Course`; hard course-delete orphans composition rows
- **Model:** Doc §4 (line 109): "`AcademicPlanCourse` → `Course`: no EF-managed FK; a course cannot be deleted from the catalog while referenced (the repository's `Delete` is hard-delete… **will fail if FK constraint exists at DB level; verify before relying**)." Doc §11 risk repeats it.
- **Implementation (verified):** `AcademicPlanCourseConfiguration` declares **no** relationship to `Course` — `CourseId` is a bare `IsRequired()` `Guid` (`AcademicPlanCourseConfiguration.cs:15`). No `HasOne<Course>` exists in any configuration. `CourseService.DeleteAsync` hard-deletes with only an `EnsureMutable()` gate and **no** plan-reference usage guard (`CourseService.cs:150-160`).
- **Evidence:** `AcademicPlanCourseConfiguration.cs:9-27`; `CourseService.cs:150-160`; `AcademicPlan.cs`/`AcademicPlanCourse.cs` (no `Course` nav).
- **Impact:** Functional/Medium — **data integrity**. The "verify" resolves to the *bad* branch: because there is no DB FK, deleting a referenced course does **not** fail; it silently leaves `AcademicPlanCourse` rows whose `CourseId` points at a non-existent course. Plan composition then references a ghost course; by-id plan reads (`AddCourse`/`Batch` re-fetch `_courses.GetByIdAsync`) will 404 on that course only when re-touched, but the orphan row persists and the layout read surfaces a dangling id.
- **Severity:** Medium
- **Notes:** Doc predicted the risk; this audit confirms the unsafe outcome. Recommended guard exists only as a doc suggestion (`ICourseRepository.IsReferencedAsync`) — not implemented.

### Divergence 2 — `Course` carries sync-provenance + closable lifecycle the doc omits
- **Model:** Doc §1: module owns the catalog "and deliberately nothing else"; §12 lists only `Code/Title/CreditHours/Category/IsActive`.
- **Implementation:** `Course : BaseEntity, IExternallySourced` with an `ExternallySourcedData ExternallySourced` block (ExternalId/Version/OriginSystem/LastSyncedAt) flattened via `OwnsOne` + unique filtered index (`Course.cs:11,18`; `CourseConfiguration.cs:42-52`), **and** a closable lifecycle `IsClosed`/`ClosedAt`/`Close`/`Reopen`/`EnsureMutable` (`Course.cs:46-69`) surfaced as service `CloseRecordAsync`/`OpenRecordAsync` (`CourseService.cs:187-207`).
- **Evidence:** `Course.cs:11-69`; `CourseConfiguration.cs:42-52`; `CourseService.cs:187-207`.
- **Impact:** Documentation/Low–Medium. Code has two whole feature dimensions (external-sync ownership, close/reopen immutability) the model is silent on. An agent trusting the doc would not know a `Course` participates in the sync platform or can be frozen.
- **Severity:** Low (doc lag, not a code defect)

### Divergence 3 — `AcademicPlan` closable lifecycle undocumented
- **Model:** Doc §4/§12 list `Name/StructureNodeId/EffectiveFrom/EffectiveTo/IsActive/RowVersion` only; §11 mentions a *possible future* `Draft→Published→Archived` state machine.
- **Implementation:** `AcademicPlan` already has `IsClosed`/`ClosedAt`/`Close`/`Reopen`/`EnsureMutable` (`AcademicPlan.cs:44-73`); every plan/composition mutation calls `plan.EnsureMutable()` (`AcademicPlanService.cs:202,228,273,314,397`) and `CloseRecordAsync`/`OpenRecordAsync` exist (`:404-420`).
- **Evidence:** `AcademicPlan.cs:44-73`; `AcademicPlanService.cs:202-420`.
- **Impact:** Documentation/Low. A simpler precursor of the doc's "future" workflow is live; the doc doesn't record it.
- **Severity:** Low

### Divergence 4 — Bilingual localization already implemented (doc marks it "future")
- **Model:** Doc §11: "Localization of `Course.Title` and `AcademicPlan.Name`. Both are single-string today."
- **Implementation:** Both services decode bilingual JSON at read time via `ILocalizationService.Get<string>` and normalize on write via `LocalizedJson.Normalize`: Course localizes **`Code` and `Title`** (`CourseService.cs:97-102,140`); Plan localizes **`Name`** (`AcademicPlanService.cs:166-170,204`).
- **Evidence:** `CourseService.cs:97-102`; `AcademicPlanService.cs:166-170`.
- **Impact:** Documentation/Medium. The doc is materially behind code. Of note: `Code` is **both** unique-indexed/normalized (`Course.cs:23-27`, upper-cased) **and** localized JSON on read — storing `Code` as `{"ar":…,"en":…}` JSON would interact awkwardly with the upper-case normalization and the case-insensitive unique index. No defect proven (the localizer passes plain strings through unchanged, so plain `Code` values are unaffected), but the dual treatment of `Code` is an undocumented design tension worth flagging.
- **Severity:** Low (no proven defect; doc-accuracy + latent `Code`-as-JSON tension)

### Divergence 5 — Course reads are unscoped (doc §3 says "every read/write" goes through `IEffectiveScope`)
- **Model:** Doc §3 table row: "Authorization scoping — `IEffectiveScope` checks on every read/write."
- **Implementation:** `CourseService` injects no `IEffectiveScope`; catalog is school-wide and intentionally unscoped (`CourseService.cs:38-52`, class comment lines 15-23). Only `AcademicPlanService` is scoped.
- **Evidence:** `CourseService.cs:38-52`.
- **Impact:** Documentation/Low. The blanket §3 statement over-claims; §1/§2/§13 clarify catalog is global, so the model is internally consistent elsewhere. The §3 row is imprecise, not a security gap (courses are deliberately public within the tenant).
- **Severity:** Low

## Step 5 — Hidden-Logic Review

- **Cache poisoning across cultures:** both services cache the **culture-neutral** payload (JSON `Code/Title/Name`) and decode on the way out (`CourseService.cs:57-62,97-102`; `AcademicPlanService.cs:78-86,166-170`). Two `Accept-Language` callers share one entry without cross-contamination. **No verified issue.**
- **Cached-read scope enforcement:** plan cache hit re-checks scope against the cached `StructureNodeId` before returning (`AcademicPlanService.cs:80-86`) — upholds invariant 7 (warm cache cannot leak). **No verified issue.**
- **`BatchUpdateCoursesAsync` all-or-nothing:** pre-validates all adds, detects intra-request duplicate `CourseId` (`:327-332`), applies removals before adds so an add+remove of the same course in one batch is legal (`:344-371`), single `SaveChanges` (`:374`). Logic sound. **No verified issue.**
- **`RemoveCourseAsync` cross-plan guard:** rejects when `entry.AcademicPlanId != planId` (`:383-386`) before scope-check. **No verified issue.**
- **Closed-entity delete:** `DeleteAsync` calls `EnsureMutable()` (`CourseService.cs:155`; `AcademicPlanService.cs:228`) — a closed course/plan cannot be deleted until reopened. Undocumented but coherent. **No verified issue.**
- **Course orphan risk:** see Divergence 1 — the one proven integrity gap (no DB FK + no usage guard).
- **No verified security issue found.**

## Step 6 — Flow Verification

### Course by-id read
- **Expected:** cache-aside on `course:object:{id}`, repository fallback, no scope (catalog global).
- **Actual:** `CourseService.GetByIdAsync:54-70` — exact.
- **Match:** YES.

### Plan by-id read
- **Expected:** cache-aside, eager composition, scope-checked, out-of-scope → null.
- **Actual:** `AcademicPlanService.GetByIdAsync:70-96` — cache + re-scope-check + `includeCourses:true` + ordered composition. Matches.
- **Match:** YES.

### Add / batch / remove composition
- **Expected:** validate + scope + existence + uniqueness; cascade-owned by plan; invalidate plan id.
- **Actual:** `AddCourseAsync:260-296`, `BatchUpdateCoursesAsync:298-376`, `RemoveCourseAsync:378-402` — all gates present; `RemoveAsync(CacheKey(planId))` after each.
- **Match:** YES.

### Course / plan delete
- **Expected:** hard delete; plan delete cascades composition; course delete unguarded (doc-flagged risk).
- **Actual:** hard deletes confirmed; plan→APC cascade via schema; course delete has no usage guard and **no DB FK backstop** (Divergence 1).
- **Match:** YES on behavior; the doc-flagged risk is **realized** (orphaning, not failure).

### Lifecycle (Close/Open) — *not in model*
- **Expected:** Not documented.
- **Actual:** `Close/OpenRecordAsync` on both services gate mutation via `EnsureMutable`.
- **Match:** N/A — undocumented feature (Divergences 2–3).

## Step 7 — Final Verdict

### Scores
- Architecture Match: 8/10
- Logic Correctness: 9/10
- Operational Safety: 7/10
- Maintainability: 8/10

### Confirmed Issues
1. **No DB FK `AcademicPlanCourse → Course` + no usage guard** → hard course-delete silently orphans composition rows (doc §4/§11 "verify" resolves to the unsafe branch) — Medium, data integrity.
2. Model doc omits `Course`/`AcademicPlan` **closable lifecycle** (`IsClosed`/`Close`/`Reopen`/`EnsureMutable`) now live in code — Low, doc lag.
3. Model doc omits `Course` **sync-provenance** (`IExternallySourced`/`ExternallySourced` + filtered unique `ExternalId` index) — Low, doc lag.
4. **Localization already implemented** for `Course.Code`/`Title` and `AcademicPlan.Name` (doc marks it "future"); `Code`-as-JSON vs unique/normalize is an undocumented tension — Low.
5. Doc §3 over-claims scope on "every read/write" — courses are deliberately unscoped — Low, doc imprecision.

### Model Violations
- Entity-shape: undocumented lifecycle + sync fields on `Course`/`AcademicPlan`. Behavioral: localization moved from "future" to "live". None of these break the documented invariants — they extend them.

### False Positives
- "Course code globally unique, CI, normalized" — upheld (setter + collation + unique index + precheck).
- "Course appears at most once per plan" — upheld (unique index + `ContainsCourseAsync`).
- "Plans scope-checked on every op; out-of-scope → null" — upheld.
- "Cached plan re-scope-checked" (invariant 7) — upheld.
- "Mutations invalidate exactly `:object:{id}`; lists uncached" — upheld.
- "Plan→composition cascade; plan→node restrict" — upheld.
- "`EffectiveTo > EffectiveFrom`" — upheld (create + update).
- "Course delete is hard, no usage guard" — upheld (doc-accurate; see Issue 1 for the FK consequence).

### Findings Summary
- **Matches:** Catalog/plan schema (codes, collation, unique indexes, restrict/cascade FKs, RowVersion), validator ranges, scope enforcement on plans incl. warm-cache re-check, cache-aside + invalidate-by-id, slim uncached lists, eager composition on by-id, batch all-or-nothing semantics.
- **Does not match:** Undocumented lifecycle + sync provenance on entities; localization already implemented (doc says future); §3's blanket scope claim; **missing DB FK** to `Course` making the hard-delete orphan risk real.
- **Needs attention:** (1) Add a course-usage guard (or a DB FK) to prevent orphaned `AcademicPlanCourse` rows — the only material defect. (2) Update the model doc to record the closable lifecycle, sync-provenance, and now-live localization. Security model for plans is sound.
