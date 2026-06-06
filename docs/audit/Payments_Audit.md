# Payments (Invoicing & Payment Verification) Module — Reverse-Audit (Undocumented Logic)

**Status:** No model document exists for this module. `InvoiceService.cs:20-22` cites `docs/caching-strategy.md` (which *does* exist and was audited separately), but there is **no Payments model doc** describing invoice lifecycle, idempotency, or settlement rules. This is a **reverse audit**: the implicit contract is reconstructed from code, then verified for internal consistency, correctness, safety, and hidden logic.

**Branch:** `sync_platform_hardening`
**Audit date:** 2026-06-03
**Method:** Same 7-step rigor as the documented audits; "Model Extract" is reconstructed from code instead of a doc. Only proven, evidence-backed findings. No code changed.

---

## 1. Reconstructed Model (implicit contract from code)

**Purpose.** Owns the invoice schema, totals math, and payment-settlement state machine for a single student. Other modules author *fee lines* through `IFeeCreationService`; payment providers report *transactions* through `IPaymentVerificationService`. Payments never interprets a `SourceModule`/`ReferenceId` (`InvoiceItem.cs:6-10, 26`).

**Reconstructed assumptions / invariants.**
- R1. `TotalAmount` is denormalised and **always equals the sum of item amounts at persist time** (`Invoice.cs:13-17`; enforced both write paths).
- R2. A `PaymentTransaction` is **idempotent per `(InvoiceId, IdempotencyKey)`** — a replayed webhook never double-records (`PaymentTransaction.cs:6-12, 32-33`).
- R3. `InvoicePaidEvent` fires **exactly once on the edge transition into `Paid`** — not on partial payments, not on a "succeeded" txn landing on an already-paid invoice (`InvoicePaidEvent.cs:9-18`).
- R4. Invoice status reflects settled total: `Paid` if `settled >= TotalAmount`, else `PartiallyPaid` if `settled > 0` (`PaymentVerificationService.cs:197-216`).
- R5. Scope (`IEffectiveScope`) is enforced on **every** `InvoiceService` read/write; verification + fee-creation services are gateway/service-facing and route-permission-gated, **by design** (`PaymentVerificationService.cs:182-185`).
- R6. Closable lifecycle: once `IsClosed`, the entity is immutable under `EnsureMutable` until `Reopen` (`Invoice.cs:45-76`).
- R7. Cancel transitions: `Paid → Conflict`, `Cancelled → idempotent no-op`, else `→ Cancelled` (`InvoiceService.cs:200-216`).
- R8. Cross-module student reference is **id-only, no EF navigation**; FK `Restrict` to Student; Items/Transactions `Cascade` (`InvoiceConfiguration.cs:31-46`).
- R9. `IExternallySourced` provenance flattened via `OwnsOne`, filtered-unique `ExternalId` index (`InvoiceConfiguration.cs:48-60`) — synced as an Invoice by the Finance sync module.
- R10. Optimistic concurrency via SQL `rowversion` (`Invoice.cs:31-32`, config `:21`).
- R11. Soft-delete (`ISoftDeletable`) on both Invoice and PaymentTransaction with global query filters (`InvoiceConfiguration.cs:26`, `PaymentTransactionConfiguration.cs:30`).
- R12. Cache-aside on `invoice:object:{id:N}`, 10-min TTL, scope re-check on every hit, culture-neutral payload + localize-on-read, invalidate on every mutation (`InvoiceService.cs:26-27, 52-79`).

**Expected flow.** Create/author sums items into `TotalAmount`; RecordAsync probes idempotency → inserts txn → reflects settled total → stages outbox on Paid-edge → saves (catching unique-index race); Cancel/Close/Open mutate with scope + lifecycle guards; reads are cache-aside with scope re-check.

---

## 2. Implementation Files

- Domain: `Domain/Invoice.cs`, `Domain/InvoiceItem.cs`, `Domain/PaymentTransaction.cs`
- Application: `Application/InvoiceService.cs`, `Application/PaymentVerificationService.cs`, `Application/FeeCreationService.cs`, `Application/Validators/InvoiceValidators.cs`
- Persistence: `Persistence/Configurations/InvoiceConfiguration.cs`, `InvoiceItemConfiguration.cs`, `PaymentTransactionConfiguration.cs`
- Repository: `Repositories/IInvoiceRepository.cs`, `Repositories/InvoiceRepository.cs`
- Contracts/Events: `Abstractions/Payments/Events/InvoicePaidEvent.cs`
- DI/Manifest: `PaymentsModuleExtensions.cs` (+ `PaymentsPermissionManifest`)
- Outbox seam: `Core.Infrastructure/Services/Outbox/OutboxService.cs`

---

## 3. Invariant Verification

| # | Invariant | Enforcement | Evidence | Holds |
|---|-----------|-------------|----------|-------|
| R1 | TotalAmount = Σ items at persist | Recomputed on both write paths | `InvoiceService.cs:157`; `FeeCreationService.cs:114` | ✅ |
| R2 | Txn idempotent per (Invoice, Key) | Schema unique index + fast-path probe + retry re-probe + catch-on-violation | `PaymentTransactionConfiguration.cs:26`; `PaymentVerificationService.cs:63-67, 87-91, 148-160` | ✅ (see Issue 1 re: detector strictness) |
| R3 | Paid-edge event fires once | `wasAlreadyPaid` captured pre-mutation; enqueue only on `!wasAlreadyPaid && nowPaid` | `PaymentVerificationService.cs:102, 132-144` | ✅ |
| R3′ | Event staged transactionally | `EnqueueAsync` stages only (Add, no SaveChanges); commits with caller's save | `OutboxService.cs:41-44`; `PaymentVerificationService.cs:123-150` | ✅ |
| R4 | Status reflects settled total | Σ non-deleted Succeeded txns → Paid / PartiallyPaid | `PaymentVerificationService.cs:197-216` | ✅ |
| R5 | Scope on every InvoiceService op | `CanAccessStudentAsync` on get/getlist/create/cancel/close/open/search | `InvoiceService.cs:66,74,101,133,185,241,254` | ✅ (verification/fee services excluded by design — see Issue 4) |
| R6 | Closable immutability | `EnsureMutable` throws when closed | `Invoice.cs:56-60`; `InvoiceService.cs:202` | ✅ |
| R7 | Cancel transition rules | Paid→Conflict, Cancelled→no-op, else→Cancelled | `InvoiceService.cs:200-216` | ✅ |
| R8 | Id-only ref, FK Restrict, Cascade children | `HasOne<Student>().WithMany().HasForeignKey(StudentId).Restrict`; Items/Tx Cascade | `InvoiceConfiguration.cs:33-46` | ✅ |
| R9 | External provenance | `OwnsOne` flatten + filtered-unique ExternalId | `InvoiceConfiguration.cs:50-60` | ✅ |
| R10 | Optimistic concurrency | `IsRowVersion()` | `Invoice.cs:32`; `InvoiceConfiguration.cs:21` | ⚠️ handled on RecordAsync (ConcurrencyRetry) but **not** on Cancel/Close/Open (Issue 2) |
| R11 | Soft-delete both entities | `HasQueryFilter(!IsDeleted)` ×2; settlement re-filters `!IsDeleted` in memory | `InvoiceConfiguration.cs:26`; `PaymentTransactionConfiguration.cs:30`; `PaymentVerificationService.cs:204-205` | ✅ |
| R12 | Cache-aside + scope re-check + localize-on-read | `invoice:object:{id:N}`, 10m, re-auth on hit, decode on return | `InvoiceService.cs:26-27, 63-78, 86-96` | ✅ |

---

## 4. Findings

### Issue 1 — Two divergent idempotency-violation detectors; the thorough one (`SaveTransactionWithIdempotencyAsync`) is dead production code, the live path uses a looser inline check
- **Reconstructed expectation:** R2 — duplicate `(InvoiceId, IdempotencyKey)` insert is caught and resolved to the winning row without throwing.
- **Implementation:** Two separate detectors exist:
  - `InvoiceRepository.SaveTransactionWithIdempotencyAsync` (`InvoiceRepository.cs:229-256`) + `IsIdempotencyDuplicate` (`:258-267`) narrows on **both** SQL error number (2627/2601) **and** the index name `IX_PaymentTransactions_InvoiceId_IdempotencyKey`, with the explicit comment "so unrelated unique constraints elsewhere on the table never count as an idempotency replay."
  - The production write path `PaymentVerificationService.RecordAsync` does **not** call that helper. It inlines `IsIdempotencyViolation` (`PaymentVerificationService.cs:148-172`), which narrows on the error number **only** — no index-name check.
- **Evidence:** `SaveTransactionWithIdempotencyAsync` is referenced **only by tests** — `tests/Core.UniTests/Payments/PaymentVerificationServiceTests.cs:37-39` sets it up as a default mock, but the SUT (`PaymentVerificationService`) never invokes it (verified by repo-wide grep; the sole production reference is the interface/impl declaration).
- **Impact:**
  - (a) **Dead production code + duplicated logic:** the deliberately-hardened helper is unused; the live path carries a parallel, looser copy.
  - (b) **Inert test arrange:** the unit test mocks a method the production code never calls, so that idempotency setup does not actually exercise the production catch path.
  - (c) **Looser narrowing in the live path:** the service's catch (`:152-160`) would treat *any* 2627/2601 raised during the shared `SaveChangesAsync` as an idempotency replay and return the "winner" txn. On this save scope the only other unique index is Invoice's filtered `ExternalId` index, which this path does not mutate — so integrity is **safe in practice**, but the protection the repository author intentionally added is absent from the code that actually runs.
- **Severity:** Low.

### Issue 2 — `RowVersion` handled on RecordAsync but not on Cancel/Close/Open write paths
- **Reconstructed expectation:** R10 — optimistic concurrency via rowversion.
- **Implementation:** `RecordAsync` wraps its mutation in `ConcurrencyRetry.ExecuteAsync` with `ResetChangeTracker` + re-probe (`PaymentVerificationService.cs:75-164`) — strong. But `InvoiceService.CancelAsync`/`CloseRecordAsync`/`OpenRecordAsync` call `SaveChangesAsync` directly with no `DbUpdateConcurrencyException` handling (`InvoiceService.cs:170, 223, 232`).
- **Impact:** A concurrent edit to the same invoice on a cancel/close/open surfaces as an unhandled 500 rather than a translated conflict. The rowversion still prevents lost updates — only error-shaping is missing. (Same shape as Student-module Issue 2.)
- **Severity:** Low.

### Issue 3 — Item `Description` normalization is inconsistent between the two write paths
- **Reconstructed expectation:** Item descriptions are stored as culture-neutral `LocalizedJson` (`{"ar":…,"en":…}`) so the cache can hold one payload and `Localize` decodes on read (`InvoiceService.cs:60-62, 86-96`).
- **Implementation:** `InvoiceService.CreateAsync` normalizes via `LocalizedJson.Normalize(i.Description)` (`:154`). `FeeCreationService.CreateFeesAsync` stores the **raw** `i.Description` with no normalization (`FeeCreationService.cs:111`).
- **Impact:** Fees authored through the service-to-service seam persist a raw (possibly non-JSON) `Description`; controller-created invoices persist normalized bilingual JSON. Both flow through `Localize` → `_localization.Get<string>(...)` on read; for a non-JSON value `Get<string>` returns it as-is, so it degrades gracefully — but the two write paths are inconsistent, and a fee-seam description authored as a raw localizable string will not round-trip bilingually. Real divergence, presentation-only.
- **Severity:** Low.

### Issue 4 — Verification and fee-creation services have no `IEffectiveScope` (by design, undocumented in a model)
- **Implementation:** `PaymentVerificationService` and `FeeCreationService` take no `IEffectiveScope`. The verification service documents this: route permission `PaymentTransactions.View` is the admin gate and the gateway/webhook caller is trusted (`PaymentVerificationService.cs:182-185`). `FeeCreationService` is a service-to-service seam (other modules author fees), so per-student scope does not apply.
- **Impact:** Documentation/traceability only — `InvoiceService` enforces scope on every op, so the asymmetry is intentional, but with no model doc a maintainer cannot confirm it is deliberate. No authorization defect: both services are reachable only through admin-/system-gated routes.
- **Severity:** Low (informational).

---

## 5. Hidden-Logic Review

- **Transactional outbox staging (`PaymentVerificationService.cs:123-150`, `OutboxService.cs:41-44`).** The Paid-edge event is enqueued *before* `SaveChangesAsync`; `EnqueueAsync` only `Add`s the row (no inner save), so the outbox row commits in the same transaction as the invoice + transaction. On idempotency-violation catch, `ResetChangeTracker()` (`:157`) drops the staged-but-uncommitted outbox row before returning the winner — no phantom event. *Verified correct. No issue.*
- **Edge-only event firing (`:102, 132-144`).** `wasAlreadyPaid` is captured from the reloaded invoice *before* mutation; the event fires only on `!wasAlreadyPaid && nowPaid`, so a "succeeded" txn landing on an already-Paid invoice is a silent no-op. Matches the `InvoicePaidEvent` contract (`:9-18`). *No issue.*
- **Settlement re-filters soft-deleted in memory (`:204-205`).** The global query filter already hides `IsDeleted` rows on load, but the settled-total sum re-applies `!t.IsDeleted` defensively against a row soft-deleted post-hydration or added in-memory by a sibling write. Cheap, honest. *No issue.*
- **Split-query on dual includes (`InvoiceRepository.cs:28-30`).** When both Items and Transactions are included, `AsSplitQuery()` avoids the Cartesian row explosion. *No issue.*
- **Fee-seam validates-then-refuses-whole-batch (`FeeCreationService.cs:51-83`).** Service-to-service callers bypass the controller's FluentValidation, so each item is re-validated with per-index error keys (`items[2].Amount`) and the *entire* batch is refused on any invalid entry — no partial commit of a bad fee. *No issue.*
- **Fast-path + retry re-probe + winner-fetch (`:63-67, 87-91, 158-159`).** Three idempotency layers ensure a replayed webhook never double-records even under a race that slips past the fast-path. *No issue (see Issue 1 only for detector-strictness/dead-code).* 
- **Search free-text scoped to owned columns (`InvoiceRepository.cs:79-83, 172-176`).** Invoice search deliberately matches only `Currency` (item descriptions are nested JSON; per-row deserialize avoided). Documented design. *No issue.*

---

## 6. Flow Verification

**Create (controller).** Validate → scope (out-of-scope → NotFound, no existence leak) → build invoice + normalized items → sum TotalAmount → add → save. **YES.**

**Author fees (service-to-service).** Per-item validate → refuse batch on any error → merge-with-pending or new invoice → append items (raw Description) → sum TotalAmount → save → invalidate cache. **YES**, with the Description-normalization inconsistency (Issue 3).

**Record payment.** Validate → fast-path idempotency probe → `ConcurrencyRetry`{re-probe on retry → reload invoice → capture `wasAlreadyPaid` → add txn → reflect settled total → stage outbox on Paid-edge → save (catch unique-violation → return winner)} → invalidate cache. **YES.**

**Cancel.** Load (scope guard, NotFound on miss/out-of-scope) → ApplyCancel (Paid→Conflict, Cancelled→no-op, EnsureMutable on closed) → save → invalidate. **YES**, with the unhandled rowversion caveat (Issue 2).

**Close / Open.** Load (scope guard) → `Close()`/`Reopen()` → save → invalidate. **YES** (Issue 2 caveat).

**Read (by id).** Cache hit → scope re-check → Localize/null; miss → repo (include items) → scope check → cache culture-neutral → Localize. **YES.**

**Bulk cancel.** Per-id try/catch wrapping single cancel; failures coded NotFound/Conflict; independent commits (partial success). **YES.**

---

## 7. Verdict

### Scores
- **Architecture:** 9/10 — Clean id-only modular reference, producer-owned event contract in Abstractions, transactional outbox, filtered-unique idempotency + ExternalId indexes, split-query, permission manifest, EF-assembly contribution. No model doc to anchor the design (traceability gap).
- **Logic:** 9/10 — Idempotency is genuinely strong (schema + three service layers + edge-only event), settlement math is defensive, cancel/close state machine is correct. Docked only for the divergent/dead idempotency detector (Issue 1).
- **Operational Safety:** 8/10 — Scope re-check on cache hits, transactional event delivery, probe-resistant 404s, `Restrict` FK, soft-delete re-filter. Docked for unhandled rowversion conflicts on cancel/close/open surfacing as 500s (Issue 2).
- **Maintainability:** 8/10 — Heavily commented, consistent with platform caching/scope/outbox conventions. Dead `SaveTransactionWithIdempotencyAsync` + inert test mock, the Description-normalization asymmetry, and the absent model doc are minor blemishes.

### Confirmed Issues
1. (Low) Divergent idempotency detectors: the index-name-narrowed `SaveTransactionWithIdempotencyAsync` is dead production code (test-only); the live path uses a looser error-number-only inline check; the unit test mocks a method the SUT never calls — `InvoiceRepository.cs:229-267`, `PaymentVerificationService.cs:148-172`, `PaymentVerificationServiceTests.cs:37-39`.
2. (Low) `RowVersion` handled on RecordAsync but not on Cancel/Close/Open — `InvoiceService.cs:170, 223, 232`.
3. (Low) Item `Description` normalized in `InvoiceService.CreateAsync` but stored raw in `FeeCreationService` — `InvoiceService.cs:154` vs `FeeCreationService.cs:111`.
4. (Low, informational) No `IEffectiveScope` on verification/fee services — by design, route-gated — `PaymentVerificationService.cs:182-185`.

### False Positives Cleared
- "Outbox event could fire without the payment committing" — `EnqueueAsync` stages only; the row commits in the same `SaveChanges`; the violation-catch resets the tracker before returning. No phantom delivery.
- "Duplicate webhook double-records a payment" — unique index + fast-path probe + retry re-probe + winner-fetch. Idempotent.
- "Looser inline detector misclassifies an unrelated unique violation" — practically unreachable on this write scope (only other unique index is Invoice's `ExternalId`, untouched here). Integrity safe; flagged as Issue 1 for the dead-code/strictness divergence, not a live defect.
- "Settled total counts soft-deleted transactions" — re-filtered `!IsDeleted` in memory.

### Findings Summary
A strong, well-built financial module. Every reconstructed invariant holds, with **two-plus-layer idempotency**, **edge-only transactional event delivery**, and **scope + lifecycle guards** as the load-bearing controls. The only real gaps are **error-shaping under concurrency on the cancel/close/open paths** (Issue 2), a **dead, more-thorough idempotency helper paired with a looser live inline check and an inert test mock** (Issue 1), and a **presentation-only Description-normalization asymmetry between the two write paths** (Issue 3). No data-integrity or authorization defect found. The absence of a Payments model doc is the dominant maintainability gap.
