# Distributed Caching Strategy — Documentation & Logic Verification Audit

**Model document:** `docs/caching-strategy.md`
**Implementation:** `ICacheService` + `RedisCacheService` / `MemoryCacheService`
and per-service object-cache usages (Course, AcademicPlan, Invoice, ScheduleSlot,
StudentService(+Request), StudentProfile).
**Branch:** `sync_platform_hardening`
**Audited:** 2026-06-02

---

## 1. Model Extract

**Purpose.** A hybrid distributed-cache architecture sized for 10k+ concurrent
users that (a) stores each full object payload **once** (shared object cache),
and (b) stores small per-user/scope **reference lists** of ids, resolved against
the shared objects — avoiding Redis memory duplication while preserving
authorization/scope isolation (lines 5–32).

**Assumptions / design.**
- **Layer 1 — Shared Object Cache.** Full detached serialized payload per entity,
  key `{entity}:object:{id}`; shared across users; long-lived when semi-static
  (lines 38–63, 179–199, 249–263).
- **Layer 2 — User/Scope Reference Cache.** Lightweight id lists, key
  `user:{id}:{dataset}` / `scope:{scopeId}:{dataset}`; authorization-/scope-aware;
  cheap invalidation (lines 67–87, 203–223, 267–282).
- **Layer 3 — Request Scope Cache.** Short-lived in-memory per-request cache
  (`HttpContext.Items`) to avoid duplicate Redis hits / repeated auth /
  localization resolution within one request (lines 226–243).
- **Retrieval flow.** reference lookup → object-cache lookup → DB fallback →
  populate object cache (lines 91–121, 347–362).

**Invariants / rules.**
- **Localization-aware keys (MUST).** "Localized payloads MUST include culture in
  keys" e.g. `course:object:{id}:culture:en`; "Never mix localized payloads"
  (lines 286–297).
- **Authorization-aware (MUST NEVER).** Auth-sensitive datasets must never use
  unrestricted global caches; use `scope:{scopeId}:…` / `user:{userId}:…`
  (lines 301–318, 322–343).
- **Forbidden content.** No EF tracked entities, DbContext-bound objects,
  proxies, circular graphs, UoW state — only detached serialized models
  (lines 162–173, 544–554).
- **Batch resolution.** Prefer pipelined/bulk Redis retrieval; avoid N calls for
  N ids (lines 365–393).
- **Invalidation.** Object change → invalidate object key, keep reference caches,
  lazy rehydrate; preferred mechanism is event-based (lines 397–442).
- **Serialization.** `System.Text.Json`, lightweight DTOs, source generators when
  possible (lines 446–459).
- **Concurrency/stampede.** Layer "should support" distributed locks,
  single-flight population, stale-while-revalidate, stampede prevention
  (lines 474–483).
- **Query strategy.** Cache-Aside (lines 487–505).

**Future extensions (explicitly out of scope).** Tag-based invalidation, CQRS
read-model caches, Redis pub/sub invalidation, versioned payloads, partial
hydration, read-through abstraction (lines 558–568).

---

## 2. Implementation Files

| File | Role |
|------|------|
| `Abstractions/.../Caching/ICacheService.cs` | Cache contract: `GetAsync`/`SetAsync`/`RemoveAsync` |
| `Application/.../Caching/RedisCacheService.cs` | `IDistributedCache` (Redis) impl, graceful degradation |
| `Application/.../Caching/MemoryCacheService.cs` | `IMemoryCache` impl (Redis disabled) |
| `Core.Infrastructure/DependencyInjection.cs:52–64` | Conditional Redis vs in-memory registration |
| `Courses/CourseService.cs` | `course:object:` cache-aside usage (reference impl) |
| `Courses/AcademicPlanService.cs` | `academicplan:object:` |
| `Module.Payments/.../InvoiceService.cs` | `invoice:object:` |
| `Module.Schedule/.../ScheduleSlotService.cs` | `schedule-slot:object:` |
| `Module.StudentServices/.../StudentServiceService.cs` (+Request) | `student-service[-request]:object:` |
| `Module.Student/.../StudentProfileService.cs` | `studentprofile:object:` |

> Note: a separate **permission/session-version cache** subsystem exists
> (`PermissionCacheOptions`, `CachedSessionVersionService`,
> `PermissionCacheInvalidator`). It is an authorization concern, not the
> object/reference strategy this document describes, and is audited under the
> Authorization model — excluded here except where noted.

---

## 3. Per-Assumption Verification

| # | Assumption (Model) | Expected Behavior | Actual Implementation | Match |
|---|--------------------|-------------------|-----------------------|-------|
| 1 | Layer 1 shared object cache, key `{entity}:object:{id}` | One detached payload per object, keyed by entity+id | Implemented uniformly: `course:object:`, `academicplan:object:`, `invoice:object:`, `schedule-slot:object:`, `student-service:object:`, `studentprofile:object:` — all `{prefix}{id:N}` (CourseService.cs:27,209; InvoiceService.cs:26,329; ScheduleSlotService.cs:67,102; etc.) | ✅ |
| 2 | Cache-Aside pattern | Lookup → DB fallback → populate | `GetByIdAsync`: cache get → on miss load + `SetAsync` (CourseService.cs:54–60; ScheduleSlotService.cs:104–128) | ✅ |
| 3 | Detached serialized models only; no EF tracked entities | Cache stores DTOs | Caches `*Response` DTOs via `MapToResponse`/mapper; `System.Text.Json` round-trip (RedisCacheService.cs:41,60; ScheduleSlotService.cs:453–465) | ✅ |
| 4 | `System.Text.Json` serializer | STJ used | `RedisCacheService` uses `JsonSerializer` with default options (RedisCacheService.cs:1,20,41,60) | ✅ |
| 5 | Object-update invalidation: invalidate object key only | Mutation evicts the object key | Update/Delete/Close/Open call `RemoveAsync(CacheKey(id))` (ScheduleSlotService.cs:271,280,434,444; CourseService mutations) | ✅ |
| 6 | Auth-sensitive data never globally cached | No `all_students`-style global cache | No unrestricted global dataset cache exists; scope-sensitive reads re-check scope live on every object-cache hit (ScheduleSlotService.cs:115,124) | ✅ |
| 7 | Localized payloads MUST include culture in keys; never mix | Culture suffix in key | **Contradicted** — single culture-neutral key; both cultures' JSON cached, decoded on read (CourseService.cs:57–60; ScheduleSlotService.cs:106–116, 470–475) — see Divergence 1 | ❌ |
| 8 | Layer 2 user/scope reference cache (`user:{id}:…`, `scope:{id}:…`) | Per-user/scope id lists cached | **Absent** — no reference-list cache key exists anywhere in `src/` — see Divergence 2 | ❌ |
| 9 | Layer 3 request-scope cache | Per-request in-memory de-dupe | **Absent** — no `HttpContext.Items` cache layer — see Divergence 3 | ❌ |
| 10 | Batch/pipelined reference resolution | Bulk Redis retrieval | **Absent** — `ICacheService` exposes only single-key `GetAsync`; no batch API (ICacheService.cs:9–11) — see Divergence 4 | ❌ |
| 11 | Stampede protection: locks / single-flight / stale-while-revalidate | Concurrency guards on miss | **Absent** — no locks, single-flight, or SWR in either cache impl — see Divergence 5 | ❌ |
| 12 | Event-based invalidation preferred | Domain events invalidate cache | Inline `RemoveAsync` on mutation instead of event handlers — see Divergence 6 | ⚠️ |
| 13 | Graceful behavior under cache failure | (not specified) | Redis errors caught → treated as miss / no-op so an outage never 500s (RedisCacheService.cs:31–48,50–68,70–80) | ✅ (exceeds doc) |

---

## 4. Divergence Blocks

### Divergence 1 — Localization key strategy: doc mandates culture-suffixed keys; implementation uses a single culture-neutral key

- **Model.** "Localized payloads MUST include culture in keys … `course:object:{id}:culture:en` … Never mix localized payloads." (lines 286–297).
- **Implementation.** The cache key carries **no** culture segment (`{entity}:object:{id:N}`). The cached payload keeps the *bilingual* `{"ar":…,"en":…}` JSON intact; the current culture is decoded **on read** via `ILocalizationService`, so one entry serves every `Accept-Language`.
- **Evidence.** CourseService.cs:57–60 ("two requests with different Accept-Language hit the same cache entry without poisoning each other"); ScheduleSlotService.cs:106–116, 470–475; InvoiceService / StudentProfileService follow the same pattern.
- **Impact.** Literal violation of a documented **MUST**. However, the document's stated *intent* — "never mix localized payloads" / no cross-culture poisoning — is fully satisfied by a different and arguably superior mechanism: the cached value is culture-*neutral* (carries both languages), not a single-culture render, so no mixing can occur, and Redis stores **one** entry instead of one-per-culture (less memory — aligned with the doc's headline goal). The risk the MUST guards against does not arise.
- **Severity.** Medium (documented-MUST contradiction) — but **no correctness defect**; behavior is safe and memory-favorable.
- **Notes.** Recommend updating the model to describe the culture-neutral-payload + localize-on-read pattern as the approved approach, superseding the culture-suffixed-key rule.

### Divergence 2 — Layer 2 (user/scope reference cache) not implemented

- **Model.** The central "hybrid" premise: per-user/scope **id-reference lists** (`user:{id}:visible_courses`, `scope:{scopeId}:students`) resolved against the shared object cache, so payloads are never duplicated per user (lines 67–121, 203–223).
- **Implementation.** No reference-list cache exists. A repository-wide search for `user:{…}:`, `scope:{…}:`, `visible_courses`, etc. finds only **shared object** keys (`*:object:*`) and unrelated sync lock names. Per-user/scope visibility is enforced at request time by live service scope checks and EF query filters, **not** by cached id lists.
- **Evidence.** No matching keys in `src/`; visibility enforced live, e.g. `ScheduleSlotService` re-checks `_offerings.GetByIdAsync(...)` on every read (ScheduleSlotService.cs:115,124,133); catalog services note "visibility lists belong to higher layers" (CourseService.cs:16–19).
- **Impact.** The document's headline scalability mechanism (Layer 2) is unbuilt. The *memory-duplication* problem it solves does **not** currently bite — because per-user lists aren't cached at all, there is nothing to duplicate; objects are cached once (Layer 1) and visibility is computed per request. The cost is on the **DB/compute side at scale**: every list/visibility resolution hits the database/service rather than a cached reference set, so the "10k concurrent users / low DB contention" target is only partially served. No functional defect — a scaling-architecture gap.
- **Severity.** Medium.
- **Notes.** This is unimplemented aspiration, not a bug. The document presents Layer 2 under "Main Design," not "Future Extensions," so it is a genuine model-vs-implementation gap worth recording.

### Divergence 3 — Layer 3 (request-scope cache) not implemented

- **Model.** A short-lived per-request in-memory cache (`HttpContext.Items` or scoped service) to avoid duplicate Redis hits / repeated auth / localization resolution within one request (lines 226–243).
- **Implementation.** No such layer exists; each service call goes straight to `ICacheService` (Redis/in-memory) with no per-request memoization.
- **Evidence.** No `HttpContext.Items`-based cache in the caching path; `ICacheService` is the only abstraction.
- **Impact.** Within a single request that reads the same object twice, two Redis round-trips occur instead of one. Minor latency cost; no correctness impact. Low at current scale.
- **Severity.** Low.

### Divergence 4 — No batch / pipelined reference resolution

- **Model.** Prefer batched Redis retrieval / pipelining; avoid "N redis calls for N ids" (lines 365–393).
- **Implementation.** `ICacheService` exposes only single-key `GetAsync` (ICacheService.cs:9–11); there is no multi-get.
- **Evidence.** ICacheService.cs:9–11.
- **Impact.** Because Layer 2 reference-list resolution (the scenario that would fan out into N gets) is itself unimplemented (Divergence 2), the N+1 Redis pattern is not currently triggered in the object-cache path. The missing batch API only becomes relevant once Layer 2 lands. Low today.
- **Severity.** Low.

### Divergence 5 — No cache-stampede / single-flight / stale-while-revalidate protection

- **Model.** The cache layer "should support" distributed locks, single-flight population, stale-while-revalidate, and stampede prevention so high-concurrency misses don't overwhelm the DB (lines 474–483).
- **Implementation.** Neither `RedisCacheService` nor `MemoryCacheService` provides any of these. On a hot-key miss/expiry, every concurrent request independently runs the DB fallback and populates the cache.
- **Evidence.** RedisCacheService.cs (plain get/set/remove); MemoryCacheService.cs (plain `TryGetValue`/`Set`/`Remove`); no `SemaphoreSlim`/lock/SWR found in any `*Cache*.cs`.
- **Impact.** Against the document's own 10k-user goal, a popular object expiring under load can produce a thundering-herd DB spike (classic stampede). Real operational risk *relative to the stated target*, though no defect at moderate load. The 30-min/15-min absolute TTLs make simultaneous mass-expiry less likely than short TTLs would.
- **Severity.** Medium.
- **Notes.** Mitigated partly by graceful degradation (a slow DB won't corrupt state) but not by any stampede control.

### Divergence 6 — Invalidation is inline cache-aside, not event-based

- **Model.** "Preferred invalidation approach" is event-based (`CourseUpdatedEvent`, `InvoicePaidEvent`, …) with handlers invalidating related entries (lines 431–442).
- **Implementation.** Each service evicts its own object key inline within the mutation method (`_cache.RemoveAsync(CacheKey(id))` right after `SaveChanges`).
- **Evidence.** ScheduleSlotService.cs:271,280,434,444; analogous in Course/Invoice/StudentProfile services.
- **Impact.** The doc labels event-based as *preferred*, not required; inline cache-aside invalidation is a valid, simpler alternative and is correctly placed after commit. Because only single-object keys are cached (no reference lists / cross-entity fan-out), there is no multi-entry invalidation that would benefit from an event bus today. No defect.
- **Severity.** Low.

---

## 5. Hidden-Logic Review

- **Redis outage resilience.** Every `RedisCacheService` operation wraps Redis I/O in try/catch and degrades to a miss (reads) or no-op (writes), logging a warning — a Redis outage degrades to "no cache," never a 500 (RedisCacheService.cs:31–98). **Verified safe** and exceeds the document.
- **Cross-culture poisoning.** The culture-neutral-payload + localize-on-read design means a warm entry cannot serve the wrong language: the stored value holds both languages and is decoded per request (CourseService.cs:57–60). **Verified safe** despite the key-format MUST violation (Divergence 1).
- **Stale data after revoked scope.** Object-cache hits for scope-sensitive entities still run a live parent/scope check before returning (ScheduleSlotService.cs:115,124), so a cached object cannot leak to a caller who lost access. **Verified safe.**
- **EF-tracking leakage into cache.** Services cache projected `*Response` DTOs, not tracked entities; STJ serialization would in any case detach them. The doc's "no tracked entities" forbidden rule holds. **No verified issue.**
- **Provider divergence (Redis vs in-memory).** `MemoryCacheService` stores the live object reference (no serialization), while `RedisCacheService` round-trips through JSON (MemoryCacheService.cs:32 vs RedisCacheService.cs:60). A cached mutable DTO mutated after caching would differ between providers in-memory. In practice services cache freshly-mapped DTOs and never mutate them post-cache, and production uses Redis. **No verified defect**, but a latent provider-semantics difference worth noting.

No other verified issue found.

---

## 6. Flow Verification

**Flow: Object read (cache-aside)**
- *Expected (doc).* reference lookup → object-cache lookup → DB fallback →
  populate object cache → return (lines 91–105, 347–362).
- *Actual.* object-cache lookup → (miss) DB load → `SetAsync` → return; **no**
  reference-lookup stage (Layer 2 absent) (CourseService.cs:54–; ScheduleSlotService.cs:104–128).
- *Match.* PARTIAL — object-cache + DB-fallback + populate match exactly; the
  upstream reference-lookup stage is skipped because Layer 2 is unimplemented.

**Flow: Object update invalidation**
- *Expected.* invalidate object key, keep reference caches, lazy rehydrate
  (lines 397–413).
- *Actual.* `RemoveAsync(object key)` after commit; next read repopulates; no
  reference caches to keep (none exist) (ScheduleSlotService.cs:266–272).
- *Match.* YES (for the layers that exist).

**Flow: Cache failure**
- *Expected.* (not specified by doc).
- *Actual.* treat as miss/no-op, log warning, continue (RedisCacheService.cs:43–47,63–67).
- *Match.* N/A — beyond doc; a correctness benefit.

---

## 7. Final Verdict

### Scores
- **Architecture fidelity:** 6/10 — Layer 1 (shared object cache) is implemented
  cleanly and uniformly and matches the key standard exactly, but Layers 2 and 3,
  batch resolution, and stampede protection — all presented under "Main
  Design"/"Cache Layers," not "Future Extensions" — are unbuilt, and the
  localization key rule is contradicted. Roughly one-third of the documented
  architecture is realized.
- **Logic correctness:** 9/10 — what exists is correct: cache-aside is right,
  no cross-culture poisoning, detached DTOs only, post-commit invalidation,
  scope re-checked on hits.
- **Operational safety:** 7/10 — excellent Redis-outage degradation, but **no
  stampede protection** against the document's own 10k-user goal is a real gap;
  absolute TTLs soften it.
- **Maintainability:** 9/10 — one tiny `ICacheService` contract, a single
  consistent key convention across seven services, and code comments that
  explicitly cite `docs/caching-strategy.md` and explain the culture-neutral
  decision.

### Confirmed Issues
1. **No cache-stampede / single-flight protection** (Divergence 5) — hot-key
   expiry under high concurrency can stampede the DB; contradicts the doc's
   "should support … stampede prevention" against its 10k-user target.
2. **Localization key MUST contradicted** (Divergence 1) — keys carry no culture
   segment; behavior is *safe* (culture-neutral payload, localize-on-read) but
   violates the documented rule verbatim.

### Model Violations (unimplemented design — gaps, not bugs)
- Layer 2 user/scope reference cache absent (Divergence 2).
- Layer 3 request-scope cache absent (Divergence 3).
- Batch/pipelined resolution absent (Divergence 4).
- Event-based invalidation replaced by inline cache-aside (Divergence 6 — doc
  allows this as non-preferred).

### False Positives Cleared
- **"Caching auth-sensitive data globally"** — not occurring; no global dataset
  cache exists, and scope-sensitive object reads re-verify scope live on every
  hit. The forbidden `all_students`-style cache is absent.
- **"Culture-neutral key = cross-culture poisoning"** — false; the cached payload
  is bilingual and decoded per request, so no poisoning is possible.
- **"Caching EF tracked entities"** — false; services cache detached `*Response`
  DTOs.

### Findings Summary
The implementation realizes the document's **Layer 1 shared object cache**
faithfully and consistently — exact `{entity}:object:{id}` key format, cache-aside
reads, post-commit single-key invalidation, detached `System.Text.Json` DTOs, and
robust Redis-outage degradation that the document does not even require. Every
proven *safety* property holds.

What is **not** built is most of the document's scaling architecture: the Layer 2
user/scope **reference cache** (the document's headline anti-duplication
mechanism), the Layer 3 request cache, batch resolution, and stampede protection.
None of these cause a current defect — the memory-duplication problem Layer 2
solves does not arise because per-user lists are never cached, and visibility is
instead resolved live with correct scope enforcement. They are genuine
**model-vs-implementation gaps** that leave the stated "10k concurrent users / low
DB contention" goal only partially served, with cache-stampede exposure being the
most material operational risk. The one outright **contradiction** — the
culture-in-key MUST — resolves to a safe, memory-favorable alternative.

Recommended (non-code) follow-ups: (1) update `caching-strategy.md` to mark
Layers 2/3, batch resolution, and stampede protection as not-yet-implemented (or
move them to "Future Extensions"); (2) replace the culture-suffixed-key MUST with
the approved culture-neutral-payload + localize-on-read pattern; (3) record
stampede protection as a tracked hardening item before the 10k-user load target
is relied upon.
