using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
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

    public PermissionManagementService(
        IPermissionService permissionService,
        IRequestContext requestContext,
        IScopeResolver scopeResolver,
        CoreDbContext dbContext,
        PermissionCacheCoordinator cacheCoordinator,
        ManifestActionExpander expander,
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
    }

    public async Task<LoginResponseDto> GetBootstrapContextAsync(IUserCredential user, CancellationToken cancellationToken = default)
    {
        var response = new LoginResponseDto
        {
            User = new UserInfoDto
            {
                Id = user.Id,
                Name = user.Name,
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
                    attributes.Uni = currentNode.Name;
                    break;
                case StructureNodeType.Faculty:
                    attributes.Faculty = currentNode.Name;
                    break;
                case StructureNodeType.Department:
                case StructureNodeType.Program:
                    attributes.Department = currentNode.Name;
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
        // Bootstrap view: return every grant the user holds, each with its own scope attached.
        // Do NOT filter through IRequestContext here — that intersection belongs to the runtime
        // authorization check (GetPermissionLookupAsync), not to enumerating what the user has.
        var assignments = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var rolePermsByRole = await LoadRolePermissionsByRoleAsync(assignments, cancellationToken);
        var overrides = await LoadOverridesAsync(userId, cancellationToken);

        // Set-based evaluation: effective = allow − deny. Storage is already per-action,
        // so no implies expansion at evaluation time — implies are folded in at write time.
        var allowByKey = new Dictionary<ScopedResourceKey, HashSet<string>>();
        var denyByKey = new Dictionary<ScopedResourceKey, HashSet<string>>();

        CollectRoleAllows(assignments, rolePermsByRole, allowByKey);
        CollectOverrideActions(overrides.Where(o => o.Type == OverrideType.Allow), allowByKey);
        CollectOverrideActions(overrides.Where(o => o.Type == OverrideType.Deny), denyByKey);

        return BuildEffectivePermissionDtos(allowByKey, denyByKey);
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

    private Task<List<StaffPermissionOverride>> LoadOverridesAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.StaffPermissions
            .Include(sp => sp.Resource)
                .ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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

        var cachedLookup = await _cache.GetAsync<HashSet<string>>(cacheKey, cancellationToken);
        if (cachedLookup != null)
        {
            return new HashSet<string>(cachedLookup);
        }

        var scope = await _scopeResolver.ResolveAsync(userId, year, semester, structureNodeId, cancellationToken);

        var rawPermissions = await _permissionService.GetAllPermissionsAsync(userId, scope, cancellationToken);

        var roleIds = rawPermissions.Assignments.Select(a => a.RoleId).Distinct().ToList();
        var rolePermsDb = await _dbContext.RolePermissions
            .Include(rp => rp.Resource)
                .ThenInclude(r => r.Module)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var denied = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rp in rolePermsDb)
        {
            allowed.Add(PermissionIdentity.Create(rp.Resource.Module.ModuleKey, rp.Resource.Key, rp.Action));
        }

        var overridesDb = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource)
                .ThenInclude(r => r.Module)
            .Where(sp => rawPermissions.Overrides.Select(o => o.Id).Contains(sp.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var ov in overridesDb)
        {
            var canonical = PermissionIdentity.Create(ov.Resource.Module.ModuleKey, ov.Resource.Key, ov.Action);
            if (ov.Type == OverrideType.Allow) allowed.Add(canonical);
            else denied.Add(canonical);
        }

        allowed.ExceptWith(denied);

        await _cache.SetAsync(cacheKey, allowed, TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes), cancellationToken);

        return allowed;
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
        var year = query.AlwaysActive ? ScopeKeys.Global : (query.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = query.AlwaysActive ? ScopeKeys.Global : (query.SemesterId?.ToString() ?? ScopeKeys.Global);

        var roles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == query.UserId &&
                         sr.StructureNodeId == query.StructureNodeId &&
                         sr.Year == year && sr.Semester == semester)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var overrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource)
                .ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == query.UserId &&
                         sp.StructureNodeId == query.StructureNodeId &&
                         sp.Year == year && sp.Semester == semester)
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
            PermissionOverrides = CollapseOverridesToDtos(overrides),
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
    /// Collapses per-action override rows into legacy DTOs. Each (ResourceId, Type)
    /// group yields one <see cref="PermissionOverrideModel"/> whose
    /// <see cref="PermissionOverrideModel.Level"/> is the highest legacy ladder
    /// step covered by the row's action set, and whose
    /// <see cref="PermissionOverrideModel.Actions"/> mirrors the underlying set.
    /// </summary>
    private List<PermissionOverrideModel> CollapseOverridesToDtos(IEnumerable<StaffPermissionOverride> rows)
    {
        return rows
            .GroupBy(o => new { o.ResourceId, o.Type, Module = o.Resource.Module.ModuleKey, ResourceKey = o.Resource.Key })
            .Select(g =>
            {
                var actions = g.Select(o => o.Action).ToList();
                return new PermissionOverrideModel
                {
                    ResourceId = g.Key.ResourceId,
                    Type = g.Key.Type,
                    Level = _expander.CollapseToMaxLevel(g.Key.Module, g.Key.ResourceKey, actions),
                    Actions = actions,
                };
            })
            .ToList();
    }

    private static void ValidateScopeCombinations(TemporalScopeModel temporal)
    {
        if (temporal.AlwaysActive && (temporal.AcademicYearId.HasValue || temporal.SemesterId.HasValue))
            throw new ArgumentException("Cannot specify both AlwaysActive=true and specific Temporal limits");
    }

    public async Task<PermissionAssignmentResponse> CreateAssignmentAsync(CreatePermissionAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScopeCombinations(request.TemporalScope);

        var year = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.SemesterId?.ToString() ?? ScopeKeys.Global);

        string? nodePath = null;
        if (request.StructuralScope.StructureNodeId.HasValue)
        {
            var node = await _dbContext.StructureNodes.FindAsync(new object[] { request.StructuralScope.StructureNodeId.Value }, cancellationToken);
            nodePath = node?.Path;
        }

        var existingRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sr.Year == year && sr.Semester == semester)
            .Select(sr => sr.RoleId)
            .ToListAsync(cancellationToken);

        foreach (var roleId in request.RoleIds.Where(rid => !existingRoles.Contains(rid)))
        {
            var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, year, semester)
            {
                StructureNodeId = request.StructuralScope.StructureNodeId,
                StructureNodePath = nodePath
            };
            _dbContext.StaffRoles.Add(roleAssignment);
        }

        var existingOverrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId &&
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sp.Year == year && sp.Semester == semester)
            .ToListAsync(cancellationToken);

        foreach (var perm in request.PermissionOverrides)
        {
            await PersistOverrideAsync(request.UserId, perm, existingOverrides, year, semester,
                request.StructuralScope.StructureNodeId, nodePath, cancellationToken);
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
        ValidateScopeCombinations(request.TemporalScope);

        var year = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.SemesterId?.ToString() ?? ScopeKeys.Global);
        var nodePath = await ResolveNodePathAsync(request.StructuralScope.StructureNodeId, cancellationToken);

        await ApplyRoleUpdatesAsync(request, year, semester, nodePath, cancellationToken);
        await ApplyOverrideUpdatesAsync(request, year, semester, nodePath, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateUserCacheAsync(request.UserId, cancellationToken);
        await EmitRoleChangeAuditAsync(request, cancellationToken);

        var finalRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sr.Year == year && sr.Semester == semester)
            .Select(sr => sr.RoleId)
            .ToListAsync(cancellationToken);

        var finalOverrideRows = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId &&
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sp.Year == year && sp.Semester == semester)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PermissionAssignmentResponse
        {
            UserId = request.UserId,
            RoleIds = finalRoles,
            PermissionOverrides = CollapseOverridesToDtos(finalOverrideRows),
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
        UpdatePermissionAssignmentRequest request, string year, string semester, string? nodePath, CancellationToken cancellationToken)
    {
        var currentRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId &&
                         sr.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sr.Year == year && sr.Semester == semester)
            .ToListAsync(cancellationToken);

        var roleIdsToRemove = request.RolesToRemove.Where(r => !request.RolesToAdd.Contains(r)).ToList();
        var rolesToRemoveEntities = currentRoles.Where(cr => roleIdsToRemove.Contains(cr.RoleId)).ToList();
        _dbContext.StaffRoles.RemoveRange(rolesToRemoveEntities);

        foreach (var roleId in request.RolesToAdd.Where(rid => currentRoles.All(cr => cr.RoleId != rid)))
        {
            var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, year, semester)
            {
                StructureNodeId = request.StructuralScope.StructureNodeId,
                StructureNodePath = nodePath
            };
            _dbContext.StaffRoles.Add(roleAssignment);
        }
    }

    private async Task ApplyOverrideUpdatesAsync(
        UpdatePermissionAssignmentRequest request, string year, string semester, string? nodePath, CancellationToken cancellationToken)
    {
        var currentOverrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Resource).ThenInclude(r => r.Module)
            .Where(sp => sp.StaffId == request.UserId &&
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId &&
                         sp.Year == year && sp.Semester == semester)
            .ToListAsync(cancellationToken);

        foreach (var permToRemove in request.PermissionsToRemove)
        {
            var actionsToRemove = ResolveActionSetForDto(permToRemove, currentOverrides);
            var entitiesToRemove = currentOverrides
                .Where(eo => eo.ResourceId == permToRemove.ResourceId && eo.Type == permToRemove.Type
                             && actionsToRemove.Contains(eo.Action))
                .ToList();

            foreach (var entity in entitiesToRemove)
            {
                _dbContext.StaffPermissions.Remove(entity);
                currentOverrides.Remove(entity);
            }
        }

        foreach (var permToAdd in request.PermissionsToAdd)
        {
            await PersistOverrideAsync(request.UserId, permToAdd, currentOverrides, year, semester,
                request.StructuralScope.StructureNodeId, nodePath, cancellationToken);
        }
    }

    private async Task PersistOverrideAsync(
        Guid userId,
        PermissionOverrideModel perm,
        List<StaffPermissionOverride> existingForScope,
        string year,
        string semester,
        Guid? structureNodeId,
        string? structureNodePath,
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
            var existing = existingForScope
                .FirstOrDefault(eo => eo.ResourceId == perm.ResourceId && eo.Type == perm.Type && eo.Action == action);
            if (existing is not null) continue;

            var row = new StaffPermissionOverride(userId, perm.ResourceId, action, perm.Type, year, semester)
            {
                StructureNodeId = structureNodeId,
                StructureNodePath = structureNodePath,
            };
            _dbContext.StaffPermissions.Add(row);
            existingForScope.Add(row);
        }
    }

    /// <summary>
    /// Determines which action names a DTO write represents. Prefers explicit
    /// <see cref="PermissionOverrideModel.Actions"/> when populated; otherwise
    /// expands the legacy <see cref="PermissionOverrideModel.Level"/> through
    /// the resource manifest.
    /// <para>The expansion direction depends on <paramref name="overrideType"/>:
    /// <b>Allow</b> uses the forward implies closure (granting a high verb
    /// grants the verbs below); <b>Deny</b> uses the reverse implies closure
    /// (denying a low verb denies every verb that would grant it transitively).
    /// Without the reverse direction, denying <c>EditClose</c> on a user who
    /// holds <c>Delete</c> would leave <c>Delete</c> intact — a silent
    /// fail-open. The asymmetry restores the legacy ladder's deny semantic
    /// under the manifest-driven graph.</para>
    /// </summary>
    private HashSet<string> ResolveActionSet(PermissionOverrideModel perm, string module, string resourceKey)
    {
        if (perm.Actions is { Count: > 0 })
        {
            return new HashSet<string>(perm.Actions, StringComparer.Ordinal);
        }
        var expanded = perm.Type == OverrideType.Deny
            ? _expander.ExpandDenyActions(module, resourceKey, perm.Level)
            : _expander.ExpandActions(module, resourceKey, perm.Level);
        return new HashSet<string>(expanded, StringComparer.Ordinal);
    }

    private HashSet<string> ResolveActionSetForDto(PermissionOverrideModel perm, IEnumerable<StaffPermissionOverride> existing)
    {
        if (perm.Actions is { Count: > 0 })
        {
            return new HashSet<string>(perm.Actions, StringComparer.Ordinal);
        }
        // Best effort: resolve from one of the existing rows whose resource matches.
        var sample = existing.FirstOrDefault(o => o.ResourceId == perm.ResourceId);
        if (sample is null) return new HashSet<string>(StringComparer.Ordinal);
        var expanded = perm.Type == OverrideType.Deny
            ? _expander.ExpandDenyActions(sample.Resource.Module.ModuleKey, sample.Resource.Key, perm.Level)
            : _expander.ExpandActions(sample.Resource.Module.ModuleKey, sample.Resource.Key, perm.Level);
        return new HashSet<string>(expanded, StringComparer.Ordinal);
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
