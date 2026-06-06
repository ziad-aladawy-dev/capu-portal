# Authentication Model — Verification Audit

**Doc:** `docs/Authentication_Model.md`
**Scope:** Login / token / bootstrap authentication flow.
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Centralized orchestration: validate credentials, resolve identity source, generate JWT, build frontend authorization bootstrap.
- **Assumptions:** Two credential sources (Students, Admins) behind one contract; backend is authorization source of truth; JWT is lightweight.
- **Invariants:** JWT excludes permission collections / scope trees / UI data. Authentication ≠ Authorization.
- **Expected flow:** `POST /auth/login → AuthController → AuthenticationService → {IUserCredentialResolver, IPasswordHasher, IAuthorizationResponseBuilder, ITokenService} → LoginResponseDto`.
- **Responsibilities:** `AuthenticationService` orchestrates only; `IUserCredentialResolver` resolves source; `IPasswordHasher` hash/verify; `IAuthorizationResponseBuilder` builds permissions + authorized scopes + active scope + login DTO; `ITokenService` generates/signs/expires JWT.
- **JWT included claims:** UserId, Role, SessionVersion, optional minimal active context. **Excluded:** permission collections, authorization graphs, UI data, scope trees.
- **Bootstrap model:** Authorized Scopes (faculties/programs/semesters/academic years), Permissions (`Module.Resource.Action`), Active Scope (Structural: faculty/program; Temporal: academic year/semester).
- **Constraints:** No CQRS, MediatR, event buses, distributed orchestration, runtime plugin discovery.

## Step 2 — Implementation Files

- `src/1.API/CapitalUniversity.API/Controllers/AuthController.cs`
- `src/2.Core/CapitalUniversity.Core.Application/CrossCutting/Auth/Authentication/AuthenticationService.cs`
- `…/Authentication/TokenService.cs`, `…/Authentication/PasswordHasher.cs`
- `src/2.Core/CapitalUniversity.Core.Infrastructure/Services/Authentication/UserCredentialResolver.cs`
- `…/Services/Authentication/RefreshTokenService.cs`
- `…/Services/Authorization/PermissionManagementService.cs` (`GetBootstrapContextAsync`)
- `src/2.Core/CapitalUniversity.Core.Abstractions/CrossCutting/Auth/Authentication/DTOs/LoginResponseDto.cs`
- Abstractions: `IUserCredentialResolver`, `ITokenService`, `IPasswordHasher`, `IAuthenticationService`

## Step 3 — Verification Table

| Assumption | Expected Behavior | Actual Implementation | Match |
| --- | --- | --- | --- |
| Centralized orchestration service | `AuthenticationService` coordinates flow | `AuthenticationService.AuthenticateAsync` orchestrates (`AuthenticationService.cs:36`) | ✅ |
| Two credential sources behind one contract | Student + Admin sources | `UserCredentialResolver` resolves Student then Staff (`UserCredentialResolver.cs:19-37`); "Admin" = Staff role | ✅ (naming: "Admin"→"Staff") |
| `IUserCredentialResolver` resolves source/credential | Locate user, identify source | Present (`UserCredentialResolver.cs:17`) | ✅ |
| `IPasswordHasher` hash/verify | PBKDF2 or equivalent, constant-time verify | PBKDF2-SHA256, 100k iters, `FixedTimeEquals` (`PasswordHasher.cs:14-37`) | ✅ |
| `IAuthorizationResponseBuilder` builds bootstrap | Dedicated builder component | **No such production type.** Bootstrap built by `IPermissionManagementService.GetBootstrapContextAsync` (`AuthenticationService.cs:68`, `PermissionManagementService.cs:63`). Interface exists only in `tests/` | ❌ |
| `ITokenService` JWT generate/sign/expire | HMAC-signed JWT with expiry | `TokenService.GenerateToken` HS256 + `ExpiryMinutes` (`TokenService.cs:19-52`) | ✅ |
| JWT includes UserId, Role, SessionVersion | Those three + optional minimal context | Present + `StructureNodeId` (`TokenService.cs:21-39`) | ✅ |
| JWT excludes permission collections / scope trees / UI | No perms/graphs in token | None present (`TokenService.cs:21-39`) | ✅ |
| JWT carries only "lightweight session context" | Minimal claims | Adds **`NationalId`** (national ID, PII) + `Email` + duplicate `Id` (`TokenService.cs:24-33`) | ❌ (PII leak, see Divergence 2) |
| Bootstrap contains **Authorized Scopes** | Collection of allowed faculties/programs/semesters/years | `LoginResponseDto` has **no AuthorizedScopes** field — only `Permissions` + `ActiveScope` (`LoginResponseDto.cs:7-22`) | ❌ |
| Bootstrap contains Active Scope (Structural+Temporal) | faculty/program + year/semester | `ActiveScope` = Structural.NodeId + Temporal.{AcademicYearId,SemesterId} (`LoginResponseDto.cs:39-54`; populated `PermissionManagementService.cs:93-100`) | ✅ (Structural is single NodeId, not faculty+program pair) |
| Permissions format `Module.Resource.Action` | String perms list | `Permissions: List<PermissionDto>`; students get empty list (context-scoped) (`PermissionManagementService.cs:102-106`) | ✅ |
| Backend final authority; bootstrap is frontend-only | Token not trusted for authz | Confirmed — perms not in token; resolved server-side | ✅ |
| No CQRS / MediatR / event bus | Direct service orchestration | Direct DI calls, no MediatR (`AuthenticationService.cs`) | ✅ |
| Route `POST /auth/login` | `/auth/login` | Actual route `api/auth/login` (`AuthController.cs:10,21`) | ⚠️ minor prefix diff |

## Step 4 — Divergence Analysis

### Divergence 1 — `IAuthorizationResponseBuilder` does not exist in production
- **Model:** `AuthenticationService` depends on `IAuthorizationResponseBuilder` to build permissions/authorized-scopes/active-scope/login DTO.
- **Implementation:** That interface exists only under `tests/Core.UniTests/Authentication/IAuthorizationResponseBuilder.cs`. Production uses `IPermissionManagementService.GetBootstrapContextAsync` (`AuthenticationService.cs:68`).
- **Evidence:** Grep for `IAuthorizationResponseBuilder` → only test project + doc. `PermissionManagementService.cs:63`.
- **Impact:** Documentation/Operational — the named contract and responsibility split in the doc do not match the codebase; a reader wiring/maintaining auth will look for a type that isn't there. No functional defect.
- **Severity:** Medium
- **Notes:** Behavior is implemented, just under a different abstraction. Doc is stale on the component name and responsibility boundary.

### Divergence 2 — National ID (PII) embedded in JWT
- **Model:** JWT "contains only lightweight session context"; included = UserId, Role, SessionVersion, optional minimal context.
- **Implementation:** Token also carries `NationalId` (the login identifier / national ID) and `Email` as claims (`TokenService.cs:25,33`).
- **Evidence:** `TokenService.cs:24-33`.
- **Impact:** Audit/Privacy — JWT payload is base64, not encrypted; national ID is readable by anyone holding the token (client storage, logs, proxies). Not "lightweight session context" as documented.
- **Severity:** Medium
- **Notes:** Confirmed presence; whether acceptable is a policy call, but it diverges from the documented exclusion of non-session data.

### Divergence 3 — "Authorized Scopes" bootstrap section not implemented
- **Model:** Bootstrap response contains an **Authorized Scopes** collection (allowed faculties/programs/semesters/academic years).
- **Implementation:** `LoginResponseDto` exposes only `Permissions` + `ActiveScope`. No authorized-scopes list is produced.
- **Evidence:** `LoginResponseDto.cs:7-22`; `GetBootstrapContextAsync` sets only attributes, active scope, permissions (`PermissionManagementService.cs:63-108`).
- **Impact:** Functional — frontend cannot enumerate allowed operational boundaries from login as the model promises; scope switching (a documented goal) has no source list. Active scope is present but the broader authorized-scope set is absent.
- **Severity:** Medium
- **Notes:** Doc over-specifies vs implementation. Either doc is aspirational or feature was dropped.

### Divergence 4 — Active Scope Structural shape
- **Model:** Structural context = faculty **and** program (two fields).
- **Implementation:** `StructuralScopeDto` is a single `NodeId` (`LoginResponseDto.cs:45-48`), populated from `user.StructureNodeId`.
- **Evidence:** `LoginResponseDto.cs:45-48`; `PermissionManagementService.cs:100`.
- **Impact:** Functional/low — structure is modeled as a single node in a tree rather than explicit faculty+program; semantically equivalent if the node resolves the hierarchy, but does not match the documented two-field shape.
- **Severity:** Low

## Step 5 — Hidden-Logic Review

- **Timing attack:** `AuthenticateAsync` verifies a dummy hash when user not found (`AuthenticationService.cs:48`) — sound, not documented but consistent with intent. No issue.
- **Refresh token replay:** `RotateAsync` detects reuse of a revoked+rotated token, walks the chain, revokes successors, bumps SessionVersion (`RefreshTokenService.cs:67-77`). Chain walk is depth-capped + visited-set guarded against loops (`RefreshTokenService.cs:136-158`). No verified issue.
- **Rotation atomicity:** Successor issued and predecessor stamped in one `SaveChangesAsync` (`RefreshTokenService.cs:93-105`). Concurrent double-refresh of the same raw token is a race: both reads see `RevokedAt == null` before either commits; DB has no unique guard shown on `TokenHash` lookup path, so two successors could be issued. **Potential concurrency risk** — not proven without the EF config/index; flagged as unverified.
- **`ResolveCredentialAsync` order:** Students matched before Staff on `NationalId`; if the same `NationalId` exists in both tables a Student always wins. No uniqueness cross-check. Low risk, undocumented.
- **Logout/ChangePassword/Refresh flows** exist in code but are **undocumented** in the model (model only covers login). Not defects; doc incompleteness.

## Step 6 — Flow Verification

### Login Flow
- **Expected:** `POST /auth/login → AuthController → AuthenticationService → resolver/hasher/builder/token → LoginResponseDto`.
- **Actual:** `api/auth/login → AuthController.Login → AuthenticationService.AuthenticateAsync → resolver + hasher + tokenService + refreshTokenService + permissionManagementService → LoginResponseDto`.
- **Match:** NO (partial)
- **Differences:** Builder is `IPermissionManagementService`, not `IAuthorizationResponseBuilder`; refresh token issuance added; route prefixed `api/`.

### Logout Flow
- **Expected:** Not documented.
- **Actual:** Revokes all refresh tokens + increments SessionVersion (`AuthenticationService.cs:75-82`).
- **Match:** Not documented (implemented).

### Refresh Flow
- **Expected:** Not documented.
- **Actual:** Rotating refresh tokens with replay detection (`AuthenticationService.cs:107-119`, `RefreshTokenService.cs:54-107`).
- **Match:** Not documented (implemented).

### Change Password Flow
- **Expected:** Not documented.
- **Actual:** Verifies current, updates hash, revokes tokens, bumps SessionVersion (`AuthenticationService.cs:84-105`).
- **Match:** Not documented (implemented).

## Step 7 — Final Verdict

### Scores
- Architecture Match: 7/10
- Logic Correctness: 8/10
- Operational Safety: 7/10
- Maintainability: 7/10

### Confirmed Issues
1. `IAuthorizationResponseBuilder` named in doc absent from production (Medium).
2. National ID + Email embedded in JWT vs documented lightweight-session-only (Medium).
3. Documented "Authorized Scopes" bootstrap section not implemented (Medium).
4. Structural active scope is single NodeId, not faculty+program (Low).

### Model Violations
- Component/responsibility split (builder) differs from code.
- JWT claim minimalism violated (PII).
- Bootstrap response shape (authorized scopes, structural pair) differs.

### False Positives
- "No CQRS/MediatR" constraint — upheld in code.
- Password hashing / timing safety — implemented soundly.

### Findings Summary
- **Matches:** Orchestration service, dual credential sources, password hashing, JWT signing/expiry, exclusion of permission graphs, no-CQRS constraint, active scope temporal population.
- **Does not match:** Builder abstraction name, JWT PII inclusion, missing Authorized Scopes, structural scope shape, route prefix.
- **Needs attention:** Confirm whether National ID in JWT is intended (privacy); confirm refresh-rotation concurrency guard (DB unique index on `TokenHash`); reconcile doc's builder/authorized-scopes language with actual `PermissionManagementService` design or update the doc.
