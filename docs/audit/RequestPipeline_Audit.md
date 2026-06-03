# Request Pipeline (Context / Authorization / Localization) — Verification Audit

**Doc:** `docs/RequestPipeline_Context_Authorization_and_Localization.md`
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Separate culture (presentation), requested scope (untrusted headers), user scope (trusted DB), and effective scope (enforced).
- **Assumptions:** Culture middleware reads `Accept-Language`, sets current culture for translations + date/number formatting. RequestContext middleware reads headers `X-Faculty-Id`, `X-Program-Id`, `X-Year-Id`, `X-Semester-Id`. Authentication → `ICurrentUser`. `IUserScope` loaded from DB (trusted). `EffectiveScope = Intersection(Requested, Allowed) + validation`.
- **Invariants / Critical Rules:** Never use `IRequestContext` directly in queries; never trust headers for authz; always use `IEffectiveScope` in business logic; keep localization separate.
- **Execution order:** Culture MW → RequestContext MW → Authenticate → Build `IUserScope` → Build `IEffectiveScope` → App.

## Step 2 — Implementation Files

- `API/Program.cs` (pipeline registration)
- `API/Infrastructure/{CorrelationIdMiddleware, RequestLoggingMiddleware, SessionVersionMiddleware, UserScopePreloadMiddleware}.cs`
- `Core.Application/CrossCutting/Auth/Authentication/RequestContext.cs` (`IRequestContext`)
- `Core.Application/CrossCutting/Auth/Authentication/CurrentUser.cs` (`ICurrentUser`)
- `Core.Infrastructure/Services/Authorization/UserScope.cs` (`IUserScope`)
- `Core.Infrastructure/Services/Authorization/EffectiveScope.cs` (`IEffectiveScope`)
- `Core.Application/CrossCutting/Localization/{CurrentCultureServcie, LocalizationService, LocalizationFormatter, SystemCultureScope}.cs`

## Step 3 — Verification Table

| Assumption | Expected | Actual Implementation | Match |
| --- | --- | --- | --- |
| Culture reads `Accept-Language` | header → language | `CurrentCultureService.Language` parses `Accept-Language` q-list, ar/en, default ar (`CurrentCultureServcie.cs:28-99`) | ✅ |
| Culture is a **middleware** that **sets current culture** | pipeline step sets thread culture | No culture middleware; no `UseRequestLocalization` in `Program.cs`; culture is a lazy scoped **service** (`Program.cs` has no culture/localization line; `CurrentCultureServcie.cs`) | ❌ (Divergence 1) |
| Culture used for date/number formatting | thread `CurrentCulture` set per request | Nothing sets request thread culture; only `SystemCultureScope` sets ambient for background (`CurrentCultureServcie.cs:38-40`) | ❌ (Divergence 1) |
| RequestContext is a **middleware** | pipeline step | `IRequestContext` is a scoped service reading `IHttpContextAccessor` lazily; no middleware (`RequestContext.cs:7-40`) | ❌ (Divergence 2) |
| Headers `X-Faculty-Id`, `X-Program-Id`, `X-Year-Id`, `X-Semester-Id` | those four names | Reads `X-StructureNode-Id`, `X-AcademicYear-Id`, `X-Semester-Id` (`RequestContext.cs:29-33`) | ❌ (Divergence 3) |
| Requested scope is untrusted | not used for authz | Header scope feeds only scope resolution / cache key, never grants (`PermissionManagementService.cs:299-302`) | ✅ |
| Authentication → `ICurrentUser` | auth provides identity | `UseAuthentication()` (`Program.cs:170`) + `CurrentUser` from claims | ✅ |
| `IUserScope` loaded from DB (trusted) | DB-backed allowed scope | `UserScope.LoadAsync` loads Staff/Student + StaffRole path grants (`UserScope.cs:70-162`) | ✅ |
| `IUserScope` built after authenticate | order | `UserScopePreloadMiddleware` runs after `UseAuthentication` (`Program.cs:170,174`) | ✅ |
| `EffectiveScope = Intersection(Requested, Allowed) + validation` produced as a scope object | single validated scope built then used | No "builder" producing an intersected scope object. `IEffectiveScope` is a per-resource access checker: `CanAccessStudent/StructureNode/AcademicYear/Semester` (`EffectiveScope.cs:39-141`) | ⚠️ (Divergence 4) |
| Never use `IRequestContext` directly in queries | only enforcement uses it | Only `PermissionManagementService` injects `IRequestContext` (for scope/cache key); no business `*Service` query uses it (grep) | ✅ |
| Always use `IEffectiveScope` in business logic | row-level guard | Enforced via `PermissionHandler` ABAC + service calls to `IEffectiveScope` | ✅ |
| Localization kept separate from data filtering | no coupling | Culture service independent of scope path; separate namespaces | ✅ |

## Step 4 — Divergence Analysis

### Divergence 1 — No culture middleware; current culture not set per request
- **Model:** A "Culture Middleware" resolves `Accept-Language` and **sets the current culture**, used for translations and date/number formatting.
- **Implementation:** Culture is a lazy scoped service `CurrentCultureService.Language` read on demand by the localization layer. `Program.cs` registers **no** `UseRequestLocalization` and no culture middleware. Thread `CultureInfo.CurrentCulture/CurrentUICulture` is set only by `SystemCultureScope` for background/outbox work, not for HTTP requests (`CurrentCultureServcie.cs:38-40`).
- **Evidence:** `Program.cs` middleware list (`:129-180`) contains no culture/localization entry; `CurrentCultureServcie.cs`.
- **Impact:** Functional/Low–Medium. Translations work (the localization service queries the language directly). But any `.ToString()`/`string.Format` relying on ambient `CurrentCulture` for date/number formatting would use the server default, not the request language, because nothing sets it. The documented "formatting (dates, numbers)" guarantee is not wired through a middleware.
- **Severity:** Medium
- **Notes:** Behavior is correct for translation; the doc’s middleware/thread-culture framing is inaccurate.

### Divergence 2 — RequestContext is a service, not middleware
- **Model:** "RequestContext Middleware" is a pipeline stage.
- **Implementation:** `IRequestContext`/`RequestContext` is a scoped service that reads headers lazily via `IHttpContextAccessor` (`RequestContext.cs`). No middleware materializes it.
- **Evidence:** `RequestContext.cs:7-40`; absent from `Program.cs` middleware list.
- **Impact:** Documentation/Low. Functionally equivalent (values available on demand). The diagram’s middleware boxes for Culture and RequestContext don’t correspond to real pipeline stages.
- **Severity:** Low

### Divergence 3 — Scope header names differ from the documented contract
- **Model:** `X-Faculty-Id`, `X-Program-Id`, `X-Year-Id`, `X-Semester-Id`.
- **Implementation:** `X-StructureNode-Id` (one node id replaces separate Faculty/Program), `X-AcademicYear-Id`, `X-Semester-Id` (`RequestContext.cs:29-33`).
- **Evidence:** `RequestContext.cs:29-33`. (`HTTP_Requests_Examples.md` should be cross-checked against these.)
- **Impact:** Functional/Medium. An API client following the doc would send headers the server ignores, silently falling back to defaults — requested scope would never take effect. Three of four documented header names are wrong.
- **Severity:** Medium
- **Notes:** Highest-impact mismatch in this doc — it’s the public request contract.

### Divergence 4 — No EffectiveScope "intersection builder"; it is a per-resource checker
- **Model:** `EffectiveScope = Intersection(RequestedScope, UserScope)` produced as a validated scope object used "everywhere."
- **Implementation:** `IEffectiveScope` exposes `CanAccess{Student,StructureNode,AcademicYear,Semester}` booleans that compare a target resource’s path/id against `IUserScope` grants and header locks at decision time (`EffectiveScope.cs:39-141`). The structural "intersection" is not precomputed; temporal "intersection" is a header-lock equality check (`EffectiveScope.cs:124-141`).
- **Evidence:** `EffectiveScope.cs`.
- **Impact:** Operational/Low. The enforcement goal (trusted-scope wins, headers can only narrow) is met, but the architecture is a lazy per-call guard, not the single intersected scope object the model depicts.
- **Severity:** Low

## Step 5 — Hidden-Logic Review

- **`UserScope` sync-over-async:** `EnsureLoaded()` does `LoadAsync().GetAwaiter().GetResult()` (`UserScope.cs:67`); `UserScopePreloadMiddleware` exists specifically to pre-warm it after auth and avoid thread-pool starvation (`UserScopePreloadMiddleware.cs`). Mitigation present; residual risk only if synchronous access occurs before the preload runs (e.g., another middleware between auth and preload reading `IUserScope`). No such reader found. No verified issue.
- **`UserScope` gate:** `SemaphoreSlim(1,1)` + double-checked `_loaded` (`UserScope.cs:17,70-95`) — safe for the scoped (single-request) lifetime. No issue.
- **Header trust:** Requested scope only ever *narrows* via `IEffectiveScope`/scope filter; never grants. Critical rule upheld. No issue.
- **Background culture:** `CurrentCultureService` falls back to ambient culture when no `HttpContext` (`:38-40`) — correct for outbox/system paths.
- **No verified security issue found.**

## Step 6 — Flow Verification

### Request execution order
- **Expected:** Culture MW → RequestContext MW → Authenticate → IUserScope → IEffectiveScope → App.
- **Actual:** `CorrelationId → RequestLogging → UseAuthentication → SessionVersion → UserScopePreload → UseAuthorization → MapControllers` (`Program.cs:129-180`). Culture + RequestContext are lazy services, not stages; `IEffectiveScope` evaluated inside authorization/handlers.
- **Match:** NO (partial).
- **Differences:** No culture/RequestContext middleware stages; `IUserScope` preloaded post-auth (matches intent); `IEffectiveScope` is per-decision, not a build step.

### Scope enforcement (requested ∩ allowed)
- **Expected:** intersection + validation, trusted wins.
- **Actual:** Header scope feeds scope resolution/cache; row access via `IEffectiveScope.CanAccess*`; trusted `IUserScope` grants gate access; header year/semester can only narrow.
- **Match:** YES on the security outcome; NO on the "single intersected scope object" shape.

### Localization
- **Expected:** Accept-Language → culture, separate from data.
- **Actual:** `CurrentCultureService` resolves language on demand; independent of scope.
- **Match:** YES for translation; NO for thread-culture/formatting wiring (Divergence 1).

## Step 7 — Final Verdict

### Scores
- Architecture Match: 6/10
- Logic Correctness: 8/10
- Operational Safety: 8/10
- Maintainability: 7/10

### Confirmed Issues
1. Documented scope header names (`X-Faculty-Id`/`X-Program-Id`/`X-Year-Id`) do not match implementation (`X-StructureNode-Id`/`X-AcademicYear-Id`/`X-Semester-Id`) — Medium, public contract.
2. No culture middleware / `UseRequestLocalization`; request thread culture not set — Medium.
3. "Culture Middleware" and "RequestContext Middleware" are services, not pipeline stages — Low.
4. No EffectiveScope intersection-builder; `IEffectiveScope` is a per-resource checker — Low.

### Model Violations
- Header contract, culture-middleware claim, middleware framing, and the intersected-scope-object abstraction all differ from code.

### False Positives
- "Never use `IRequestContext` in queries" — upheld in code.
- "Headers untrusted for authz" — upheld.
- "Always use `IEffectiveScope`" — upheld (just a different shape).

### Findings Summary
- **Matches:** Untrusted-headers principle, trusted DB-backed `IUserScope`, post-auth scope preload, localization separation, Accept-Language resolution semantics.
- **Does not match:** Header names, culture-as-middleware + thread-culture formatting, RequestContext-as-middleware, EffectiveScope-as-intersection-object.
- **Needs attention:** Update the doc’s header table to the real names (highest priority — it misleads API clients), and correct the middleware framing (culture/RequestContext are lazy services; `IEffectiveScope` is a per-call guard). The security model itself is sound.
