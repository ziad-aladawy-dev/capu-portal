# Authorization — Verification Audit

**Docs:** `docs/Authorization_Model.md` (conceptual) + `docs/Authorization_Implementation.md` (engineering reference)
**Scope:** Scope-first hybrid RBAC + ABAC permission system.
**Date:** 2026-06-02

---

## Step 1 — Model Extract

- **Purpose:** Scope filters data first; permissions evaluated inside scope. Manifest is single source of truth for permission identities; storage is per-action rows; runtime is pure set ops `effective = (allow ∪ implied) − (deny ∪ reverse-implied)`, with implies folded at write time.
- **Assumptions:** Manifests declared in code per domain; `PermissionManifestSynchronizer` reconciles → DB additively; `PermissionNames` is the only attribute literal source; scope columns inline on grant rows.
- **Invariants:** No integer ladder in storage; implies per-resource (no global CRUD); deny via reverse-implies; cache key embeds epoch+user-version; deny supersedes allow.
- **Expected flow:** `[HasPermission] → PermissionHandler → GetPermissionLookupAsync (cache/DB) → Contains → IEffectiveScope ABAC check`.
- **Failure behavior:** Missing action or out-of-scope → 403 + audit log.
- **State:** Closable entities (`IsClosed`, `Close/Reopen/EnsureMutable`).

## Step 2 — Implementation Files

- `Core.Infrastructure/Services/Authorization/PermissionManagementService.cs` (eval + writes + cache)
- `…/Authorization/PermissionService.cs` (scope-filtered row loads)
- `…/Authorization/Manifest/ManifestActionExpander.cs` (level↔action, forward/reverse implies)
- `…/Authorization/Manifest/PermissionManifestSynchronizer.cs` (additive sync + rename)
- `…/Authorization/PermissionCacheInvalidator.cs` (user/role/epoch)
- `…/Authorization/EffectiveScope.cs` (ABAC row-level)
- `API/Infrastructure/PermissionHandler.cs`, `HasPermissionAttribute.cs`
- `Application/UniversityStructure/UniversityStructureService.cs` (path repair + epoch)
- Abstractions: `IPermissionManagementService`, `IPermissionService`, `IEffectiveScope`, `IPermissionCacheInvalidator`, `PermissionNames`, `ScopeKeys`, Manifest contracts.

## Step 3 — Verification Table

| Assumption | Expected | Actual Implementation | Match |
| --- | --- | --- | --- |
| Scope filters before permission eval | Scope-first | `GetPermissionLookupAsync` resolves scope, scope-filters rows via `PermissionService.LoadAssignmentsAsync/LoadOverridesAsync` then set-ops (`PermissionManagementService.cs:297-353`, `PermissionService.cs:51-82`) | ✅ |
| Runtime = pure set ops, no arithmetic | `allow − deny`, no MAX(level) | `allowed.ExceptWith(denied)` only (`PermissionManagementService.cs:349`) | ✅ |
| Implies folded at write time | Reads are hash lookups | Eval does no expansion; `Contains` only (`PermissionHandler.cs:41`) | ✅ |
| Forward implies on allow writes | High verb grants lower | `ResolveActionSet` → `ExpandActions` (`PermissionManagementService.cs:739-741`, `ManifestActionExpander.cs:57`) | ✅ |
| Reverse implies on deny writes | Deny EditClose denies Open/Delete | `ExpandDenyActions` → `ExpandReverseImplied` (`ManifestActionExpander.cs:68-82`) | ✅ |
| Per-action storage, string Action | One row per action | Rows keyed by `rp.Action`/`ov.Action` strings (`PermissionManagementService.cs:332,344`) | ✅ |
| Override Type Allow/Deny | enum | `OverrideType.Allow/Deny` (`PermissionManagementService.cs:345-346`) | ✅ |
| Scope match rule (Global OR equal; path StartsWith) | Inline SQL-translatable predicate | Exact predicate present (`PermissionService.cs:60-64, 75-79`) | ✅ |
| Subtree match via materialised path | descendant inherits | `scope.StructureNodePath.StartsWith(sr.StructureNodePath)` (`PermissionService.cs:63-64`) | ✅ |
| Cache key shape | `perm_lookup_{epoch}_{userId}_{version}_{year}_{semester}_{node}` | Exact (`PermissionManagementService.cs:307`) | ✅ |
| Epoch orphans all entries | InvalidateAll rotates epoch | `InvalidateAllAsync` sets new Guid epoch (`PermissionCacheInvalidator.cs:59-64`) | ✅ |
| User version invalidation on writes | every write path bumps user | `InvalidateUserCacheAsync` after each Create/Update (`PermissionManagementService.cs:547,587`) | ✅ |
| Role invalidation enumerates assignees | bump each | `InvalidateRoleAsync` walks `StaffRoles` (`PermissionCacheInvalidator.cs:41-57`) | ✅ |
| `[HasPermission]` → handler → lookup → Contains | ASP.NET handler | `PermissionHandler.HandleRequirementAsync` (`PermissionHandler.cs:31-68`) | ✅ |
| ABAC scope check on resource id | IEffectiveScope.CanAccess* | `EvaluateScopeAsync` switch over `ScopeKind` (`PermissionHandler.cs:70-94`) | ✅ |
| Deny/miss → 403 + audit | LogPermissionDenied | `_audit.LogPermissionDeniedAsync` then framework 403 (`PermissionHandler.cs:43-46,59-62`) | ✅ |
| Synchronizer additive (never deletes) | add + refresh only | No deletes; create/refresh/rename only (`PermissionManifestSynchronizer.cs`) | ✅ |
| Rename preserves FK grants | ResourceId stable | `PreviousKeys` rename updates `Key`, keeps row Id (`PermissionManifestSynchronizer.cs:97-126`) | ✅ (richer than doc; doc only mentions DisplayName refresh) |
| Path-drift repair on node move | RepairPermissionPathPrefix + InvalidateAll | `MoveNodeAsync` calls both (`UniversityStructureService.cs:325,328`) | ✅ |
| Closable entities enforce in domain | EnsureMutable/Close/Reopen | (model §7; domain `AcademicYear`/`Semester`) | ✅ (assumed; ClosableEntityTests present) |
| Epoch invalidated when **manifests** change | sync rotates epoch | Synchronizer does **not** call `InvalidateAllAsync` (`PermissionManifestSynchronizer.cs`) | ❌ (see Divergence 1) |
| Bootstrap effective = allow−deny | net per resource | Bootstrap computes per **ScopedResourceKey** incl. exact path/year/sem (`PermissionManagementService.cs:167-274`) | ⚠️ (see Divergence 2) |

## Step 4 — Divergence Analysis

### Divergence 1 — Manifest change does not rotate the global epoch
- **Model:** `Authorization_Model.md` §5.1 and `Authorization_Implementation.md` §5.4 both state the global epoch is invalidated "when system-wide roles **or manifests** change."
- **Implementation:** `PermissionManifestSynchronizer.SynchronizeAsync` adds/refreshes Modules+Resources but never calls `IPermissionCacheInvalidator.InvalidateAllAsync` (`PermissionManifestSynchronizer.cs:30-151`). Epoch rotation is wired only to `UniversityStructureService.MoveNodeAsync` (`UniversityStructureService.cs:328`). Role changes use `InvalidateRoleAsync`/`InvalidateUserAsync`, not the epoch.
- **Evidence:** Grep `InvalidateAllAsync` → only `UniversityStructureService.cs:328` + the invalidator definition. None in synchronizer.
- **Impact:** Operational/Low. Newly synced resources represent permissions nobody has been granted yet, so a stale cache cannot leak access — the doc’s stated trigger is simply broader than reality. The Implementation doc’s own §11 migration note even says the epoch is *not* auto-bumped and must be triggered manually, which contradicts §5.4’s prose.
- **Severity:** Low
- **Notes:** Documentation imprecision, not a security hole. Reconcile §5.1/§5.4 wording with §11.

### Divergence 2 — Bootstrap permission enumeration is scope-keyed; runtime is flattened
- **Model:** §4.2 / Impl §5.1 describe `effective = allow − deny` as the user’s effective set.
- **Implementation:** `GetEffectivePermissionsAsync` (login bootstrap) computes allow−deny **per `ScopedResourceKey`** where the key includes the exact `StructureNodePath`, `Year`, `Semester` (`PermissionManagementService.cs:277-278, 248-275`). A deny written at a *broader* scope (e.g. `Year=Global`) therefore does **not** subtract from an allow at a *narrower* scope, because the dictionary keys differ. Runtime `GetPermissionLookupAsync` instead scope-filters rows and then flattens into a single allowed/denied set (`PermissionManagementService.cs:327-349`), so a broader-scope deny *does* cancel a narrower allow there.
- **Evidence:** Compare the two methods: bootstrap `BuildEffectivePermissionDtos` iterates per-key (`:253`); runtime uses flat `HashSet` (`:327-349`).
- **Impact:** Audit/Functional/Low. The bootstrap set is explicitly a frontend hint with the backend authoritative (`Authentication_Model.md`), so no access is actually granted incorrectly. But the permission list shown to a management UI can disagree with the enforced runtime decision for cross-scope denies.
- **Severity:** Low
- **Notes:** Behaviorally safe because enforcement is at runtime; flagged for UI-accuracy awareness.

### Accepted Deviation (NOT a defect) — Role command handlers are CQRS in Core.Infrastructure
- `CreateRoleCommand` / `UpdateRoleCommand` / `DeleteRoleCommand` live under `Core.Infrastructure/Services/Roles/Commands`. The Implementation doc frames auth as direct-service with manifests; the role mutation surface is CQRS-styled. Per project decision this is intentionally retained and is **not** counted as a divergence.

## Step 5 — Hidden-Logic Review

- **Deny fail-open guard:** Reverse-implies on deny is implemented and matches the documented "fail-open deny" mitigation (`ManifestActionExpander.cs:68-82`). No issue.
- **Override toggle logic:** `PersistOverrideAsync` deletes the opposite-type row instead of stacking conflicting allow+deny (`PermissionManagementService.cs:694-703`). Sensible; not documented but consistent.
- **ABAC no-HTTP refusal:** `EvaluateScopeAsync` returns `false` when no HttpContext (`PermissionHandler.cs:73-79`) — fail-closed. Good.
- **System bypass:** `EffectiveScope` short-circuits to `true` when `IExecutionContext.IsSystem` (`EffectiveScope.cs:44,68,126,136`). Documented (Runtime Hardening). Background/outbox work bypasses per-user scope — correct by design but worth noting as a trust boundary.
- **`CanAccessAcademicYear/Semester` permissiveness:** Returns `true` when no active header lock (`EffectiveScope.cs:130-131,139-140`). Documented ("header lock must match payload") — permissive default, not a defect.
- **Batch transactions:** `BatchCreate/UpdateAssignmentsAsync` wrap in a relational transaction, no-op on InMemory (`PermissionManagementService.cs:464-499`). All-or-nothing as intended. No issue.
- **`PermissionSyncReport` drops Updated/Renamed counts:** `modulesUpdated`, `resourcesUpdated`, `resourcesRenamed` are computed but not surfaced in the returned report (`PermissionManifestSynchronizer.cs:147-150`). Cosmetic/observability only.
- **No verified security issue found** in the runtime evaluation path.

## Step 6 — Flow Verification

### Permission evaluation (HasPermission)
- **Expected:** handler → lookup (cache/DB) → Contains → ABAC scope check → succeed/403.
- **Actual:** `PermissionHandler.HandleRequirementAsync` exactly this (`PermissionHandler.cs:31-68`).
- **Match:** YES — Matches documented behavior.

### Role/override create & update
- **Expected:** per-action fan-out with forward(allow)/reverse(deny) implies, scope columns inline, user cache bump.
- **Actual:** `CreateAssignmentAsync`/`UpdateAssignmentAsync` + `PersistOverrideAsync` + `InvalidateUserCacheAsync` (`PermissionManagementService.cs:501-613`).
- **Match:** YES.

### Manifest synchronize (startup)
- **Expected:** additive reconcile, refresh metadata, never delete, preserve FKs on rename.
- **Actual:** Matches; adds rename-detection beyond doc (`PermissionManifestSynchronizer.cs`).
- **Match:** YES (impl exceeds doc).

### Cache invalidation
- **Expected:** user-version, role-enumerate, global-epoch knobs.
- **Actual:** All three present (`PermissionCacheInvalidator.cs`). Epoch trigger narrower than doc (Divergence 1).
- **Match:** Partial.

### Node move path repair
- **Expected:** repair snapshots + invalidate all.
- **Actual:** `MoveNodeAsync` both (`UniversityStructureService.cs:325,328`).
- **Match:** YES.

## Step 7 — Final Verdict

### Scores
- Architecture Match: 9/10
- Logic Correctness: 9/10
- Operational Safety: 8/10
- Maintainability: 9/10

### Confirmed Issues
1. Manifest sync does not rotate the global epoch despite doc claim (Low; doc §5.4 vs §11 also self-contradict).
2. Bootstrap permission enumeration is scope-keyed and can disagree with flattened runtime evaluation for cross-scope denies (Low; backend remains authoritative).
3. `PermissionSyncReport` omits update/rename counts (cosmetic).

### Model Violations
- Epoch invalidation trigger description (roles/manifests) broader than implementation.

### False Positives
- "No arithmetic / no integer ladder in storage" — upheld.
- "Reverse-implies deny" fail-open hazard — correctly mitigated.
- Role-handler CQRS — accepted deviation, not a violation.

### Findings Summary
- **Matches:** Scope-first filtering, set-based evaluation, forward/reverse implies at write time, cache key shape + user/role/epoch invalidation, additive synchronizer (with bonus rename handling), ABAC handler, node-move path repair, fail-closed scope defaults.
- **Does not match:** Epoch-on-manifest-change claim; bootstrap vs runtime deny-scoping nuance.
- **Needs attention:** Reconcile the doc’s epoch-trigger wording with the code (and with its own §11). Decide whether bootstrap enumeration should mirror runtime cross-scope deny semantics for UI accuracy. Otherwise the implementation is a faithful, well-guarded realization of the model.
