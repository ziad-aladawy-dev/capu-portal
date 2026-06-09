# Distributed Caching Strategy

# Goal

The caching architecture is designed to:

- support 10k+ concurrent users
- reduce database pressure
- minimize Redis memory duplication
- preserve authorization and scope isolation
- support modular architecture
- reduce serialization overhead
- provide predictable invalidation behavior
- scale horizontally

The system uses a hybrid:

- shared object cache
- user/scope reference cache

strategy.

---

# Core Caching Principle

The architecture separates:

1. full object payload storage
2. user/scope-specific references

Instead of duplicating complete serialized payloads for every user.

---

# Main Design

# Shared Object Cache

The system stores the FULL serialized object payload once.

Example:

```text
course:object:{id}
academicplan:object:{id}
invoice:object:{id}
studentprofile:object:{id}
```

Example payload:

```json
{
  "id": "course-guid",
  "code": "CS101",
  "title": "Algorithms",
  "creditHours": 3,
  "category": "ProgramRequirement"
}
```

This object contains the complete read model required for retrieval.

---

# User/Scope Reference Cache

User-specific datasets store only lightweight references.

Example:

```text
user:15:visible_courses
```

Value:

```json
[
  "course-guid-1",
  "course-guid-2",
  "course-guid-3"
]
```

The system then resolves the IDs using the shared object cache.

---

# Retrieval Flow

Expected retrieval strategy:

```text
Request
   ->
User/scope reference lookup
   ->
Shared object cache lookup
   ->
Database fallback for missing objects
   ->
Populate shared object cache
```

Example:

```text
user:15:visible_courses
    ->
[
  "course-id-1",
  "course-id-2"
]

course:object:course-id-1
course:object:course-id-2
```

This prevents payload duplication while still allowing fast full-object retrieval.

---

# Why This Strategy Exists

Without this architecture:

```text
user:15:courses -> full payloads
user:18:courses -> same full payloads
user:22:courses -> same full payloads
```

Redis memory usage explodes quickly.

Instead:

- objects exist once
- users reference objects
- invalidation becomes simpler
- serialization cost decreases
- memory efficiency improves

---

# Allowed Cache Content

The shared object cache MAY store:

- full serialized object payloads
- complete read models
- denormalized projections
- immutable snapshots
- semi-static query results
- DTOs optimized for reads

This is REQUIRED to minimize repeated database queries.

---

# Forbidden Cache Content

Do NOT cache:

- EF tracked entities
- DbContext-bound objects
- lazy-loading proxies
- circular navigation graphs
- ORM runtime state
- active unit-of-work state

Objects stored in Redis must be detached serialized models.

---

# Cache Layers

# Layer 1 — Shared Object Cache

Contains reusable full payloads.

Examples:

```text
course:object:{id}
academicplan:object:{id}
structure:object:{id}
invoice:object:{id}
studentprofile:object:{id}
```

Characteristics:

- shared across users
- reusable
- detached
- serialized
- long-lived when semi-static

---

# Layer 2 — User/Scope Reference Cache

Contains lightweight ID references only.

Examples:

```text
user:{id}:visible_courses
user:{id}:academicplans
scope:{scopeId}:students
scope:{scopeId}:payments
```

Characteristics:

- small payloads
- authorization-aware
- scope-aware
- cheap invalidation
- cheap memory usage

---

# Layer 3 — Request Scope Cache

Short-lived in-memory request cache.

Purpose:

- avoid duplicate Redis hits in same request
- avoid repeated authorization resolution
- avoid repeated localization resolution
- avoid repeated context extraction

Can use:

```csharp
HttpContext.Items
```

or scoped services.

---

# Cache Key Standards

# Object Cache Keys

Format:

```text
{entity}:object:{id}
```

Examples:

```text
course:object:{id}
invoice:object:{id}
studentprofile:object:{id}
```

---

# Reference Cache Keys

Format:

```text
user:{userId}:{dataset}
scope:{scopeId}:{dataset}
```

Examples:

```text
user:{id}:visible_courses
scope:{id}:academicplans
scope:{id}:students
```

---

# Localization-Aware Cache

Localized payloads MUST include culture in keys.

Example:

```text
course:object:{id}:culture:en
course:object:{id}:culture:ar
```

Never mix localized payloads.

---

# Authorization-Aware Cache

Authorization-sensitive datasets MUST NEVER use unrestricted global caches.

Incorrect:

```text
all_students
```

Correct:

```text
scope:{scopeId}:students
user:{userId}:students
```

Cache visibility must respect authorization boundaries.

---

# Scope Filtering Integration

The system integrates with:

```text
RequestPipeline_Context_Authorization_and_Localization.md
```

Scope information extracted from headers MUST participate in:

- query filtering
- cache key generation
- authorization visibility
- dataset isolation

Example:

```text
scope:{scopeId}:academicplans
```

Never bypass scope filtering.

---

# Database Fallback Rules

If:

```text
shared object cache miss
```

occurs:

1. query database
2. materialize read model
3. serialize detached object
4. populate shared cache
5. return payload

---

# Batch Resolution Rules

When resolving references:

Prefer:

- batch Redis retrieval
- pipelining
- bulk operations

Avoid:

```text
N redis calls for N ids
```

Example:

Incorrect:

```text
100 cache calls for 100 ids
```

Correct:

```text
single batched retrieval
```

---

# Cache Invalidation Rules

# Object Updates

When an object changes:

1. invalidate shared object cache
2. keep user reference caches intact
3. lazy rehydrate on next request

Example:

```text
course:object:{id}
```

Only the shared payload needs invalidation.

---

# Reference Updates

When visibility changes:

invalidate only affected reference datasets.

Example:

```text
user:{id}:visible_courses
```

---

# Event-Based Invalidation

Preferred invalidation approach:

```text
CourseUpdatedEvent
AcademicPlanUpdatedEvent
InvoicePaidEvent
StudentProfileUpdatedEvent
```

Event handlers invalidate related cache entries.

---

# Serialization Rules

Preferred serializer:

```text
System.Text.Json
```

Requirements:

- lightweight DTOs
- minimal reflection
- optimized serialization
- source generators when possible

---

# Compression Rules

Compression MAY be used for:

- very large payloads
- infrequently modified datasets

Do NOT compress small payloads.

---

# Concurrency & Stampede Protection

The cache layer should support:

- distributed locks
- single-flight population
- stale-while-revalidate
- cache stampede prevention

to avoid high-concurrency cache misses overwhelming the database.

---

# Query Strategy

Preferred strategy:

```text
Cache-Aside Pattern
```

Flow:

```text
Request
  ->
Cache lookup
  ->
Database fallback
  ->
Populate cache
```

---

# Performance Expectations

The architecture should support:

- 10k+ concurrent users
- low Redis memory growth
- low DB contention
- low serialization overhead
- horizontal scalability
- predictable invalidation behavior

---

# Seeder & Testing Requirements

Seeder data MUST support:

- authorization testing
- scope filtering testing
- localization testing
- cache invalidation testing
- high-concurrency testing
- object/reference cache testing

Example seeded datasets:

- courses
- academic plans
- students
- invoices
- permissions
- localized records

---

# Forbidden Patterns

Do NOT:

- duplicate full payloads per user
- cache tracked EF entities
- cache unrestricted global datasets
- bypass authorization filtering
- bypass scope filtering
- store huge circular object graphs
- tightly couple cache payloads to EF tracking state

---

# Recommended Future Extensions

Potential future improvements:

- tag-based invalidation
- CQRS read-model caches
- Redis pub/sub invalidation
- versioned payloads
- partial object hydration
- distributed cache synchronization
- read-through cache abstraction

---

# Final Summary

The platform uses:

- shared full-object caching
- lightweight user/scope references
- authorization-aware datasets
- localization-aware keys
- scope-aware isolation
- detached serialized read models

to maximize scalability while minimizing Redis memory duplication and invalidation complexity.