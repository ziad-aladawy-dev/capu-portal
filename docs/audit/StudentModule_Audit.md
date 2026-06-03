# Student (Student Information) Module — Reverse-Audit (Undocumented Logic)

**Status:** No model document exists for this module. The code comment at `StudentProfileRecord.cs:9-10` cites `docs/Plan.md` "Student Information Rules" — **that file is not present in `docs/`**. This is a **reverse audit**: the implicit contract is reconstructed from code, then verified for internal consistency, correctness, safety, and hidden logic.

**Branch:** `sync_platform_hardening`
**Audit date:** 2026-06-02
**Method:** Same 7-step rigor as the documented audits; "Model Extract" is reconstructed from code instead of a doc. Only proven, evidence-backed findings. No code changed.

---

## 1. Reconstructed Model (implicit contract from code)

**Purpose.** Owns sparse, optional, regulation-driven facts about a student, stored as opaque JSON blobs (one row per category) rather than per-category SQL tables or key-value tables.

**Reconstructed assumptions / invariants.**
- R1. Profile data is schemaless JSON; the module never interprets `DataJson` (`StudentProfileRecord.cs:13-16, 39-40`).
- R2. `SchemaVersion` is per-category, producer-defined; consumers parse against the version they understand (`:36-37`).
- R3. At most one **live** record per `(StudentId, Category, CustomCategoryKey)` (`StudentProfileService.cs:16-19`).
- R4. `CustomCategoryKey` only meaningful when `Category == Custom`; forced empty otherwise (`StudentProfileService.cs:110-112`).
- R5. Cross-module reference to student is **id-only, no EF navigation** (modularity rule) (`StudentProfileRecord.cs:25-26`).
- R6. `IsSensitive` (medical/disability/military) drives shorter cache TTL + audit logging (`:47-48`, `StudentProfileService.cs:28-29, 272-273`).
- R7. Editing data **clears verification stamps** — re-verification required after change (`StudentProfileService.cs:120-123`).
- R8. Scope enforced on every read/write via `IEffectiveScope.CanAccessStudentAsync`; out-of-scope → null/404, never leak (`:63, 69, 78, 89, 105`).
- R9. Soft-delete (`ISoftDeletable`); soft-deleted row may be re-added (filtered unique index) (`StudentProfileRecordConfiguration.cs:48-52`).
- R10. Optimistic concurrency via SQL `rowversion` (`StudentProfileRecord.cs:28-29`, config `:27`).

**Expected flow.** Upsert keys on `(StudentId, Category, CustomCategoryKey)` (read-then-insert-or-update); Verify stamps `VerifiedBy/VerifiedAt`; Delete soft-deletes; bulk variants wrap singles with per-item failure isolation; reads are cache-aside on `studentprofile:object:{id:N}` with scope re-check on every hit.

---

## 2. Implementation Files

- Domain: `Domain/StudentProfileRecord.cs`
- Application: `Application/StudentProfileService.cs`, `Application/Validators/StudentProfileValidators.cs`
- Persistence: `Persistence/Configurations/StudentProfileRecordConfiguration.cs`
- Repository: `Repositories/IStudentProfileRecordRepository.cs`, `Repositories/StudentProfileRecordRepository.cs`
- DI/Manifest: `StudentModuleExtensions.cs` (+ `Abstractions.Manifest.StudentInformationPermissionManifest`)

---

## 3. Invariant Verification

| # | Invariant | Enforcement | Evidence | Holds |
|---|-----------|-------------|----------|-------|
| R1 | JSON opaque, module never interprets | `DataJson` treated as blob; validated only for well-formedness + size | `StudentProfileValidators.cs:14-25` | ✅ |
| R2 | Per-category schema version | `SchemaVersion >= 0`, stored, never interpreted | `Validators:13`; service copies through | ✅ |
| R3 | One live record per (Student, Category, CustomKey) | Filtered unique index `IsDeleted=0` + read-before-write upsert | `Config:48-52`; `Service:114-129` | ✅ (index is the real guard; service read is best-effort — see Issue 1) |
| R4 | CustomKey only for Custom category | Forced empty when not Custom | `Service:110-112`; validator requires it when Custom (`Validators:26-30`) | ✅ |
| R5 | Id-only cross-module ref | `HasOne<StudentEntity>().WithMany().HasForeignKey(StudentId)`, no nav property | `Config:36-39` | ✅ |
| R6 | Sensitive → short TTL | `SensitiveTtl=2m` vs `StandardTtl=15m` | `Service:28-29, 272-273` | ✅ |
| R7 | Edit clears verification | `VerifiedBy/At = null` on update path | `Service:120-123` | ✅ |
| R8 | Scope on every op | `CanAccessStudentAsync` on get/getlist/getcat/upsert/verify/delete | `Service:63,69,78,89,105,166,190` | ✅ |
| R9 | Soft-delete, re-addable | `HasQueryFilter(!IsDeleted)`; unique index filtered on `IsDeleted=0` | `Config:33, 48-52` | ✅ |
| R10 | Optimistic concurrency | `IsRowVersion()` | `Config:27` | ⚠️ present but not handled on write (see Issue 2) |
| — | FK delete behavior | `Restrict` (student delete blocked while records exist) | `Config:39` | ✅ |
| — | Cross-student id probe defense | id whose `StudentId != route studentId` → identical `NotFoundException` | `Service:158-164, 184-188` | ✅ |

---

## 4. Findings

### Issue 1 — Upsert is read-then-write; concurrent duplicate insert maps to an unhandled `DbUpdateException`, not `ConflictException`
- **Reconstructed expectation:** R3 — at most one live record per category; bulk upsert isolates per-item failures and is documented to catch `ConflictException` (`Service:223-226`).
- **Implementation:** `UpsertAsync` reads by category then inserts when none found (`Service:114, 131-142`). Two concurrent upserts for the same new `(StudentId, Category, CustomKey)` both miss the read and both insert; the filtered unique index (`Config:50-52`) rejects the second at `SaveChangesAsync` with a `DbUpdateException` (unique violation). Nothing translates that into `ConflictException`.
- **Evidence:** `Service:114-142` (no try/catch around insert/save); `BatchUpsertAsync:215-226` catches only `NotFoundException`, `ValidationException`, `ConflictException`.
- **Impact:** The unique index correctly prevents the duplicate (data integrity is safe), but the failure surfaces as an unhandled `DbUpdateException` → 500. Inside `BatchUpsertAsync` it is **not caught**, so a single racing row aborts the entire batch rather than being recorded as one `Conflict` failure. The `catch (ConflictException)` arm is therefore effectively dead for this path.
- **Severity:** Low–Medium (correctness of partial-success contract under concurrency; integrity itself is safe).

### Issue 2 — `RowVersion` configured but no concurrency-exception handling on update/verify
- **Reconstructed expectation:** R10 — optimistic concurrency via rowversion.
- **Implementation:** `IsRowVersion()` is mapped, but `UpsertAsync`/`VerifyAsync`/`DeleteAsync` call `SaveChangesAsync` without catching `DbUpdateConcurrencyException`.
- **Evidence:** `Service:126, 175, 196`; `Config:27`.
- **Impact:** Concurrent edits to the same record surface as an unhandled 500 rather than a translated conflict result. The rowversion still prevents lost updates (last-writer does not silently clobber) — only the error shaping is missing.
- **Severity:** Low.

### Issue 3 — References a non-existent design document
- **Implementation:** `StudentProfileRecord.cs:9-10` cites `docs/Plan.md` "Student Information Rules" as the authority for "no per-category tables / no key-value tables." No `docs/Plan.md` exists in the repository.
- **Impact:** Documentation/traceability only — the rule is nonetheless honored in code (single JSON-blob table). Maintainers cannot locate the cited authority.
- **Severity:** Low.

---

## 5. Hidden-Logic Review

- **Scope re-check on cache hit (`Service:60-64`).** A cached sensitive DTO is re-authorized against `cached.StudentId` on every read; out-of-scope returns null. Correct — caching does not bypass authorization. *No verified issue.*
- **Cross-student id probing (`Service:158-164, 184-188`).** `Verify`/`Delete` return `NotFoundException` (not Forbidden) when `record.StudentId != studentId`, identical to a truly missing record — prevents existence-probing of other students' record ids. *No verified issue.*
- **`SlotId` synthetic GUID (`Service:263-268`).** Encodes the request index into the last 4 bytes so batch-upsert failures correlate to input position pre-persistence. Deterministic, collision-free for a single request. *No verified issue.*
- **Insert path skips cache removal (`Service:131-142`).** Correct — a never-persisted id was never cached; only the update path invalidates (`:127`). *No verified issue.*
- **64 KB `DataJson` cap (`Validators:23`).** Guards the cache layer from multi-MB blobs reloaded on every cached read. Reasonable, documented rationale. *No verified issue.*

---

## 6. Flow Verification

**Upsert.** Validate → scope check → resolve custom key → read by category → update (clear verify stamps, save, invalidate cache) or insert (save). Match to reconstructed intent: **YES**, with the concurrency caveat (Issue 1).

**Verify.** Validate → load by id → ownership guard → scope check → stamp → save → invalidate. **YES.**

**Delete.** Load by id → ownership guard → scope check → soft-delete → save → invalidate. **YES.**

**Read (by id).** Cache hit → scope re-check → return/null; miss → repo → scope check → cache with TTL-by-sensitivity. **YES.**

**Bulk.** Per-item try/catch wrapping the single-item methods; failures coded NotFound/Validation/Conflict. **YES**, except unhandled `DbUpdateException` under race escapes the loop (Issue 1).

---

## 7. Verdict

### Scores
- **Architecture:** 9/10 — Clean id-only modular reference, JSON-blob design honored, filtered unique index, soft-delete query filter, permission manifest, EF-assembly contribution. Cites a missing doc (Issue 3).
- **Logic:** 8/10 — Invariants enforced at the DB (unique index) and service; scope and ownership guards are solid. Docked for the read-then-write upsert race not mapping to the partial-success contract (Issue 1).
- **Operational Safety:** 8/10 — Scope re-check on cache hits, sensitive-data TTL caps, probe-resistant 404s, `Restrict` FK. Docked for unhandled concurrency/duplicate exceptions surfacing as 500s (Issues 1–2).
- **Maintainability:** 8/10 — Well-commented, consistent with platform caching/scope conventions. Dead `ConflictException` catch arm and the missing-doc citation are minor blemishes.

### Confirmed Issues
1. (Low–Medium) Concurrent duplicate upsert → unhandled `DbUpdateException`; not caught in `BatchUpsertAsync`, aborting the whole batch — `StudentProfileService.cs:114-142, 215-226`.
2. (Low) `RowVersion` mapped but no `DbUpdateConcurrencyException` handling on write paths — `StudentProfileService.cs:126,175,196`.
3. (Low) Cites non-existent `docs/Plan.md` — `StudentProfileRecord.cs:9-10`.

### False Positives Cleared
- "JSON blob + no schema = unbounded payload risk" — bounded at 64 KB with JSON well-formedness validation.
- "Caching sensitive medical data" — re-authorized on every hit + 2-minute TTL cap; no global dataset cache.
- "Id-only ref = orphan risk" — FK is `Restrict`, blocking student delete while records exist.

### Findings Summary
A small, well-built module. Every reconstructed invariant is enforced, with the **filtered unique index** as the authoritative guard for "one record per category" and **scope + ownership guards** that resist cross-student probing. The only real gaps are **error-shaping under concurrency** (duplicate/rowversion conflicts surface as 500s, and the duplicate case escapes batch isolation) and a **dangling reference to a missing `docs/Plan.md`**. No data-integrity or authorization defect found.
