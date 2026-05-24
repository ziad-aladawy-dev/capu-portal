# Phase 0 — Security and correctness fixes

**Tier:** P0
**Why first:** these were surfaced during the bulk/search audit but are independent of it. Several controllers carry mutating endpoints with `[AllowAnonymous]`, and a couple of non-idempotent shapes are easy to flip in passing.

## Item table

| #  | Item                                                                                                                          | Status | Owner | Notes |
|----|-------------------------------------------------------------------------------------------------------------------------------|--------|-------|-------|
| 0.8| Clarify `POST /api/academic-years/resolve` and `POST /api/semesters/resolve` — verb mismatch (POST for what looks like a read)| Done   |       | Documented as write; added `recompute-current` route; `resolve` kept as deprecated alias |

## Per-item context

### 0.8 Resolve verb mismatch

**Investigation result:** both endpoints are **writes** that mutate `IsCurrent` flags across all year/semester rows against the server's UTC clock and persist via `SaveChangesAsync` (two-phase commit to satisfy a filtered UNIQUE index). They are also **idempotent** — the same logic runs on a background timer in [AcademicTimelineBackgroundService.cs](../../src/2.Core/CapitalUniversity.Core.Infrastructure/Services/Semesters/AcademicTimelineBackgroundService.cs); the controller endpoints are the manual trigger. POST is the correct HTTP verb; the route name `resolve` was the only problem.

**Decision:**
- POST stays. The operation is a write, not a query — switching to GET would be wrong.
- Canonical route renamed to `recompute-current` (action-y, accurate).
- The `resolve` route is retained as a second `[HttpPost]` alias on the same action so the existing frontend ([AcademicCalendarPage.jsx:230,240](../../frontend/src/modules/academic/pages/AcademicCalendarPage.jsx:230)) and any external callers keep working. New callers should use `recompute-current`.
- Service method names (`ResolveCurrentYearAsync`, `ResolveCurrentSemesterAsync`) intentionally **not** renamed. That would ripple across 13 files including the background service and tests with no caller-visible benefit. Cosmetic-only — defer to a separate cleanup if desired.

**What changed:**
- [AcademicYearsController.cs:69-94](../../src/1.API/CapitalUniversity.API/Controllers/AcademicYearsController.cs:69) — added `recompute-current` route, kept `resolve` alias, added `<summary>` and `<remarks>` documenting the write semantics and idempotency, updated response message to `"Current academic year flag recomputed"`.
- [SemestersController.cs:62-87](../../src/1.API/CapitalUniversity.API/Controllers/SemestersController.cs:62) — symmetric change for semesters.
- [IAcademicYearService.cs:13](../../src/2.Core/CapitalUniversity.Core.Abstractions/Semesters/IAcademicYearService.cs:13) — added `<summary>` noting WRITE operation, affected columns, idempotency, and the two-phase commit reason.
- [ISemesterService.cs:13](../../src/2.Core/CapitalUniversity.Core.Abstractions/Semesters/ISemesterService.cs:13) — symmetric service-interface doc.

**Frontend follow-up (not in scope):** at the next frontend touch on `AcademicCalendarPage.jsx`, swap the `resolve` calls for `recompute-current`. Until then the alias keeps the page working.

## Open questions
- _Add as you go._
