# Master Audit Report — Capital University Portal

**Scope:** System Documentation & Logic Verification Audit — full sweep of every model/architecture document under `docs/` against its implementation.
**Branch:** `sync_platform_hardening`
**Audit date:** 2026-06-02
**Method:** Per-document 7-step audit (model extract → file location → per-assumption verification → divergence blocks with file:line evidence → hidden-logic review → flow verification → scored verdict). Only proven, evidence-backed findings recorded. No code rewritten, refactored, or redesigned.

**Verdict in one line:** The implementation is a **faithful, well-engineered realization** of the documented system. Most divergences are **documentation lag** (code matured past stale docs) rather than defects. **One material data-integrity defect** and a small set of **Medium contract/operational divergences** warrant action; everything else is Low.

---

## 1. Documents Audited

| # | Document | Audit report | Arch | Logic | Op-Safety | Maint |
|---|----------|--------------|:----:|:-----:|:---------:|:-----:|
| 1 | `Authentication_Model.md` | `Authentication_Audit.md` | 7 | 8 | 7 | 7 |
| 2 | Authorization (Model + Impl) | `Authorization_Audit.md` | 9 | 9 | 8 | 9 |
| 3 | RequestPipeline | `RequestPipeline_Audit.md` | 6 | 8 | 8 | 7 |
| 4 | `AcademicStructure_Model.md` | `AcademicStructure_Audit.md` | 9 | 9 | 8 | 7 |
| 5 | `CoursesAndAcademicPlans_Model.md` | `CoursesAndAcademicPlans_Audit.md` | 8 | 9 | 7 | 8 |
| 6 | `CourseOffering_Model.md` | `CourseOffering_Audit.md` | 9 | 9 | 9 | 9 |
| 7 | `Schedules_Model.md` | `Schedules_Audit.md` | 8 | 9 | 9 | 8 |
| 8 | `caching-strategy.md` | `Caching_Audit.md` | 6 | 9 | 7 | 9 |
| 9 | `Sync_Platform_Model.md` | `SyncPlatform_Audit.md` | 9 | 8 | 9 | 9 |
| 10 | Record Locking Lifecycle | `RecordLocking_Audit.md` | 9 | 9 | 9 | 9 |
| | **Average** | | **8.0** | **8.8** | **8.0** | **8.3** |

**Reading of the spread.** Logic correctness is uniformly high (8–9) — *what is built behaves correctly*. The lower Architecture scores cluster where the **document over-describes** relative to the build (RequestPipeline 6, Caching 6) or where the code **adds platform features the doc forbade/deferred** (Schedules 8, Courses 8). No document scored below 6 on any axis.

---

## 2. Confirmed Issues — Consolidated by Severity

### Medium (action recommended)

| # | Source | Issue | Evidence |
|---|--------|-------|----------|
| M1 | Courses & Academic Plans | **Missing DB FK `AcademicPlanCourse → Course` and no usage guard** — hard course delete silently orphans composition rows. The single material data-integrity defect found. | `CoursesAndAcademicPlans_Audit.md` Issue 1 |
| M2 | Sync Platform | **"External deleted → Internal soft-delete" not implemented** — `CoreWriteGateway` has only insert/update; external deletions do not propagate. | `SyncPlatform_Audit.md` Div 2; `CoreWriteGateway.cs:96-138` |
| M3 | Sync Platform | **Scheduled cadence is minutely, not the documented "daily at 2 AM."** | `SyncPlatform_Audit.md` Div 1; `SyncRecurringJobsRegistrar.cs:49-107` |
| M4 | Authentication | **JWT embeds National ID + Email** vs documented lightweight-session-only token (privacy/PII). | `Authentication_Audit.md` Issue 2 |
| M5 | Authentication | Documented `IAuthorizationResponseBuilder` + "Authorized Scopes" bootstrap **absent** from production. | `Authentication_Audit.md` Issues 1, 3 |
| M6 | RequestPipeline | **Documented scope header names do not match implementation** (`X-Faculty-Id/...` vs `X-StructureNode-Id/X-AcademicYear-Id/X-Semester-Id`) — misleads API clients. | `RequestPipeline_Audit.md` Issue 1 |
| M7 | RequestPipeline | **No culture middleware / `UseRequestLocalization`**; request thread culture not set. | `RequestPipeline_Audit.md` Issue 2 |
| M8 | Caching | **No cache-stampede / single-flight protection** against the doc's stated 10k-user goal. | `Caching_Audit.md` Issue 1 |

### Low (latent / cosmetic / doc-lag)

- **Academic Structure:** `StructureNodeType.Department` enum unreachable (dead member); `MoveNodeAsync` relies on `descendants[0]` ordering (fragile, currently correct); service throws raw English `Exception` bypassing localized-exception convention.
- **Authorization:** Manifest sync does not rotate the global epoch despite doc claim (doc self-contradicts §5.4 vs §11); bootstrap enumeration is scope-keyed and can disagree with flattened runtime evaluation for cross-scope denies (backend remains authoritative); `PermissionSyncReport` omits counts.
- **Caching:** Localization key MUST contradicted (keys carry no culture segment — behavior is *safe*: culture-neutral payload + localize-on-read).
- **Schedules:** Manifest comment factually wrong (claims "No `Open` verb" while calling `WithCrudActions` which emits `Open`; surface is actually correct); `DeliveryMode?` optional field unimplemented.
- **Course Offering:** Doc framed as "minimal initial model"; implementation matured well past it (all within owned scope).
- **Sync Platform:** `inbox_messages` absent / `sync_jobs` orphaned / per-module outbox vs central; metadata via composition not inheritance; EFCore.BulkExtensions not used; `ISyncModule` signature extended (superset); no explicit push-side origin filter (loop structurally avoided).

---

## 3. Cross-Cutting Themes

These patterns recur across multiple documents and are the real story of the audit.

### Theme A — Documentation lag (dominant theme, low risk)
The code has consistently **matured past the model docs**, which read as stale snapshots:
- **Sync provenance** (`IExternallySourced` / `ExternallySourced` + filtered unique `ExternalId` index) is implemented platform-wide — flagged in **Courses, Course Offering, Schedules** even though some module docs said "do not implement unless requested." It is the backbone the Sync Platform legitimately depends on.
- **Closable lifecycle** (`IsClosed` / `Close` / `Reopen` / `EnsureMutable`) is present on **Course, AcademicPlan, CourseOffering, ScheduleSlot** but undocumented in their model docs.
- **Localization** of catalog fields is **live** though `CoursesAndAcademicPlans_Model.md` marks it "future."
- **Course Offering** doc still calls itself an "intentionally minimal initial model" against a mature build.
- **Recommendation:** refresh the model docs to record sync-provenance, the closable lifecycle, and live localization. None of these break documented invariants — they extend them.

### Theme B — Doc over-specifies architecture the code realizes differently (Medium)
Where docs describe concrete component shapes, the code sometimes chooses a sound alternative:
- RequestPipeline: "Culture Middleware" / "RequestContext Middleware" are **lazy services**, not pipeline stages; `IEffectiveScope` is a **per-call guard**, not an intersected scope object.
- Authentication: the documented **builder abstraction** and **authorized-scopes bootstrap** are not built.
- Caching: **Layers 2 and 3, batch resolution, stampede protection** (all under "Main Design") are unbuilt — only Layer 1 shared-object cache exists.
- These are the **biggest contributors to lower Architecture scores** and the items most worth reconciling (update the doc *or* build the missing piece).

### Theme C — Idempotency & resiliency exceed the spec (strength)
The Sync Platform meets the doc's own "Most Important" axis and goes beyond: per-run dedup, external-wins version guard, race-safe dead-lettering on a DB unique index, orphan reaper, retention sweeper, replay/requeue tooling, exact retry backoff (1m/5m/15m/1h). The Caching and Schedules layers add Redis-outage graceful degradation and SERIALIZABLE TOCTOU guards the docs never required.

### Theme D — Security model is sound across the board (strength)
Every security-relevant invariant the auditor tested **held**: untrusted-headers-for-authz, DB-backed trusted scope, scope re-check on cache hits, fail-closed scope defaults, set-based permission evaluation with forward/reverse implies, no global auth-sensitive cache, password hashing/timing, JWT signing/expiry, no-dev-bypass admin gating on the Sync host. The only security-adjacent flag is **M4 (PII in JWT)** — a privacy/data-minimization concern, not an auth bypass.

---

## 4. Material Defects vs. Divergences

**Genuine defect (fix):**
- **M1** — missing FK / usage guard on `AcademicPlanCourse → Course`. This is the only finding across all nine audits that can corrupt data (orphaned composition rows). Everything else is a contract mismatch, an operational tuning, a privacy choice, or a scaling gap.

**Functional divergences from documented behavior (decide: build or amend doc):**
- **M2** external-delete soft-delete, **M3** sync cadence, **M5** authorized-scopes bootstrap, **M6** header contract, **M7** culture middleware, **M8** stampede protection.

**Privacy decision (confirm intent):**
- **M4** JWT PII.

**Documentation updates (no code change):**
- All Theme-A items; Authorization epoch-trigger wording; Schedules manifest comment; Caching culture-key rule wording; Course Offering "minimal" framing.

---

## 5. False Positives Cleared

The audit explicitly disproved several plausible-looking concerns, to avoid sending teams on phantom hunts:
- **"No FK to CourseOffering from ScheduleSlot = orphan risk"** — deliberate modularity rule, compensated by `CourseOfferingDeletedHandler` + `DeleteForOfferingAsync`.
- **"Schedule overlap rejection = forbidden conflict engine"** — it is intra-offering duplicate prevention, explicitly permitted; the forbidden cross-student/room/instructor checks are absent.
- **"Culture-neutral cache key = cross-culture poisoning"** — false; cached payload is bilingual, decoded per request.
- **"Caching EF tracked entities"** — false; detached DTOs only.
- **"Reverse-implies deny fail-open hazard"** — correctly mitigated.
- **"No CQRS/MediatR constraint violated"** — upheld (Role handlers are an accepted, user-confirmed deviation).
- **"`IRequestContext` used in queries / headers trusted for authz"** — both upheld as forbidden.
- **Sync:** "no transactional outbox in host," "pipeline doesn't advance checkpoint," "executor leaves run Running on failure," "`<=` external-wins drops changes" — all correct-by-design, not defects.

---

## 6. Prioritized Recommendations

**P1 — Fix the one data-integrity defect.**
Add a DB FK (or an explicit usage guard) for `AcademicPlanCourse → Course` so a hard course delete cannot orphan composition rows. (M1)

**P2 — Reconcile functional divergences (per item: build the behavior or correct the doc).**
- Decide whether external-delete should soft-delete internally; implement or strike from the conflict matrix. (M2)
- Confirm the intended sync cadence; align cron with the doc (or update the doc to "minutely, incremental"). (M3)
- Correct the RequestPipeline header table to the real header names — highest doc-priority, it actively misleads clients. (M6)
- Decide on culture middleware vs. the current localize-on-read approach; document the choice. (M7)
- Build or formally defer Layer 2 reference cache + stampede protection before the 10k-user load target is real. (M8)
- Resolve the Authentication builder / authorized-scopes gap (build, or update the model). (M5)

**P3 — Confirm the privacy posture.**
Decide whether National ID + Email belong in the JWT; if not, move to a server-side lookup. (M4)

**P4 — Documentation refresh (no code risk).**
Record sync-provenance, closable lifecycle, and live localization in the affected model docs; fix the Schedules manifest comment, the Authorization epoch wording, and the Course Offering "minimal" framing.

**P5 — Low-severity hardening (opportunistic).**
Remove/wire the unreachable `Department` enum; harden `MoveNodeAsync` against list-index assumptions; route service exceptions through the localized-exception convention; drop the orphaned `sync_jobs` column.

---

## 7. Conclusion

Across nine documents and their implementations, the Capital University Portal shows **high logic correctness (avg 8.7/10)** and a **consistently sound security model**. The lower architecture scores reflect **documentation that trails the code**, not unsafe code. Resiliency and idempotency in the Sync Platform, and the safety properties of the caching and scheduling layers, **exceed** what the documents require.

The audit surfaced exactly **one material data-integrity defect** (M1, the missing `AcademicPlanCourse → Course` FK) and a focused set of **Medium contract/operational/privacy divergences** (M2–M8) that are decisions to be made — build-vs-document — rather than emergencies. The remaining findings are Low-severity documentation lag and cosmetic items. No security bypass, no proven correctness defect in any examined runtime path beyond M1, and a large set of plausible concerns were positively cleared as false positives.

**Per-document detail:** see the nine reports in `docs/audit/`.

---

## 8. Addendum — Undocumented Modules (Reverse Audits)

**Scope extension.** Beyond the nine documented models above, three business modules ship **with no model document**: **Payments**, **Student (Student Information)**, and **Student Services**. For these the same 7-step rigor was applied as a **reverse audit** — the implicit contract (`R`-numbered invariants) was reconstructed from code comments + behavior, then each invariant was verified for internal consistency, correctness, operational safety, and hidden logic. "Never assume the code is correct" still governs; the only change is that the "model" is reconstructed rather than read.

### 8.1 Reverse-Audit Scores

| # | Module | Audit report | Arch | Logic | Op-Safety | Maint |
|---|--------|--------------|:----:|:-----:|:---------:|:-----:|
| 10 | Payments (Invoicing + Verification) | `Payments_Audit.md` | 9 | 9 | 8 | 8 |
| 11 | Student (Profile Records) | `StudentModule_Audit.md` | 9 | 8 | 8 | 8 |
| 12 | Student Services (Catalog + Forms + Workflow) | `StudentServices_Audit.md` | 9 | 7 | 7 | 8 |
| | **Reverse-audit average** | | **9.0** | **8.0** | **7.7** | **8.0** |
| | **Combined average (all 12)** | | **8.2** | **8.5** | **7.9** | **8.2** |

**Reading.** Architecture is uniformly high (9) — these modules are the most idiomatic in the codebase (id-only modular refs, filtered-unique indexes, soft-delete, per-object caching, outbox, permission manifests). The dip is in **Logic/Op-Safety for Student Services**, driven by its workflow engine under-enforcing its own catalog metadata.

### 8.2 Confirmed Issues — Undocumented Modules

#### Medium

| # | Source | Issue | Evidence |
|---|--------|-------|----------|
| U1 | Student Services | **Workflow-configured transitions ignore `TransitionType`** — the firing-mechanism guard (Manual/Automatic/Student) is enforced only on the no-workflow fallback path; with a workflow attached, a Manual staff endpoint can fire an `Automatic`/`Student` transition (incl. leaving `WaitingPayment` without payment). | `StudentServices_Audit.md` Issue 1; `StudentServiceRequestService.cs:574-602`, `WorkflowService.cs:102-103` |

#### Low (correctness / resiliency / doc-traceability)

- **U2 — Student Services:** per-transition `RequiredAction` verb stored but **never enforced** at the service layer (uniform controller permission is the only gate; privilege-granularity gap). `StudentServices_Audit.md` Issue 2.
- **U3 — Student Services:** `SubmitAsync` fee path is a **three-commit, non-atomic sequence**; a mid-sequence failure orphans the request from its invoice (no auto-reconciliation). Issue 3.
- **U4 — Payments:** **two divergent idempotency-violation detectors** — the index-name-narrowed `SaveTransactionWithIdempotencyAsync` is **dead production code** (test-only); the live path uses a looser error-number-only inline check, and a unit test mocks a method the SUT never calls. `Payments_Audit.md` Issue 1.
- **U5 — Student:** concurrent duplicate upsert → **unhandled `DbUpdateException`** not caught in `BatchUpsertAsync`, aborting the whole batch (dead `catch(ConflictException)` arm); the filtered unique index keeps integrity safe. `StudentModule_Audit.md` Issue 1.
- **U6 — All three modules:** **`RowVersion` mapped but no `DbUpdateConcurrencyException` handling** on write paths (Payments cancel/close/open; Student upsert/verify/delete; Student Services all) → concurrency conflicts surface as 500s. Lost-update protection still holds; only error-shaping is missing. (Payments `RecordAsync` is the exception — it wraps in `ConcurrencyRetry`.) `Payments_Audit.md` Issue 2; `StudentModule_Audit.md` Issue 2; `StudentServices_Audit.md` Issue 4.
- **U7 — Payments:** item `Description` normalized in `InvoiceService.CreateAsync` but stored **raw** in `FeeCreationService` — presentation-only write-path inconsistency. `Payments_Audit.md` Issue 3.
- **U8 — Student:** domain comment cites a **non-existent `docs/Plan.md`** as its rule authority. `StudentModule_Audit.md` Issue 3.

#### Informational (accepted-by-design deviations, undocumented)

- **U9 — Gateway/system-caller scope omissions:** `PaymentVerificationService` + `FeeCreationService` (Payments) and `ConfirmPaymentAsync` (Student Services) carry **no `IEffectiveScope` check** — by design (webhook / service-to-service / outbox system callers, route-permission gated). Worth recording precisely because no model doc confirms the intent. `Payments_Audit.md` Issue 4; `StudentServices_Audit.md` Issue 5.

### 8.3 Cross-Cutting Observations (reverse audits)

- **Theme A confirmed at the source.** The idempotency / transactional-outbox / external-provenance machinery the Sync Platform depends on is **authored here**: Payments owns the `payments.invoice.paid` edge-transition event (staged transactionally, fired once on the not-paid→Paid edge), and Student Services consumes it idempotently. The producer/consumer contract is correct end-to-end.
- **New recurring pattern — `RowVersion`-mapped-but-unhandled (U6).** Across **all three** undocumented modules (and noted in spirit in the documented set), rowversion is configured for lost-update protection but write paths don't translate `DbUpdateConcurrencyException` into a conflict result. It is a consistent, low-risk error-shaping gap worth a single platform-wide fix (a shared retry/translate helper — `ConcurrencyRetry` already exists and is used by Payments `RecordAsync`).
- **Strength — DB-enforced invariants.** Every "one X per Y" rule is backed by a **filtered/composite unique index** (Student: `(StudentId,Category,CustomCategoryKey)`; Payments: `(InvoiceId,IdempotencyKey)`; Student Services: `(RequestId,FieldId)`, `(Workflow,From,To)`), so integrity holds even where the service-layer read-before-write races. The indexes — not the service code — are the authoritative guard.
- **Strength — security discipline carries over.** Scope re-check on cache hits, existence-leak `NotFound`s (probe-resistant), and `Restrict` FKs are present and correct in all three. The only scope omissions are the deliberate system-caller seams (U9).

### 8.4 Revised Material-Defect Picture

The reverse audits surfaced **no new data-integrity defect** to rank alongside M1 (the missing `AcademicPlanCourse → Course` FK remains the single material integrity defect across all twelve). The most consequential new finding is **U1** (workflow `TransitionType` under-enforcement) — a *correctness/authorization-granularity* divergence, not data corruption — followed by the platform-wide **U6** error-shaping gap. Everything else is Low or accepted-by-design.

**Updated prioritized recommendations (additions):**
- **P2+ —** Enforce `TransitionType` (and decide whether to enforce `RequiredAction`) in the workflow-configured branch of `EnsureTransitionAllowedAsync`, so the workflow path is at least as strict as the fallback graph. (U1, U2)
- **P5+ —** One platform-wide pass to translate `DbUpdateConcurrencyException`/duplicate-`DbUpdateException` into conflict results on the write paths that lack it (reuse `ConcurrencyRetry`); delete the dead `SaveTransactionWithIdempotencyAsync` or switch the live path onto it; fix the `docs/Plan.md` citation. (U4, U5, U6, U8)
- **Doc —** Author model docs for Payments, Student, and Student Services (or at minimum record the accepted system-caller scope omissions U9 and the fee-submission consistency boundary U3), since their absence is now the dominant maintainability gap for these modules.

**Per-module detail:** see `Payments_Audit.md`, `StudentModule_Audit.md`, `StudentServices_Audit.md` in `docs/audit/`.
