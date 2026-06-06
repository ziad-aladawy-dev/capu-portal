# Student Services (Service Catalog + Dynamic Forms + Workflow Engine) Module — Reverse-Audit (Undocumented Logic)

**Status:** No model document exists for this module. It is the largest undocumented module: a configurable service catalog, dynamic field/document forms, a declarative workflow state machine, the request lifecycle, and the outbox consumer that bridges `payments.invoice.paid` back into the request flow. This is a **reverse audit**: the implicit contract is reconstructed from code, then verified for internal consistency, correctness, safety, and hidden logic.

**Branch:** `sync_platform_hardening`
**Audit date:** 2026-06-03
**Method:** Same 7-step rigor as the documented audits; "Model Extract" is reconstructed from code instead of a doc. Only proven, evidence-backed findings. No code changed.

---

## 1. Reconstructed Model (implicit contract from code)

**Purpose.** Lets admins configure university services (Transcript Request, Enrollment Certificate, …) with dynamic fields, required documents, an optional fee, and an optional workflow. Students submit requests; staff advance them through the workflow; payment settlement is bridged from the Payments module via the outbox.

**Reconstructed assumptions / invariants.**
- R1. A `StudentService` owns its dynamic `Fields`/`Documents` and references a `WorkflowDefinition` by id (no EF navigation across the configured-workflow boundary) (`StudentService.cs:24-56`).
- R2. `Name`/`Description`/`Label` are bilingual `LocalizedJson`, normalised on write, decoded on read (`StudentServiceService.cs:105-145, 166-167`).
- R3. A `WorkflowDefinition` picks a subset of the canonical `ServiceRequestStatus` enum as `States` and declares permitted `Transitions` with a firing `TransitionType` (Manual/Automatic/Student) and a per-transition `RequiredAction` verb (`WorkflowState.cs:6-32`, `WorkflowTransition.cs:6-24`).
- R4. A request's status changes only via an allowed transition; with a workflow attached the workflow is authoritative, else a hardcoded default graph applies (`StudentServiceRequestService.cs:567-603`).
- R5. Submitting against a fee-charging service authors an invoice through `IFeeCreationService` and moves the request to `WaitingPayment` (`:254-277`).
- R6. Payment settlement is delivered at-least-once via the outbox; `ConfirmPaymentAsync` advances the request out of `WaitingPayment` and is idempotent on replay (`InvoicePaidEventHandler.cs:10-34`, `:460-509`).
- R7. Scope (`IEffectiveScope.CanAccessStudentAsync`) is enforced on every student/staff read+write path; out-of-scope and missing both surface as `NotFound` (`:95-106, 511-555`).
- R8. Cross-module references (StudentId, PaymentReferenceId, AllowedProcessingRoleIds) are id/CSV only — no EF navigation; FK `Restrict` to Student/Service, `Cascade` to children (`StudentServiceRequestConfiguration.cs:36-54`).
- R9. Dynamic field/document submissions are validated against the service's configured definitions at submit time (`:616-738`).
- R10. Soft-delete (`ISoftDeletable`) with global query filters and filtered-unique `Code` index; field/document definitions are `Restrict`-protected against deletion while submissions reference them (`StudentServiceConfiguration.cs:29, 49-51`; `ServiceFieldValueConfiguration.cs:24-31`).
- R11. Optimistic concurrency via SQL `rowversion` on `StudentService` + `StudentServiceRequest` (`StudentService.cs:52`, `StudentServiceRequest.cs:37`).
- R12. Cache-aside per object (`student-service:object:` 15m, `student-service-request:object:` 5m), scope re-check on hit, culture-neutral payload + localize-on-read, invalidate on mutation (`StudentServiceService.cs:33-34, 61-76`; `StudentServiceRequestService.cs:52-53, 95-111`).

**Expected flow.** Configure service/workflow → student submits (validate dynamic form → resolve initial state → author fee → WaitingPayment) → outbox confirms payment → staff approve/reject/move via workflow transitions → terminal.

---

## 2. Implementation Files

- Domain: `Domain/StudentService.cs`, `StudentServiceRequest.cs`, `WorkflowDefinition.cs`, `WorkflowState.cs`, `WorkflowTransition.cs`, `ServiceFieldDefinition.cs`, `ServiceFieldValue.cs`, `ServiceDocumentDefinition.cs`, `ServiceDocumentSubmission.cs`
- Application: `Application/StudentServiceService.cs`, `StudentServiceRequestService.cs`, `WorkflowService.cs`, `Application/Outbox/InvoicePaidEventHandler.cs`, `Application/Validators/*`
- Persistence: `Persistence/Configurations/*` (9 configs)
- Repository: `Repositories/StudentServiceRepository.cs`, `StudentServiceRequestRepository.cs`, `WorkflowRepository.cs`
- DI: `StudentServicesModuleExtensions.cs`

---

## 3. Invariant Verification

| # | Invariant | Enforcement | Evidence | Holds |
|---|-----------|-------------|----------|-------|
| R1 | Service owns fields/docs, workflow by id | Cascade children; `WorkflowDefinitionId` scalar | `StudentServiceConfiguration.cs:35-45` | ✅ |
| R2 | Bilingual JSON normalize/localize | `LocalizedJson.Normalize` on write; `_localization.Get` on read | `StudentServiceService.cs:166-167, 105-145` | ✅ |
| R3 | Workflow declares states + typed transitions | Enum-subset states; transition rows with type + verb | `WorkflowService.cs:60-80` | ✅ (but type/verb not enforced — Issues 1–2) |
| R4 | Status changes only via allowed transition | `EnsureTransitionAllowedAsync` workflow + default graph | `:567-603` | ⚠️ workflow path ignores `TransitionType` (Issue 1) |
| R5 | Fee → invoice → WaitingPayment | `IFeeCreationService.CreateFeesAsync` + status flip | `:254-277` | ✅ (non-atomic — Issue 3) |
| R6 | At-least-once payment confirm, idempotent | No-op when status ≠ WaitingPayment; empty match no-op | `:474-486`; `InvoicePaidEventHandler.cs:58-86` | ✅ |
| R7 | Scope on every student/staff op | `CanAccessStudentAsync` on get/list/submit/cancel/staff loads | `:100,106,195,522,535,549` | ✅ (except ConfirmPaymentAsync — Issue 5, by design) |
| R8 | Id-only refs, FK Restrict/Cascade | FK Restrict to Student+Service; Cascade FieldValues/Documents | `StudentServiceRequestConfiguration.cs:36-54` | ✅ |
| R9 | Dynamic form validated at submit | Required/format/dropdown/file checks per definition | `:616-738` | ✅ |
| R10 | Soft-delete + Restrict on definitions | Query filters; filtered-unique Code; `Restrict` FK on field/doc def | `StudentServiceConfiguration.cs:29,49-51`; `ServiceFieldValueConfiguration.cs:24-31` | ✅ |
| R11 | Optimistic concurrency | `IsRowVersion()` ×2 | `StudentServiceConfiguration.cs:25`; `StudentServiceRequestConfiguration.cs:28` | ⚠️ mapped, not handled on write (Issue 4) |
| R12 | Cache-aside + scope re-check + localize-on-read | Per-object keys, TTLs, re-auth on hit, invalidate on mutation | `StudentServiceRequestService.cs:95-111`; `StudentServiceService.cs:61-76` | ✅ |
| — | Unique (request, field) value | Unique index `(StudentServiceRequestId, FieldDefinitionId)` | `ServiceFieldValueConfiguration.cs:31` | ✅ |
| — | Unique (workflow, from, to) transition | Unique index prevents ambiguous resolution | `WorkflowTransitionConfiguration.cs:23` | ✅ |

---

## 4. Findings

### Issue 1 — Workflow-configured transitions ignore `TransitionType`; the firing-mechanism guard is enforced only on the no-workflow fallback path
- **Reconstructed expectation:** R3/R4 — a transition's `TransitionType` (Manual/Automatic/Student) constrains *how* it may fire. Manual endpoints (Approve/Reject/MoveState) should only drive `Manual` transitions; `Automatic` transitions (e.g. `WaitingPayment → UnderReview`) should fire only through `ConfirmPaymentAsync`.
- **Implementation:** `EnsureTransitionAllowedAsync` takes an `expectedType` parameter (`StudentServiceRequestService.cs:567`). The **no-workflow fallback** enforces it — `WaitingPayment → UnderReview` is allowed only when `expectedType == WorkflowTransitionType.Automatic` (`:596`). But the **workflow-configured branch** (`:574-585`) calls `_workflows.ResolveTransitionAsync`, which matches on **`FromStatus`/`ToStatus` only** (`WorkflowService.cs:102-103`), and returns as soon as a row exists — it **never compares `transition.TransitionType` to `expectedType`**.
- **Evidence:** `StudentServiceRequestService.cs:574-585` vs `:589-602`; `WorkflowService.cs:94-105`.
- **Impact:** When a workflow is attached, a staff caller hitting a Manual endpoint can fire a transition the catalog marked `Automatic` or `Student` — including advancing a request out of `WaitingPayment` **without** payment confirmation, the exact move the fallback path forbids. Enforcement is inconsistent between the two code paths; the workflow path is the weaker one.
- **Severity:** Medium.

### Issue 2 — Per-transition `RequiredAction` verb is stored but never enforced at the service layer
- **Reconstructed expectation:** `WorkflowTransition.RequiredAction` is documented as "the permission action verb the caller must hold on the `student-services.requests` resource" for manual transitions (`WorkflowTransition.cs:21-23`).
- **Implementation:** The workflow branch of `EnsureTransitionAllowedAsync` resolves the transition and returns without reading `RequiredAction`; the comment explicitly defers to "the controller's `[HasPermission]` attribute" (`StudentServiceRequestService.cs:581-584`). No code path reads `transition.RequiredAction` for authorization (verified by grep — it is only mapped into responses).
- **Impact:** A workflow that assigns *different* action verbs to different transitions has no effect — the single uniform controller-level permission is the only gate. Any caller holding that one permission can perform every manual transition regardless of the catalog's per-transition verb. Privilege-granularity gap; the data model implies finer control than the code enforces. No full authorization bypass (the controller attribute still gates the endpoint).
- **Severity:** Low–Medium.

### Issue 3 — `SubmitAsync` fee path is a three-commit, non-atomic sequence with no compensation
- **Reconstructed expectation:** R5 — a fee-charging submission ends as a persisted request in `WaitingPayment` carrying its `PaymentReferenceId`, linked to a payable invoice.
- **Implementation:** `SubmitAsync` commits the request (`:248`), then `IFeeCreationService.CreateFeesAsync` commits the invoice in its own `SaveChangesAsync` (`FeeCreationService.cs:118`), then a third commit stores `PaymentReferenceId` + flips to `WaitingPayment` (`:273-276`).
- **Evidence:** `StudentServiceRequestService.cs:247-277`; `FeeCreationService.cs:114-121`.
- **Impact:** A failure between the invoice commit and the third commit leaves an invoice persisted but the request **without** `PaymentReferenceId` and still in its initial status. The `InvoicePaidEventHandler` keys advancement on `PaymentReferenceId == invoiceId` (`InvoicePaidEventHandler.cs:58`), so such a request can never be auto-advanced on payment. Recoverable (the invoice item carries `ReferenceId = request.Id`, `:266`), but there is no automatic reconciliation and the sequence is not wrapped in a transaction or staged through the outbox.
- **Severity:** Low.

### Issue 4 — `RowVersion` mapped on both entities but no `DbUpdateConcurrencyException` handling on any write path
- **Reconstructed expectation:** R11 — optimistic concurrency via rowversion.
- **Implementation:** `IsRowVersion()` is mapped for `StudentService` and `StudentServiceRequest`, but no write path (Submit/Cancel/Approve/Reject/MoveState/ConfirmPayment/Update/Toggle/Delete) catches `DbUpdateConcurrencyException`.
- **Evidence:** `StudentServiceConfiguration.cs:25`; `StudentServiceRequestConfiguration.cs:28`; service `SaveChangesAsync` calls throughout with no concurrency catch.
- **Impact:** Concurrent edits to the same request/service surface as an unhandled 500 rather than a translated conflict. Rowversion still prevents lost updates — only error-shaping is missing. (Identical pattern to the Student and Payments modules.)
- **Severity:** Low.

### Issue 5 — `ConfirmPaymentAsync` has no scope check or ownership guard (by design, undocumented)
- **Implementation:** Unlike every other request method, `ConfirmPaymentAsync` loads by id and mutates with **no** `CanAccessStudentAsync` and no `StudentId` ownership guard (`:460-498`).
- **Impact:** Documentation/traceability only — it is driven by the outbox system caller (`InvoicePaidEventHandler`, which has no user context) and a break-glass admin endpoint; per-student scope does not apply to a system caller. But with no model doc a maintainer cannot confirm the omission is deliberate. No defect: the break-glass route is permission-gated and the method no-ops outside `WaitingPayment`.
- **Severity:** Low (informational).

---

## 5. Hidden-Logic Review

- **At-least-once idempotency (`:474-486`, `InvoicePaidEventHandler.cs:58-86`).** `ConfirmPaymentAsync` no-ops (logged) on any status other than `WaitingPayment`, and the handler no-ops on an empty match list (invoices authored by other modules). The handler deliberately does **not** catch inside the loop so transient failures bubble to the dispatcher's retry/backoff. *Correct. No issue.*
- **Existence-leak discipline (`:511-541`).** Missing, wrong-owner (`entity.StudentId != studentId`), and out-of-scope all map to the same `RequestNotFound` — no cross-student probing. *No issue.*
- **Per-row bulk dispatch (`:393-427`).** `BulkTransitionAsync` calls the single-row `MoveStateAsync` per id so workflow validation, scope, and audit logging cannot be bypassed; failures coded NotFound/InvalidTransition/Validation. *No issue (inherits Issue 1's transition-type gap, not a new one).*
- **Dynamic-form validation (`:616-738`).** Duplicate field ids rejected up front (protecting the `(request, field)` unique index from surfacing a 500), required field/document presence, per-type format (number/date/bool/dropdown), file size + extension allow-list. Thorough. *No issue.*
- **Split-query on sibling collections (`StudentServiceRequestRepository.cs:29-31`).** FieldValues + Documents split to avoid Cartesian explosion. *No issue.*
- **`Restrict` on definition FKs (`ServiceFieldValueConfiguration.cs:24-27`, `ServiceDocumentSubmissionConfiguration.cs:23-26`).** Admins cannot delete a field/document definition still referenced by historical submissions. *No issue.*
- **Search on `Code` not bilingual `Name` (`StudentServiceRequestRepository.cs:82-88`).** Deliberate — a naive `Like` on bilingual JSON would false-match. *No issue.*
- **Wholesale field/document replacement on update (`StudentServiceService.cs:218-230`).** `Clear()` + re-add the full desired set; the admin UI sends the complete set. Note: this orphans nothing because children cascade, but it reassigns child ids on every update — acceptable given the definitions are referenced by submissions through `Restrict`, which would *block* the clear if live submissions referenced a definition being removed. *No verified issue* (the `Restrict` FK is the backstop).
- **`ServiceFieldValueConfiguration` comment cites an "upsert path on resubmission" (`:30`) that does not exist in code** — submissions only ever insert new values on a fresh request. Harmless stale comment; not a logic defect. *No issue.*

---

## 6. Flow Verification

**Configure service.** Validate → unique Code (filtered) → normalize bilingual → add fields/docs → save. **YES.**

**Configure workflow.** Validate → unique Code → add states + typed transitions (unique per from/to) → save. **YES**, but `TransitionType`/`RequiredAction` are stored without runtime enforcement (Issues 1–2).

**Submit request.** Validate → scope (own student) → load active service → validate dynamic form → resolve initial state → persist → (if fee) author invoice + flip WaitingPayment. **YES**, with the non-atomic fee caveat (Issue 3).

**Confirm payment (outbox).** Deserialize fact → match requests by `PaymentReferenceId` → per-request `ConfirmPaymentAsync` (no-op outside WaitingPayment) → resolve automatic next state (default UnderReview). **YES.**

**Staff transition (approve/reject/move).** Validate → scope load → `EnsureTransitionAllowedAsync` → mutate status + stamps → save → invalidate → notify. **YES**, but the workflow path does not enforce `TransitionType` (Issue 1).

**Cancel.** Validate → owner+scope load → block after processing began → Cancelled. **YES.**

**Read (by id).** Cache hit → scope re-check → localize/null; miss → repo (split-query children) → scope → cache culture-neutral → localize. **YES.**

---

## 7. Verdict

### Scores
- **Architecture:** 9/10 — Clean modular boundaries (id/CSV-only cross-module refs), declarative workflow catalog, outbox-driven payment bridge, dynamic-form engine, filtered-unique indexes, soft-delete, `Restrict`-protected definitions, per-object caching. No model doc to anchor it (traceability gap).
- **Logic:** 7/10 — Request lifecycle, dynamic validation, and idempotent payment confirmation are solid. Docked for the workflow path ignoring `TransitionType` (Issue 1) and `RequiredAction` going unenforced (Issue 2) — the workflow data model promises more constraint than the engine applies.
- **Operational Safety:** 7/10 — Scope re-check on cache hits, existence-leak NotFounds, at-least-once-safe confirmation, soft-delete. Docked for the non-atomic three-commit fee submit (Issue 3) and unhandled rowversion conflicts surfacing as 500s (Issue 4).
- **Maintainability:** 8/10 — Heavily commented, consistent with platform caching/scope/outbox/localization conventions. The unenforced workflow metadata, a stale config comment, and the absent model doc are the blemishes.

### Confirmed Issues
1. (Medium) Workflow-configured path ignores `TransitionType`; firing-mechanism guard enforced only on the no-workflow fallback — `StudentServiceRequestService.cs:574-602`, `WorkflowService.cs:102-103`.
2. (Low–Medium) Per-transition `RequiredAction` verb stored but never enforced; uniform controller permission is the only gate — `WorkflowTransition.cs:21-23`, `StudentServiceRequestService.cs:581-584`.
3. (Low) `SubmitAsync` fee path is a three-commit, non-atomic sequence; a mid-sequence failure orphans the request from its invoice — `StudentServiceRequestService.cs:247-277`.
4. (Low) `RowVersion` mapped on both entities but no `DbUpdateConcurrencyException` handling on any write path — `StudentServiceConfiguration.cs:25`, `StudentServiceRequestConfiguration.cs:28`.
5. (Low, informational) `ConfirmPaymentAsync` has no scope/ownership guard — by design (outbox + break-glass) — `StudentServiceRequestService.cs:460-498`.

### False Positives Cleared
- "Duplicate payment confirmation double-advances a request" — `ConfirmPaymentAsync` no-ops outside `WaitingPayment`; the handler no-ops on empty matches. At-least-once safe.
- "Deleting a field definition breaks historical submissions" — `Restrict` FK on `ServiceFieldValue`/`ServiceDocumentSubmission` blocks it.
- "Bulk transition bypasses workflow/scope checks" — per-row dispatch through the single-row method.
- "Free-text request search false-matches bilingual names" — search deliberately scoped to `StudentService.Code`.

### Findings Summary
A large, carefully-built module whose request lifecycle, dynamic-form validation, and outbox-driven payment confirmation all hold up. The real gaps cluster in the **workflow engine's enforcement**: when a workflow is attached, the engine resolves transitions by `(from, to)` only and **drops both the `TransitionType` and `RequiredAction` constraints** the catalog defines (Issues 1–2) — the no-workflow fallback is actually the stricter path. Secondary gaps are the **non-atomic three-commit fee submission** (Issue 3) and the platform-wide **unhandled-rowversion** pattern (Issue 4). No data-integrity or outright authorization-bypass defect found; the workflow-metadata enforcement gap is the item most worth a maintainer's attention.
