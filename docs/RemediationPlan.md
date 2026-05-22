Audit Remediation Plan (FINAL DEPLOY VERSION)

Scope: Cross-cutting infrastructure, Semesters, Courses, Payments/Fees, Student Information
Teammate-owned modules remain untouched.

P0 — System-Wide Non-Negotiable Rules
P0.1 Scope Enforcement Boundary

Scope applies ONLY to:

Invoice
AcademicPlan
StudentProfileRecord

All other modules remain global.

P0.2 Authorization Order
Resolve user identity
Compute IUserScope
Apply IEffectiveScope
Execute query
THEN caching (if applicable)
P0.3 Caching Rule (GLOBAL)
Cache only stores authorized projection results
NEVER cache raw domain entities before authorization
NO scope-based cache keys
Sensitive records are never cached
P0.4 Foreign Key Rule
ALL relationships inside monolith MUST use EF Core schema-level foreign keys
No service-level FK validation layer is needed
P0.5 Pagination Rule
Cursor pagination is default everywhere
Offset pagination ONLY for admin endpoints
Max page size = 100
P0.6 Soft Delete Rule
Implement via ISoftDeletable interface + EF global query filter
Apply ONLY to:
Invoice
PaymentTransaction
StudentProfileRecord
NOT applied to:
Course
AcademicPlan
AcademicPlanCourse
P0.7 Transaction Boundary Standard
1 service method = 1 transaction
No nested SaveChanges inside workflows
Retry logic ONLY at service layer
No implicit transactions in repositories
P0.8 Concurrency Handling Standard
Never suppress DbUpdateConcurrencyException
Always:
reload entity
reapply mutation logic
retry max 3 times
After failure → return 409 Conflict
P1 — Critical Fixes
P1.1 Scope Model (Hybrid Scoped Authorization)

Implement:

IUserScope
IEffectiveScope

Apply ONLY to:

InvoiceService
AcademicPlanService
StudentProfileService

Rules:

Out-of-scope → return 404 (avoid existence leakage)
Scope computed from StaffRoles + structural + temporal grants
P1.2 Cache Alignment
Cache runs AFTER authorization
Cache stores only authorized projections
No cache key embedding of scope
P1.3 Payment Idempotency (D7)
Use UNIQUE constraint on (InvoiceId, IdempotencyKey)
On duplicate insert:
catch DbUpdateException
fetch existing record
return it as success (200 OK)

No retries, no locks.

P1.4 Concurrency Model (D4 Unified RowVersion Standard)

Apply RowVersion to:

Invoice
AcademicPlan
StudentProfileRecord
Course

Rules:

EF Core .IsRowVersion()
Detect DbUpdateConcurrencyException
Retry max 3 times
Reload from DB on each retry
Final failure → 409 Conflict
P1.5 Soft Delete Enforcement (ISoftDeletable)
Add global query filter for soft-deletable entities
Delete = set IsDeleted = true
Admin bypass = IgnoreQueryFilters()
P1.6 Foreign Keys Enforcement
Add EF Core FK constraints for:
Invoice → Student
AcademicPlan → StructureNode
StudentProfileRecord → Student
PaymentTransaction → Invoice
No navigations required, but FK must exist
P2 — High Priority
P2.1 Pagination Implementation
Cursor pagination for all APIs
Offset only admin endpoints
Enforce max page size = 100
P2.2 Batch Caching (Post-Auth Only)
Cache active course lists AFTER authorization
Cache invoices per student AFTER authorization
Safe invalidation per mutation
P2.3 Sensitive Data Audit Logging
Log ALL reads of sensitive StudentProfileRecord
Include:
userId
recordId
timestamp
P2.4 RolePermission Validation
Validate Resource against PermissionManifestRegistry
Prevent invalid permission creation
P2.5 Cache Stampede Protection
Use SemaphoreSlim per key OR HybridCache (.NET 9 recommended)
P2.6 Session Cache Invalidation Fix
Remove cached SessionVersion on user creation/update
P3 — Medium Priority
P3.1 UTC Enforcement
Enforce UTC DateTime conversion globally via EF conventions
P3.2 JSON Validation Constraints
Enforce ISJSON() constraints on:
StudentProfileRecord.DataJson
PaymentTransaction.RawPayloadJson
P3.3 Course Code Normalization
Trim + uppercase Course.Code
Apply consistent collation
P3.4 Unique Filtered Index
StudentProfileRecord uniqueness:
(StudentId, Category, CustomCategoryKey)
Filtered by IsDeleted = 0
P3.5 Unit of Work Simplification
Keep only SaveChangesAsync abstraction
No per-module UoW
P3.6 EF Snapshot Cleanup
Regenerate EF migration snapshot cleanly
Remove suppressed warnings workaround
P3.7 Payment Validation + Constraints
Validate Amount rules (no negative invalid states)
Add JSON size constraints for payload safety
P3.8 Refund State Handling
Keep enum states reserved (do not implement refunds yet)
P3.9 Cache Stampede Protection (Global Safety)
Prevent duplicate expensive loads under concurrency
P3.10 Exception Localization
Replace raw exception messages with localization keys
Standardize error responses
P4 — Low Priority / Hygiene
Cascade delete rules cleanup
Index cleanup
CorrelationId validation strictness
Audit log hashing improvement
Outbox retry refinement
FK cleanup on legacy constraints
Currency limitation documentation
Testing Strategy (FINALIZED)
Required Integration Tests
Scope isolation test:
user A cannot access user B invoice → 404
Payment idempotency test:
duplicate webhook → single record + 200 OK
Concurrency test:
concurrent invoice updates → final consistent state or 409
Soft delete test:
deleted record hidden from list but accessible via admin
Architecture Tests (MINIMAL ONLY)
Dependency direction enforcement
Authorization attribute coverage
Cache prefix consistency