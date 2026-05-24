# Phases — Bulk Actions, Search/Filter, and API Cleanup

Editable phase plan derived from the 2026-05-24 audit of controllers, domain models, and permission manifests on branch `feat/infra-hardening`.

Each phase file is a scaffold: tick items off, reassign priority, edit scope, add or remove rows. Nothing here is a commitment until the **Status** column says so.

## Index

| Phase | File | Theme | Tier |
|---|---|---|---|
| 0 | [Phase0_Security_And_Correctness.md](Phase0_Security_And_Correctness.md) | Close auth gaps and fix non-idempotent endpoints found during audit | P0 |
| 1 | [Phase1_HighValue_Bulk_Actions.md](Phase1_HighValue_Bulk_Actions.md) | The bulk endpoints admins actually need today | P1 |
| 2 | [Phase2_HighValue_Search_Filter.md](Phase2_HighValue_Search_Filter.md) | Search/filter on Invoices, Offerings, Notifications, Transactions | P2 |
| 3 | [Phase3_Consistency_Cleanup.md](Phase3_Consistency_Cleanup.md) | Shared paged-query base, B-tier bulk, dedupe endpoints | P3 |
| 4 | [Phase4_Deferred.md](Phase4_Deferred.md) | Explicitly not building; revisit if pain emerges | P4 |

## Conventions used in these files

- **Status column values:** `Proposed` (default), `Accepted`, `In progress`, `Done`, `Dropped`.
- **Owner column:** GitHub handle or team.
- **Notes column:** for one-line context; longer discussion goes in the per-item section below the table.
- **Endpoint shapes** are suggestions, not contracts. Edit freely.
- **Acceptance criteria** stubs are intentionally generic — refine per item before work starts.

## Cross-cutting design decisions to lock in BEFORE Phase 1 work begins

Two patterns recur across many items. Agreeing on them once avoids drift later:

1. **Bulk operation envelope** — see [Phase1_HighValue_Bulk_Actions.md](Phase1_HighValue_Bulk_Actions.md#cross-cutting-design) for the proposed `BulkTransitionRequest<TStatus>` / per-row outcome shape.
2. **Paged query base** — see [Phase2_HighValue_Search_Filter.md](Phase2_HighValue_Search_Filter.md#cross-cutting-design) for the proposed `PagedQueryRequest` base, sort encoding, and `PageSize` cap.
