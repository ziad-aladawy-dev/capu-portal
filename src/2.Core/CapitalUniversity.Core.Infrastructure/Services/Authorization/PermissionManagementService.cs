using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

/// <summary>
/// Bundles the cache surface used by <see cref="PermissionManagementService"/>.
/// Lets the service constructor stay under the 7-parameter limit while keeping
/// per-dependency injection explicit.
/// </summary>
public sealed record PermissionCacheCoordinator(
    ICacheService Cache,
    PermissionCacheOptions Options,
    IPermissionCacheInvalidator? Invalidator);

public class PermissionManagementService : IPermissionManagementService
{
    private readonly IPermissionService _permissionService;
    private readonly IRequestContext _requestContext;
    private readonly IScopeResolver _scopeResolver;
    private readonly CoreDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly PermissionCacheOptions _cacheOptions;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;
    private readonly IAuthAuditLogger? _audit;
    private readonly ManifestActionExpander _expander;
    private readonly ILocalizationService _localization;

    public PermissionManagementService(
        IPermissionService permissionService,
        IRequestContext requestContext,
        IScopeResolver scopeResolver,
        CoreDbContext dbContext,
        PermissionCacheCoordinator cacheCoordinator,
        ManifestActionExpander expander,
        ILocalizationService localization,
        IAuthAuditLogger? audit = null)
    {
        _permissionService = permissionService;
        _requestContext = requestContext;
        _scopeResolver = scopeResolver;
        _dbContext = dbContext;
        _cache = cacheCoordinator.Cache;
        _cacheOptions = cacheCoordinator.Options;
        _cacheInvalidator = cacheCoordinator.Invalidator;
        _audit = audit;
        _expander = expander;
        _localization = localization;
    }

    public async Task<LoginResponseDto> GetBootstrapContextAsync(IUserCredential user, CancellationToken cancellationToken = default)
    {
        var response = new LoginResponseDto
        {
            User = new UserInfoDto
            {
                Id = user.Id,
                // Personal name is bilingual JSON when authored that way; the
                // login bootstrap renders it in the caller's culture so the
                // header chip and welcome screen pick up "Menna Magdy"
                // / "منة مجدى" without a follow-up GET.
                Name = _localization.Get<string>(user.Name),
                Email = user.Email
            }
        };

        // 1. Resolve Attributes (Uni, Faculty, Dept)
        if (user.StructureNodeId.HasValue)
        {
            var node = await _dbContext.StructureNodes
                .Include(n => n.Parent)
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == user.StructureNodeId.Value, cancellationToken);

            if (node != null)
            {
                response.User.Attributes = await ResolveAttributesAsync(node, cancellationToken);
            }
        }

        // 2. Resolve Active Scope (Temporal)
        var currentYear = await _dbContext.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent, cancellationToken);
        var currentSem = await _dbContext.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.IsCurrent, cancellationToken);

        response.ActiveScope.Temporal.AcademicYearId = currentYear?.Id;
        response.ActiveScope.Temporal.SemesterId = currentSem?.Id;

        response.ActiveScope.Structural.NodeId = user.StructureNodeId;

        if (user.Role == "Student")
        {
            // Students are context-scoped: no explicit permission grants. The frontend
            // treats absence of a permission entry as "act inside the user's StructureNode".
            response.Permissions = new List<PermissionDto>();
        }
        else
        {
            response.Permissions = await GetEffectivePermissionsAsync(user.Id, cancellationToken);
        }

        return response;
    }

    private async Task<UserAttributesDto> ResolveAttributesAsync(StructureNode node, CancellationToken cancellationToken)
    {
        var attributes = new UserAttributesDto();

        var currentNode = node;
        while (currentNode != null)
        {
            switch (currentNode.Type)
            {
                case StructureNodeType.University:
                    attributes.Uni = _localization.Get<string>(currentNode.Name);
                    break;
                case StructureNodeType.Faculty:
                    attributes.Faculty = _localization.Get<string>(currentNode.Name);
                    break;
                case StructureNodeType.Department:
                case StructureNodeType.Program:
                    attributes.Department = _localization.Get<string>(currentNode.Name);
                    break;
            }

            if (currentNode.ParentId.HasValue)
            {
                currentNode = await _dbContext.StructureNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == currentNode.ParentId.Value, cancellationToken);
            }
            else
            {
                currentNode = null;
            }
        }

        return attributes;
    }

    public async Task<List<PermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Role-permission aggregation + effective computation is DB-heavy (StaffRoles
        // + RolePermissions join + StaffPermissions overrides). Cache it through the
        // stampede-protected path keyed by the SAME epoch/user-version stamps the
        // lookup cache uses, so an assignment edit (which rotates the user version)
        // or a global invalidate (epoch) orphans this entry too — no new
        // invalidation surface, consistent keying per (user, version).
        var version = await GetUserPermissionVersionAsync(userId, cancellationToken);
        var epoch = await GetGlobalEpochAsync(cancellationToken);
        var cacheKey = $"perm_effective_{epoch}_{userId}_{version}";

        var dtos = await _cache.GetOrSetAsync<List<PermissionDto>>(
            cacheKey,
            async ct =>
            {
                // Bootstrap view: return every grant the user holds, each with its own scope attached.
                // Do NOT filter through IRequestContext here — that intersection belongs to the runtime
                // authorization check (GetPermissionLookupAsync), not to enumerating what the user has.
                var assignments = await _dbContext.StaffRoles
                    .Where(sr => sr.StaffId == userId)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var rolePermsByRole = await LoadRolePermissionsByRoleAsync(assignments, ct);
                var overrides = await LoadOverridesAsync(userId, ct);

                // Set-based evaluation: effective = allow − deny. Storage is already per-action,
                // so no implies expansion at evaluation time — implies are folded in at write time.
                var allowByKey = new Dictionary<ScopedResourceKey, HashSet<string>>();
                var denyByKey = new Dictionary<ScopedResourceKey, HashSet<string>>();

                CollectRoleAllows(assignments, rolePermsByRole, allowByKey);
                CollectOverrideActions(overrides.Where(o => o.Type == OverrideType.Allow), allowByKey);
                CollectOverrideActions(overrides.Where(o => o.Type == OverrideType.Deny), denyByKey);

                return BuildEffectivePermissionDtos(allowByKey, denyByKey);
            },
            TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes),
            cancellationToken);

        return dtos ?? new List<PermissionDto>();
    }

    private async Task<Dictionary<Guid, List<RolePermission>>> LoadRolePermissionsByRoleAsync(
        IReadOnlyCollection<StaffRoleAssignment> assignments, CancellationToken cancellationToken)
    {
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
        var rolePerms = await _dbContext.RolePermissions
            .Include(rp => rp.Resource)
                .ThenInclude(r => r.Module)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rolePerms.GroupBy(rp => rp.RoleId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private Task<List<StaffPermissionOverride>> LoadOverridesAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Exclude overrides whose temporal window has already closed (see the
        // matching note in PermissionService.LoadOverridesAsync). Read-time filter
        // — the row survives until ExpireOverridesAsync prunes it, but it stops
        // contributing to the effective set the moment it expires.
        var now = DateTime.UtcNow;
        return _dbContext.StaffPermissions
            .Include(sp => sp.Resource)
                .ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == userId && (sp.ExpiresAt == null || sp.ExpiresAt > now))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static void CollectRoleAllows(
        IEnumerable<StaffRoleAssignment> assignments,
        Dictionary<Guid, List<RolePermission>> rolePermsByRole,
        Dictionary<ScopedResourceKey, HashSet<string>> target)
    {
        foreach (var assignment in assignments)
        {
            if (!rolePermsByRole.TryGetValue(assignment.RoleId, out var perms)) continue;

            var scope = new ScopeKey(
                assignment.StructureNodeId,
                assignment.StructureNodePath,
                assignment.Year,
                assignment.Semester);

            foreach (var rp in perms)
            {
                var key = new ScopedResourceKey(scope, rp.Resource.Module.ModuleKey, rp.Resource.Key);
                AddAction(target, key, rp.Action);
            }
        }
    }

    private static void CollectOverrideActions(
        IEnumerable<StaffPermissionOverride> overrides,
        Dictionary<ScopedResourceKey, HashSet<string>> target)
    {
        foreach (var ov in overrides)
        {
            var scope = new ScopeKey(ov.StructureNodeId, ov.StructureNodePath, ov.Year, ov.Semester);
            var key = new ScopedResourceKey(scope, ov.Resource.Module.ModuleKey, ov.Resource.Key);
            AddAction(target, key, ov.Action);
        }
    }

    private static void AddAction(
        Dictionary<ScopedResourceKey, HashSet<string>> target,
        ScopedResourceKey key,
        string action)
    {
        if (string.IsNullOrEmpty(action)) return;
        if (!target.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            target[key] = set;
        }
        set.Add(action);
    }

    private static List<PermissionDto> BuildEffectivePermissionDtos(
        Dictionary<ScopedResourceKey, HashSet<string>> allowByKey,
        Dictionary<ScopedResourceKey, HashSet<string>> denyByKey)
    {
        var result = new List<PermissionDto>();
        foreach (var (key, allowed) in allowByKey)
        {
            var effective = new HashSet<string>(allowed, StringComparer.Ordinal);
            if (denyByKey.TryGetValue(key, out var denied))
            {
                effective.ExceptWith(denied);
            }
            if (effective.Count == 0) continue;

            var scopeDto = BuildScopeDto(key.Scope);
            foreach (var action in effective)
            {
                result.Add(new PermissionDto
                {
                    Module = key.Module,
                    Resource = key.Resource,
                    Action = action,
                    Scope = scopeDto,
                });
            }
        }
        return result;
    }

    private sealed record ScopeKey(Guid? StructureNodeId, string? StructureNodePath, string Year, string Semester);
    private sealed record ScopedResourceKey(ScopeKey Scope, string Module, string Resource);

    private static PermissionScopeDto BuildScopeDto(ScopeKey k)
    {
        var isGlobalYear = k.Year == ScopeKeys.Global;
        var isGlobalSemester = k.Semester == ScopeKeys.Global;

        return new PermissionScopeDto
        {
            IsGlobalStructural = !k.StructureNodeId.HasValue,
            StructureNodeId = k.StructureNodeId,
            StructureNodePath = k.StructureNodePath,
            IsGlobalYear = isGlobalYear,
            AcademicYearId = !isGlobalYear && Guid.TryParse(k.Year, out var y) ? y : null,
            IsGlobalSemester = isGlobalSemester,
            SemesterId = !isGlobalSemester && Guid.TryParse(k.Semester, out var s) ? s : null,
        };
    }

    public async Task<HashSet<string>> GetPermissionLookupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var year = _requestContext.ActiveAcademicYearId?.ToString() ?? ScopeKeys.Global;
        var semester = _requestContext.ActiveSemesterId?.ToString() ?? ScopeKeys.Global;
        var structureNodeId = _requestContext.ActiveStructureNodeId;
        var structureKey = structureNodeId?.ToString() ?? ScopeKeys.Global;
        var version = await GetUserPermissionVersionAsync(userId, cancellationToken);
        var epoch = await GetGlobalEpochAsync(cancellationToken);
        // Epoch participates in the key so InvalidateAllAsync (manifest sync, schema
        // migration) orphans every cached entry without enumerating keys.
        var cacheKey = $"perm_lookup_{epoch}_{userId}_{version}_{year}_{semester}_{structureKey}";

        // Stampede-protected read-through (CRITICAL hot path — runs on every
        // authorized request). Under a burst of concurrent misses for the same
        // (epoch, user, version, scope) only ONE instance executes the DB
        // rebuild; the rest wait on the distributed lock and reuse the result.
        // The cache key and the version/epoch invalidation model are unchanged.
        var allowed = await _cache.GetOrSetAsync<HashSet<string>>(
            cacheKey,
            async ct =>
            {
                var scope = await _scopeResolver.ResolveAsync(userId, year, semester, structureNodeId, ct);

                var rawPermissions = await _permissionService.GetAllPermissionsAsync(userId, scope, ct);

                var roleIds = rawPermissions.Assignments.Select(a => a.RoleId).Distinct().ToList();
                var rolePermsDb = await _dbContext.RolePermissions
                    .Include(rp => rp.Resource)
                        .ThenInclude(r => r.Module)
                    .Where(rp => roleIds.Contains(rp.RoleId))
                    .AsNoTracking()
                    .ToListAsync(ct);

                var built = new HashSet<string>(StringComparer.Ordinal);
                var denied = new HashSet<string>(StringComparer.Ordinal);

                foreach (var rp in rolePermsDb)
                {
                    built.Add(PermissionIdentity.Create(rp.Resource.Module.ModuleKey, rp.Resource.Key, rp.Action));
                }

                // rawPermissions.Overrides is already expiry-filtered at the source
                // (PermissionService.LoadOverridesAsync); the redundant ExpiresAt
                // predicate here is defence-in-depth so this hot authorization path
                // can never fail open on an expired override.
                var nowLookup = DateTime.UtcNow;
                var overridesDb = await _dbContext.StaffPermissions
                    .Include(sp => sp.Resource)
                        .ThenInclude(r => r.Module)
                    .Where(sp => rawPermissions.Overrides.Select(o => o.Id).Contains(sp.Id)
                        && (sp.ExpiresAt == null || sp.ExpiresAt > nowLookup))
                    .AsNoTracking()
                    .ToListAsync(ct);

                foreach (var ov in overridesDb)
                {
                    var canonical = PermissionIdentity.Create(ov.Resource.Module.ModuleKey, ov.Resource.Key, ov.Action);
                    if (ov.Type == OverrideType.Allow) built.Add(canonical);
                    else denied.Add(canonical);
                }

                built.ExceptWith(denied);
                return built;
            },
            TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes),
            cancellationToken);

        // Defensive copy — callers must not be able to mutate the cached instance
        // (in memory mode GetAsync returns the stored reference directly).
        return new HashSet<string>(allowed ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
    }

    public async Task<int> ExpireOverridesAsync(CancellationToken cancellationToken = default)
    {
        // Manually-triggered expiry: an override's temporal window ends at the
        // EndDate of its scoped Semester (or AcademicYear when only a year is
        // scoped), stamped onto ExpiresAt at write time (see ResolveTemporalExpiryAsync).
        // Once that moment is now-or-past the grant is dead weight, so hard-delete
        // it — the same physical removal the toggle-to-default path uses — and rotate
        // each affected user's cache version so the next lookup rebuilds without the
        // stale row. Global / AlwaysActive overrides have a null ExpiresAt and are
        // never selected here.
        var now = DateTime.UtcNow;
        var expired = await _dbContext.StaffPermissions
            .Where(sp => sp.ExpiresAt != null && sp.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return 0;

        var affectedUsers = expired.Select(e => e.StaffId).Distinct().ToList();
        _dbContext.StaffPermissions.RemoveRange(expired);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var userId in affectedUsers)
        {
            await InvalidateUserCacheAsync(userId, cancellationToken);
        }

        return expired.Count;
    }

    public async Task<int> BackfillOverrideExpiryAsync(CancellationToken cancellationToken = default)
    {
        // Legacy rows (written before ExpiresAt was stamped) carry a null expiry
        // even when scoped to a bounded semester/year. Re-derive the end of that
        // window and fill it in. Tracked load (no AsNoTracking) so the property
        // edit persists. Genuinely Global rows have no end and are skipped by the
        // predicate below.
        var candidates = await _dbContext.StaffPermissions
            .Where(sp => sp.ExpiresAt == null
                && (sp.Year != ScopeKeys.Global || sp.Semester != ScopeKeys.Global))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return 0;

        // One lookup per distinct (year, semester) window rather than per row.
        var expiryByScope = new Dictionary<(string Year, string Semester), DateTime?>();
        var updatedUsers = new HashSet<Guid>();
        var updated = 0;

        foreach (var row in candidates)
        {
            var scopeKey = (row.Year, row.Semester);
            if (!expiryByScope.TryGetValue(scopeKey, out var expiry))
            {
                expiry = await ResolveTemporalExpiryAsync(row.Year, row.Semester, cancellationToken);
                expiryByScope[scopeKey] = expiry;
            }

            if (expiry is null) continue; // window no longer resolvable — leave as-is

            row.ExpiresAt = expiry;
            updatedUsers.Add(row.StaffId);
            updated++;
        }

        if (updated == 0) return 0;

        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var userId in updatedUsers)
        {
            await InvalidateUserCacheAsync(userId, cancellationToken);
        }

        return updated;
    }

    /// <summary>
    /// Resolves the moment a scoped override stops applying: the EndDate of the
    /// scoped Semester, or — when only an AcademicYear is scoped — the year's
    /// EndDate. A Global ("AlwaysActive") temporal scope has no end and yields
    /// null, so such overrides never expire.
    /// </summary>
    private async Task<DateTime?> ResolveTemporalExpiryAsync(string year, string semester, CancellationToken cancellationToken)
    {
        // Semester is the finer-grained window — prefer it, then fall back to the year.
        if (!ScopeKeys.IsGlobal(semester) && Guid.TryParse(semester, out var semId))
        {
            var sem = await _dbContext.Semesters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == semId, cancellationToken);
            if (sem is not null) return sem.EndDate;
        }

        if (!ScopeKeys.IsGlobal(year) && Guid.TryParse(year, out var yearId))
        {
            var yr = await _dbContext.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId, cancellationToken);
            if (yr is not null) return yr.EndDate;
        }

        return null;
    }

    /// <summary>A fully-resolved scope: structural node (+ denormalised path) and
    /// temporal Year/Semester keys, plus the temporal-window expiry stamp.</summary>
    private sealed record ResolvedScope(Guid? NodeId, string? NodePath, string Year, string Semester, DateTime? ExpiresAt);

    /// <summary>Resolves a request/role-level scope from its DTO pair.</summary>
    private async Task<ResolvedScope> ResolveScopeAsync(
        StructuralScopeModel structural, TemporalScopeModel temporal, CancellationToken cancellationToken)
    {
        ValidateScopeCombinations(temporal);
        var year = temporal.AlwaysActive ? ScopeKeys.Global : (temporal.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = temporal.AlwaysActive ? ScopeKeys.Global : (temporal.SemesterId?.ToString() ?? ScopeKeys.Global);
        var nodePath = await ResolveNodePathAsync(structural.StructureNodeId, cancellationToken);
        var expiresAt = await ResolveTemporalExpiryAsync(year, semester, cancellationToken);
        return new ResolvedScope(structural.StructureNodeId, nodePath, year, semester, expiresAt);
    }

    /// <summary>
    /// Resolves an override's effective scope. Each axis (structural / temporal) is
    /// taken from the override when it supplies one, otherwise inherited from the
    /// request-level scope. This is what lets a single assignment place a role at
    /// one scope and an individual permission at a different scope.
    /// </summary>
    private async Task<ResolvedScope> ResolveOverrideScopeAsync(
        PermissionOverrideModel perm, ResolvedScope requestScope, CancellationToken cancellationToken)
    {
        if (perm.StructuralScope is null && perm.TemporalScope is null)
        {
            return requestScope;
        }

        var nodeId = requestScope.NodeId;
        var nodePath = requestScope.NodePath;
        if (perm.StructuralScope is not null)
        {
            nodeId = perm.StructuralScope.StructureNodeId;
            nodePath = await ResolveNodePathAsync(nodeId, cancellationToken);
        }

        var year = requestScope.Year;
        var semester = requestScope.Semester;
        var expiresAt = requestScope.ExpiresAt;
        if (perm.TemporalScope is not null)
        {
            ValidateScopeCombinations(perm.TemporalScope);
            year = perm.TemporalScope.AlwaysActive ? ScopeKeys.Global : (perm.TemporalScope.AcademicYearId?.ToString() ?? ScopeKeys.Global);
            semester = perm.TemporalScope.AlwaysActive ? ScopeKeys.Global : (perm.TemporalScope.SemesterId?.ToString() ?? ScopeKeys.Global);
            expiresAt = await ResolveTemporalExpiryAsync(year, semester, cancellationToken);
        }

        return new ResolvedScope(nodeId, nodePath, year, semester, expiresAt);
    }

    private async Task InvalidateUserCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cacheInvalidator is not null)
        {
            await _cacheInvalidator.InvalidateUserAsync(userId, cancellationToken);
            return;
        }
        await _cache.SetAsync(VersionKey(userId), Guid.NewGuid().ToString("N"), TimeSpan.FromHours(_cacheOptions.VersionTtlHours), cancellationToken);
    }

    private async Task<string> GetUserPermissionVersionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = VersionKey(userId);
        var current = await _cache.GetAsync<string>(key, cancellationToken);
        if (!string.IsNullOrEmpty(current)) return current;

        var initial = "0";
        await _cache.SetAsync(key, initial, TimeSpan.FromHours(_cacheOptions.VersionTtlHours), cancellationToken);
        return initial;
    }

    private async Task<string> GetGlobalEpochAsync(CancellationToken cancellationToken)
    {
        var current = await _cache.GetAsync<string>(PermissionCacheInvalidator.GlobalEpochKey, cancellationToken);
        if (!string.IsNullOrEmpty(current)) return current;

        var initial = "0";
        await _cache.SetAsync(PermissionCacheInvalidator.GlobalEpochKey, initial, TimeSpan.FromHours(_cacheOptions.VersionTtlHours), cancellationToken);
        return initial;
    }

    private static string VersionKey(Guid userId) => PermissionCacheInvalidator.UserVersionKey(userId);

    public async Task<PermissionAssignmentResponse?> GetAssignmentAsync(GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken = default)
    {
        // Each null scope param is treated as a wildcard (no filter), so
        // partial-scope queries return all matches for the specified dimensions.
        // Only non-null params constrain the result.  When AlwaysActive is true,
        // Year/Semester are pinned to "Global"/"Global" regardless of the ids.

        var year = query.AlwaysActive ? ScopeKeys.Global : (query.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = query.AlwaysActive ? ScopeKeys.Global : (query.SemesterId?.ToString() ?? ScopeKeys.Global);

        // Roles stay scope-keyed to the queried scope (roles are scope-atomic).
        var roles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == query.UserId &&
                         sr.StructureNodeId == query.StructureNodeId &&
                         sr.Year == year && sr.Semester == semester)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Overrides are returned across EVERY scope the user holds, each tagged with its
        // own scope, so a permission re-scoped away from the role scope is visible from
        // the assignment editor (not just the user permission tree).
        var overrides = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == query.UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (roles.Count == 0 && overrides.Count == 0)
        {
            return null;
        }

        return new PermissionAssignmentResponse
        {
            UserId = query.UserId,
            RoleIds = roles.Select(r => r.RoleId).ToList(),
            PermissionOverrides = BuildOverrideDtosWithScope(overrides),
            StructuralScope = new StructuralScopeModel
            {
                StructureNodeId = query.StructureNodeId
            },
            TemporalScope = new TemporalScopeModel
            {
                AcademicYearId = query.AcademicYearId,
                SemesterId = query.SemesterId,
                AlwaysActive = query.AlwaysActive
            }
        };
    }

    /// <summary>
    /// Projects per-action override rows into DTOs grouped by (resource, type, scope),
    /// with each DTO carrying its <b>own</b> structural + temporal scope reconstructed
    /// from the stored keys. This is what surfaces re-scoped overrides on read — a
    /// single resource can appear multiple times, once per distinct scope it lives at.
    /// </summary>
    private static List<PermissionOverrideModel> BuildOverrideDtosWithScope(IEnumerable<StaffPermissionOverride> rows)
    {
        return rows
            .GroupBy(o => new { o.ResourceId, o.Type, o.StructureNodeId, o.Year, o.Semester })
            .Select(g => new PermissionOverrideModel
            {
                ResourceId = g.Key.ResourceId,
                Type = g.Key.Type,
                Actions = g.Select(o => o.Action).Distinct(StringComparer.Ordinal).ToList(),
                StructuralScope = new StructuralScopeModel { StructureNodeId = g.Key.StructureNodeId },
                TemporalScope = BuildTemporalScope(g.Key.Year, g.Key.Semester),
            })
            .ToList();
    }

    /// <summary>Reconstructs a <see cref="TemporalScopeModel"/> from stored Year/Semester
    /// keys. Both Global → AlwaysActive; otherwise the GUID-valued axes are surfaced.</summary>
    private static TemporalScopeModel BuildTemporalScope(string year, string semester)
    {
        var isGlobalYear = ScopeKeys.IsGlobal(year);
        var isGlobalSemester = ScopeKeys.IsGlobal(semester);
        return new TemporalScopeModel
        {
            AlwaysActive = isGlobalYear && isGlobalSemester,
            AcademicYearId = !isGlobalYear && Guid.TryParse(year, out var y) ? y : null,
            SemesterId = !isGlobalSemester && Guid.TryParse(semester, out var s) ? s : null,
        };
    }

    private static void ValidateScopeCombinations(TemporalScopeModel temporal)
    {
        if (temporal.AlwaysActive && (temporal.AcademicYearId.HasValue || temporal.SemesterId.HasValue))
            throw new ArgumentException("Cannot specify both AlwaysActive=true and specific Temporal limits");
    }

    public async Task<IReadOnlyList<PermissionAssignmentResponse>> BatchCreateAssignmentsAsync(IReadOnlyList<CreatePermissionAssignmentRequest> requests, CancellationToken cancellationToken = default)
    {
        // All-or-nothing — wrap in a single transaction so partial seeding can
        // never leak permissions or leave a user with a half-applied role set.
        // Only relational providers support real transactions; on InMemory the
        // execution strategy is a no-op (matches existing service patterns).
        await using var tx = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var results = new List<PermissionAssignmentResponse>(requests.Count);
        foreach (var req in requests)
        {
            results.Add(await CreateAssignmentAsync(req, cancellationToken));
        }

        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return results;
    }

    public async Task<IReadOnlyList<PermissionAssignmentResponse>> BatchUpdateAssignmentsAsync(IReadOnlyList<UpdatePermissionAssignmentRequest> requests, CancellationToken cancellationToken = default)
    {
        // All-or-nothing — same rationale as BatchCreateAssignmentsAsync.
        await using var tx = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var results = new List<PermissionAssignmentResponse>(requests.Count);
        foreach (var req in requests)
        {
            results.Add(await UpdateAssignmentAsync(req, cancellationToken));
        }

        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return results;
    }

    public async Task<PermissionAssignmentResponse> CreateAssignmentAsync(CreatePermissionAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var requestScope = await ResolveScopeAsync(request.StructuralScope, request.TemporalScope, cancellationToken);

        // Roles are scope-atomic: every RoleId is assigned at the request scope.
        var existingRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == requestScope.NodeId &&
                         sr.Year == requestScope.Year && sr.Semester == requestScope.Semester)
            .Select(sr => sr.RoleId)
            .ToListAsync(cancellationToken);

        foreach (var roleId in request.RoleIds.Where(rid => !existingRoles.Contains(rid)))
        {
            var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, requestScope.Year, requestScope.Semester)
            {
                StructureNodeId = requestScope.NodeId,
                StructureNodePath = requestScope.NodePath
            };
            _dbContext.StaffRoles.Add(roleAssignment);
        }

        // Overrides may each carry their OWN scope, so load every row for the user
        // (tracked) and let PersistOverrideAsync toggle/dedup within each override's
        // resolved scope rather than a single request scope.
        var allOverrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId)
            .ToListAsync(cancellationToken);

        foreach (var perm in request.PermissionOverrides)
        {
            var scope = await ResolveOverrideScopeAsync(perm, requestScope, cancellationToken);
            await PersistOverrideAsync(request.UserId, perm, allOverrides, scope, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await InvalidateUserCacheAsync(request.UserId, cancellationToken);

        if (_audit is not null)
        {
            var added = request.RoleIds.Where(r => !existingRoles.Contains(r)).ToArray();
            if (added.Length > 0)
            {
                await _audit.LogRoleAssignmentChangedAsync(request.UserId, added, Array.Empty<Guid>(), cancellationToken);
            }
        }

        return new PermissionAssignmentResponse
        {
            UserId = request.UserId,
            RoleIds = request.RoleIds,
            PermissionOverrides = request.PermissionOverrides,
            StructuralScope = request.StructuralScope,
            TemporalScope = request.TemporalScope
        };
    }

    public async Task<PermissionAssignmentResponse> UpdateAssignmentAsync(UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var requestScope = await ResolveScopeAsync(request.StructuralScope, request.TemporalScope, cancellationToken);

        // Per-override scope means a remove/add can target a different scope than the
        // request, so operate over the user's full override set.
        var allOverrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId)
            .ToListAsync(cancellationToken);

        await ApplyRoleUpdatesAsync(request, requestScope, cancellationToken);
        await ApplyOverrideUpdatesAsync(request, requestScope, allOverrides, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateUserCacheAsync(request.UserId, cancellationToken);
        await EmitRoleChangeAuditAsync(request, cancellationToken);

        var finalRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == requestScope.NodeId &&
                         sr.Year == requestScope.Year && sr.Semester == requestScope.Semester)
            .Select(sr => sr.RoleId)
            .ToListAsync(cancellationToken);

        // Echo every override the user holds, each tagged with its own scope — matches
        // the reshaped GetAssignment so a write that re-scopes a permission round-trips.
        var finalOverrideRows = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == request.UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PermissionAssignmentResponse
        {
            UserId = request.UserId,
            RoleIds = finalRoles,
            PermissionOverrides = BuildOverrideDtosWithScope(finalOverrideRows),
            StructuralScope = request.StructuralScope,
            TemporalScope = request.TemporalScope
        };
    }

    private async Task<string?> ResolveNodePathAsync(Guid? structureNodeId, CancellationToken cancellationToken)
    {
        if (!structureNodeId.HasValue) return null;
        var node = await _dbContext.StructureNodes.FindAsync(new object[] { structureNodeId.Value }, cancellationToken);
        return node?.Path;
    }

    private async Task ApplyRoleUpdatesAsync(
        UpdatePermissionAssignmentRequest request, ResolvedScope scope, CancellationToken cancellationToken)
    {
        var currentRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == scope.NodeId &&
                         sr.Year == scope.Year && sr.Semester == scope.Semester)
            .ToListAsync(cancellationToken);

        var roleIdsToRemove = request.RolesToRemove.Where(r => !request.RolesToAdd.Contains(r)).ToList();
        var rolesToRemoveEntities = currentRoles.Where(cr => roleIdsToRemove.Contains(cr.RoleId)).ToList();
        _dbContext.StaffRoles.RemoveRange(rolesToRemoveEntities);

        var addedRoles = request.RolesToAdd.Where(rid => currentRoles.All(cr => cr.RoleId != rid)).ToList();
        foreach (var roleId in addedRoles)
        {
            var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, scope.Year, scope.Semester)
            {
                StructureNodeId = scope.NodeId,
                StructureNodePath = scope.NodePath
            };
            _dbContext.StaffRoles.Add(roleAssignment);
        }
    }

    private async Task ApplyOverrideUpdatesAsync(
        UpdatePermissionAssignmentRequest request, ResolvedScope requestScope,
        List<StaffPermissionOverride> allOverrides, CancellationToken cancellationToken)
    {
        foreach (var permToRemove in request.PermissionsToRemove)
        {
            var scope = await ResolveOverrideScopeAsync(permToRemove, requestScope, cancellationToken);
            var actionsToRemove = await ResolveActionSetForResourceAsync(permToRemove, cancellationToken);
            var entitiesToRemove = allOverrides
                .Where(eo => eo.ResourceId == permToRemove.ResourceId && eo.Type == permToRemove.Type
                             && eo.StructureNodeId == scope.NodeId
                             && eo.Year == scope.Year && eo.Semester == scope.Semester
                             && actionsToRemove.Contains(eo.Action))
                .ToList();

            foreach (var entity in entitiesToRemove)
            {
                _dbContext.StaffPermissions.Remove(entity);
                allOverrides.Remove(entity);
            }
        }

        foreach (var permToAdd in request.PermissionsToAdd)
        {
            var scope = await ResolveOverrideScopeAsync(permToAdd, requestScope, cancellationToken);
            await PersistOverrideAsync(request.UserId, permToAdd, allOverrides, scope, cancellationToken);
        }
    }

    private async Task PersistOverrideAsync(
        Guid userId,
        PermissionOverrideModel perm,
        List<StaffPermissionOverride> allOverrides,
        ResolvedScope scope,
        CancellationToken cancellationToken)
    {
        var resource = await _dbContext.Resources
            .Include(r => r.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == perm.ResourceId, cancellationToken);
        if (resource is null) return;

        var targetActions = ResolveActionSet(perm, resource.Module.ModuleKey, resource.Key);
        if (targetActions.Count == 0) return;

        foreach (var action in targetActions)
        {
            // Match only rows at THIS override's resolved scope (allOverrides spans
            // every scope the user holds).
            bool ScopeMatch(StaffPermissionOverride eo) =>
                eo.ResourceId == perm.ResourceId
                && eo.StructureNodeId == scope.NodeId
                && eo.Year == scope.Year
                && eo.Semester == scope.Semester
                && string.Equals(eo.Action, action, StringComparison.Ordinal);

            // Toggle: the exact opposite override at this scope is deleted, returning
            // the action to its default rather than storing a conflicting row.
            var oppositeType = perm.Type == OverrideType.Allow ? OverrideType.Deny : OverrideType.Allow;
            var conflicting = allOverrides.FirstOrDefault(eo => ScopeMatch(eo) && eo.Type == oppositeType);
            if (conflicting != null)
            {
                _dbContext.StaffPermissions.Remove(conflicting);
                allOverrides.Remove(conflicting);
                continue;
            }

            // Prevent duplicates at this scope.
            if (allOverrides.Any(eo => ScopeMatch(eo) && eo.Type == perm.Type)) continue;

            var row = new StaffPermissionOverride(userId, perm.ResourceId, action, perm.Type, scope.Year, scope.Semester)
            {
                StructureNodeId = scope.NodeId,
                StructureNodePath = scope.NodePath,
                // End of the temporal scope (semester/year EndDate); null for
                // Global/AlwaysActive scopes. ExpireOverridesAsync prunes rows
                // once this is now-or-past.
                ExpiresAt = scope.ExpiresAt,
            };
            _dbContext.StaffPermissions.Add(row);
            allOverrides.Add(row);
        }
    }

    /// <summary>
    /// Expands a DTO override's <see cref="PermissionOverrideModel.Actions"/> into the
    /// full set of action names to persist, via the resource manifest's implies graph.
    /// <b>Allow</b> uses the forward closure (granting a high verb grants the verbs it
    /// implies); <b>Deny</b> uses the reverse closure (denying a low verb denies every
    /// verb that would grant it transitively). Without the reverse direction, denying
    /// <c>EditClose</c> on a user who holds <c>Delete</c> would leave <c>Delete</c>
    /// intact — a silent fail-open.
    /// </summary>
    private HashSet<string> ResolveActionSet(PermissionOverrideModel perm, string module, string resourceKey)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (perm.Actions is null) return result;
        foreach (var action in perm.Actions)
        {
            if (string.IsNullOrWhiteSpace(action)) continue;
            var expanded = perm.Type == OverrideType.Deny
                ? _expander.ExpandDenyActionNames(module, resourceKey, action)
                : _expander.ExpandActionNames(module, resourceKey, action);
            result.UnionWith(expanded);
        }
        return result;
    }

    private async Task<HashSet<string>> ResolveActionSetForResourceAsync(PermissionOverrideModel perm, CancellationToken cancellationToken)
    {
        var resource = await _dbContext.Resources
            .Include(r => r.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == perm.ResourceId, cancellationToken);
        return resource is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : ResolveActionSet(perm, resource.Module.ModuleKey, resource.Key);
    }

    private async Task EmitRoleChangeAuditAsync(UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (_audit is null) return;
        if (request.RolesToAdd.Count == 0 && request.RolesToRemove.Count == 0) return;

        var added = request.RolesToAdd.Where(r => !request.RolesToRemove.Contains(r)).ToArray();
        var removed = request.RolesToRemove.Where(r => !request.RolesToAdd.Contains(r)).ToArray();
        if (added.Length == 0 && removed.Length == 0) return;

        await _audit.LogRoleAssignmentChangedAsync(request.UserId, added, removed, cancellationToken);
    }
}
