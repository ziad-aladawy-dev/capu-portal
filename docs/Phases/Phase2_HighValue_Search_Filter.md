# Phase 2 — High-value search and filter

**Tier:** P2
**Status:** all 10 items landed (see per-item notes below). Build clean.
**Goal:** add list/search/filter on resources where the current API forces "fetch everything and filter client-side" or "fetch by a single foreign key." Finance and operations dashboards are the immediate beneficiaries.

> **Read first:** [Cross-cutting design](#cross-cutting-design) — the shared paged-query base ships before any item.

## Item table

| # | Item | Resource | Status | Owner | Notes |
|---|---|---|---|---|---|
| 2.1 | Invoice list with filters | Invoices | Done | | `GET /api/invoices` |
| 2.2 | Course offering list (cross-node) | CourseOfferings | Done | | `GET /api/course-offerings` (kept narrow `node/.../semester/...` as convenience) |
| 2.3 | Notification list with pagination + filters | Notifications | Done | | `GET /api/notifications/search` |
| 2.4 | Payment transaction list with filters | PaymentTransactions | Done | | `GET /api/payments/transactions` |
| 2.5 | Schedule slot list with filters | ScheduleSlots | Done | | `GET /api/schedule-slots` |
| 2.6 | Course catalog search | Courses | Done | | `GET /api/courses/search` |
| 2.7 | Academic plan search | AcademicPlans | Done | | `GET /api/academic-plans/search` |
| 2.8 | Role search + filter (extend existing pagination) | Roles | Done | | `GetRolesRequest` extended with `Search` + `IsSystem` |
| 2.9 | Profile records additional filters | StudentProfileRecords | Done | | Filters added to existing `GET /api/students/{id}/profile-records` |
| 2.10 | Student services catalog search | StudentServices catalog | Done | | `GET /api/student-services/search` (surfaces existing service-layer pagination) |

## Cross-cutting prep (landed first)

[src/2.Core/CapitalUniversity.Core.Abstractions/Shared/Paging/](../../src/2.Core/CapitalUniversity.Core.Abstractions/Shared/Paging/):
- `PagedQueryRequest.cs` — abstract base with `Search`, `Page`, `PageSize`, `Sort`. Provides `NormalizedPage` and `NormalizedPageSize` (server-clamped to `MaxPageSize`).
- `PagingConstants.cs` — `MaxPageSize = 100`, `DefaultPageSize = 20`.
- `SortClause.cs` — `Parse(raw, allowedFields)` returns the parsed clauses or throws `ValidationException` on unknown field / direction. Wire format: `sort=field:asc|desc,field2:...`.

`PagedResult<T>` (already in `Core.Abstractions/Shared/`) is the uniform response envelope.

## Per-item context (what landed)

### 2.1 Invoice list with filters
- `GET /api/invoices` — filters: `studentId`, `status`, `issuedFrom/To`, `dueFrom/To`, `minAmount/maxAmount`, `currency`, free-text `search` (currency).
- Sort whitelist: `createdAt|dueAt|amount|status`. Default `createdAt:desc`.
- New DTO: [InvoiceSearchQuery.cs](../../src/4.Modules/CapitalUniversity.Module.Payments.Abstractions/Payments/DTOs/InvoiceSearchQuery.cs).
- Repository: `IInvoiceRepository.SearchAsync` builds a single SQL query (no N+1 — totals already denormalized on `Invoice`).
- Service: `InvoiceService.SearchAsync` applies pinned-`studentId` scope check then projects slim summary DTOs (drops `Items`).

### 2.2 Course offering cross-node list
- `GET /api/course-offerings` — filters: `semesterId`, `structureNodeId`, `courseId`, `status`, `registrationState`, free-text `search` (section code).
- Sort: `createdAt|sectionCode|status`. Default `createdAt:desc`.
- Existing `GET /node/{nodeId}/semester/{semesterId}` kept as convenience.
- Out-of-scope offerings filtered post-query (total stays as raw count — admin-typical).

### 2.3 Notification list with pagination + filters
- `GET /api/notifications/search` — filters: `isRead`, `type`, `from/to`, free-text on title/message.
- Always scoped to the caller. Default sort `createdAt:desc`.
- Existing `GET /api/notifications` and `GET /api/notifications/unread` kept (no breakage).
- Service: `INotificationService.SearchAsync` runs the query directly on `CoreDbContext` (no separate repo layer for notifications).

### 2.4 Payment transactions list with filters
- `GET /api/payments/transactions` — filters: `invoiceId`, `studentId` (joins Invoice), `status`, `provider`, `from/to`, amount range, free-text on provider + transaction id.
- Sort: `createdAt|amount|status`.
- Repository: `IInvoiceRepository.SearchTransactionsAsync` (kept on the same repository — `PaymentTransaction` is owned by the Payments module and lives next to invoices).

### 2.5 Schedule slot list with filters
- `GET /api/schedule-slots` — filters: `courseOfferingId`, `dayOfWeek`, `kind`, `from/to` (time-of-day window).
- Sort: `dayOfWeek|startTime|createdAt`. Default `dayOfWeek:asc,startTime:asc`.
- Per-row parent-offering visibility check post-query.

### 2.6 Course catalog search
- `GET /api/courses/search` — filters: `category`, `isActive`, credit hours range; free-text on code/title.
- Sort: `code|creditHours|createdAt`. Default `code:asc`.

### 2.7 Academic plan search
- `GET /api/academic-plans/search` — filters: `structureNodeId`, `isActive`, `effectiveFromInclusive/Exclusive`; free-text on name.
- Sort: `name|effectiveFrom|createdAt`.
- Per-row scope filter post-query.

### 2.8 Role search + filter
- `GetRolesRequest` extended with `Search` (name contains) and `IsSystem` (filter on system-role flag).
- Existing pagination preserved — no breaking change to the existing `GET /api/roles` route.

### 2.9 Profile records additional filters
- Existing `GET /api/students/{studentId}/profile-records` now accepts optional `category`, `verifiedOnly`, `verifiedFrom`, `verifiedTo` query params.
- Filtering is in-memory after the per-student fetch — per-student record count is bounded, simpler than a separate repo path.

### 2.10 Student services catalog search
- `GET /api/student-services/search` — surfaces the existing `IStudentServiceService.GetAllAsync(page, pageSize, search, isActive)` that previously had no public route.
- Caps `pageSize` at 100 inline.

## Cross-cutting design

### Shared paged-query base

```csharp
public abstract class PagedQueryRequest
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Comma-separated <c>field:asc|desc</c> pairs.</summary>
    public string? Sort { get; set; }

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, PagingConstants.MaxPageSize);
}
```

**Rules adopted:**
- Page size silently clamped server-side to `MaxPageSize = 100` (not 400).
- Sort encoding: `sort=field:asc,field2:desc`. Direction defaults to `asc`. Unknown fields → 400 (`ValidationException`) — typos never silently fall back to default.
- Date convention: `*From` inclusive, `*To` exclusive. Documented per DTO.
- `PagedResult<T>` is the uniform response envelope.
- Free-text `Search` semantics are per-endpoint (different resources have different searchable columns) — kept on the base for ergonomics but each repo decides what to match.

### Scope filtering pattern

Two patterns in use depending on the resource:
1. **Pinned id with scope short-circuit:** if the caller filters by a specific id (e.g. `studentId` on invoice search), check that one id up front and return an empty page if denied. No existence leak.
2. **Cross-id queries with post-fetch filter:** materialize the paged set, then drop rows the caller cannot see. Total stays as the raw count — most callers of these endpoints have unrestricted scope (admin). Narrow-scope readers see a shrinking page; the trade-off keeps the query single-pass.

### Deferred / NOT implemented in this phase

- **Roles full migration to `PagedQueryRequest` base** — existing `GetRolesRequest` extended in place to avoid breaking the existing CQRS handler. Migration to the shared base is a Phase-3 (consistency cleanup) item.
- **`StudentQueryRequest` / `StaffQueryRequest` migration to the shared base** — Phase-3 cleanup item.
- **Per-resource sort-field documentation in OpenAPI** — not added; each endpoint's `<summary>` lists the whitelist but it's not machine-readable yet.

### Open questions
- Should list endpoints emit `totalCount` always, or accept `?withCount=false` for cheap pages? (Affects index strategy.) — Not addressed; current implementation always counts.
- _Add as you go._
