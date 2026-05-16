using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

public class PermissionManagementService : IPermissionManagementService
{
    private readonly IPermissionService _permissionService;
    private readonly IRequestContext _requestContext;
    private readonly IScopeResolver _scopeResolver;
    private readonly CoreDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly ICurrentUser _currentUser;
    private readonly PermissionCacheOptions _cacheOptions;
    private readonly IPermissionCacheInvalidator? _cacheInvalidator;

    public PermissionManagementService(
        IPermissionService permissionService,
        IRequestContext requestContext,
        IScopeResolver scopeResolver,
        CoreDbContext dbContext,
        ICacheService cache,
        ICurrentUser currentUser,
        Microsoft.Extensions.Options.IOptions<PermissionCacheOptions>? cacheOptions = null,
        IPermissionCacheInvalidator? cacheInvalidator = null)
    {
        _permissionService = permissionService;
        _requestContext = requestContext;
        _scopeResolver = scopeResolver;
        _dbContext = dbContext;
        _cache = cache;
        _currentUser = currentUser;
        _cacheOptions = cacheOptions?.Value ?? new PermissionCacheOptions();
        _cacheInvalidator = cacheInvalidator;
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
        
        // Traverse up to find levels
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

        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();

        var rolePerms = await _dbContext.RolePermissions
            .Include(rp => rp.Service)
                .ThenInclude(s => s.Module)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var rolePermsByRole = rolePerms
            .GroupBy(rp => rp.RoleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var overrides = await _dbContext.StaffPermissions
            .Include(sp => sp.Service)
                .ThenInclude(s => s.Module)
            .Where(sp => sp.StaffId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var maxLevelByScopeResource = new Dictionary<ScopedResourceKey, ActionLevel>();

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
                var key = new ScopedResourceKey(scope, rp.Service.Module.ModuleKey, rp.Resource);
                if (!maxLevelByScopeResource.TryGetValue(key, out var existing) || rp.Level > existing)
                {
                    maxLevelByScopeResource[key] = rp.Level;
                }
            }
        }

        foreach (var ov in overrides.Where(o => o.Type == OverrideType.Allow))
        {
            var scope = new ScopeKey(ov.StructureNodeId, ov.StructureNodePath, ov.Year, ov.Semester);
            var key = new ScopedResourceKey(scope, ov.Service.Module.ModuleKey, ov.Resource);
            if (!maxLevelByScopeResource.TryGetValue(key, out var existing) || ov.Level > existing)
            {
                maxLevelByScopeResource[key] = ov.Level;
            }
        }

        foreach (var ov in overrides.Where(o => o.Type == OverrideType.Deny))
        {
            var scope = new ScopeKey(ov.StructureNodeId, ov.StructureNodePath, ov.Year, ov.Semester);
            var key = new ScopedResourceKey(scope, ov.Service.Module.ModuleKey, ov.Resource);
            if (maxLevelByScopeResource.TryGetValue(key, out var existing) && existing >= ov.Level)
            {
                maxLevelByScopeResource[key] = (ActionLevel)((int)ov.Level - 1);
            }
        }

        var dtos = new List<PermissionDto>();
        foreach (var (key, level) in maxLevelByScopeResource)
        {
            if (level <= ActionLevel.None) continue;
            var scopeDto = BuildScopeDto(key.Scope);
            foreach (var action in GetActionsUpToLevel(level))
            {
                dtos.Add(new PermissionDto
                {
                    Module = key.Module,
                    Resource = key.Resource,
                    Action = action,
                    Scope = scopeDto
                });
            }
        }

        return dtos;
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

        var rawPermissions = await _permissionService.GetPermissionsAsync(userId, "*", scope, cancellationToken);

        var roleIds = rawPermissions.Assignments.Select(a => a.RoleId).Distinct().ToList();
        var rolePermsDb = await _dbContext.RolePermissions
            .Include(rp => rp.Service)
                .ThenInclude(s => s.Module)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var maxGrantedLevelPerResource = new Dictionary<string, (ActionLevel Level, string ModuleKey, string Resource)>();

        foreach (var rp in rolePermsDb)
        {
            var key = PermissionIdentity.Create(rp.Service.Module.ModuleKey, rp.Resource, "");
            if (!maxGrantedLevelPerResource.ContainsKey(key) || maxGrantedLevelPerResource[key].Level < rp.Level)
            {
                maxGrantedLevelPerResource[key] = (rp.Level, rp.Service.Module.ModuleKey, rp.Resource);
            }
        }

        var overridesDb = await _dbContext.StaffPermissions
            .Include(sp => sp.Service)
                .ThenInclude(s => s.Module)
            .Where(sp => rawPermissions.Overrides.Select(o => o.Id).Contains(sp.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allowOverrides = overridesDb.Where(o => o.Type == OverrideType.Allow);
        foreach (var ov in allowOverrides)
        {
            var key = PermissionIdentity.Create(ov.Service.Module.ModuleKey, ov.Resource, "");
            if (!maxGrantedLevelPerResource.ContainsKey(key) || maxGrantedLevelPerResource[key].Level < ov.Level)
            {
                maxGrantedLevelPerResource[key] = (ov.Level, ov.Service.Module.ModuleKey, ov.Resource);
            }
        }

        var denyOverrides = overridesDb.Where(o => o.Type == OverrideType.Deny);
        foreach (var deny in denyOverrides)
        {
            var key = PermissionIdentity.Create(deny.Service.Module.ModuleKey, deny.Resource, "");
            if (maxGrantedLevelPerResource.ContainsKey(key) && maxGrantedLevelPerResource[key].Level >= deny.Level)
            {
                var newMaxLevel = (ActionLevel)((int)deny.Level - 1);
                maxGrantedLevelPerResource[key] = (newMaxLevel, maxGrantedLevelPerResource[key].ModuleKey, maxGrantedLevelPerResource[key].Resource);
            }
        }

        var lookup = new HashSet<string>();
        foreach (var kvp in maxGrantedLevelPerResource)
        {
            if (kvp.Value.Level > ActionLevel.None)
            {
                var actions = GetActionsUpToLevel(kvp.Value.Level);
                foreach (var action in actions)
                {
                    // Canonical Composite Key
                    lookup.Add(PermissionIdentity.Create(kvp.Value.ModuleKey, kvp.Value.Resource, action));
                }
            }
        }

        await _cache.SetAsync(cacheKey, lookup, TimeSpan.FromMinutes(_cacheOptions.LookupTtlMinutes), cancellationToken);

        return lookup;
    }

    // Rotating the per-user version orphans every cached perm_lookup_{userId}_{version}_*
    // entry across all scope variants in a single write — no key enumeration needed.
    // Delegates to IPermissionCacheInvalidator when it's wired so all rotation paths
    // share one implementation; falls back to a direct cache write for tests/old
    // construction paths that bypass DI.
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

    private IEnumerable<string> GetActionsUpToLevel(ActionLevel level)
    {
        var actions = new List<string>();
        if (level >= ActionLevel.View) actions.Add("View");
        if (level >= ActionLevel.Insert) actions.Add("Insert");
        if (level >= ActionLevel.EditClose) actions.Add("EditClose");
        if (level >= ActionLevel.Open) actions.Add("Open");
        if (level >= ActionLevel.Delete) actions.Add("Delete");
        return actions;
    }

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
            .Where(sp => sp.StaffId == query.UserId && 
                         sp.StructureNodeId == query.StructureNodeId && 
                         sp.Year == year && sp.Semester == semester)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (!roles.Any() && !overrides.Any())
        {
            return null;
        }

        return new PermissionAssignmentResponse
        {
            UserId = query.UserId,
            RoleIds = roles.Select(r => r.RoleId).ToList(),
            PermissionOverrides = overrides.Select(o => new PermissionOverrideModel
            {
                ServiceId = o.ServiceId,
                Resource = o.Resource,
                Level = o.Level,
                Type = o.Type
            }).ToList(),
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

    private void ValidateScopeCombinations(StructuralScopeModel structural, TemporalScopeModel temporal)
    {
        if (temporal.AlwaysActive && (temporal.AcademicYearId.HasValue || temporal.SemesterId.HasValue))
            throw new ArgumentException("Cannot specify both AlwaysActive=true and specific Temporal limits");
    }

    public async Task<PermissionAssignmentResponse> CreateAssignmentAsync(CreatePermissionAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScopeCombinations(request.StructuralScope, request.TemporalScope);

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

        foreach (var roleId in request.RoleIds)
        {
            if (!existingRoles.Contains(roleId))
            {
                var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };
                _dbContext.StaffRoles.Add(roleAssignment);
            }
        }

        var existingOverrides = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == request.UserId && 
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId && 
                         sp.Year == year && sp.Semester == semester)
            .ToListAsync(cancellationToken);

        foreach (var perm in request.PermissionOverrides)
        {
            if (!existingOverrides.Any(eo => eo.ServiceId == perm.ServiceId && eo.Resource == perm.Resource && eo.Type == perm.Type))
            {
                var spOverride = new StaffPermissionOverride(request.UserId, perm.ServiceId, perm.Resource, perm.Level, perm.Type, ScopeKeys.Global, year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };

                _dbContext.StaffPermissions.Add(spOverride);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        await InvalidateUserCacheAsync(request.UserId, cancellationToken);

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
        ValidateScopeCombinations(request.StructuralScope, request.TemporalScope);

        var year = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.AcademicYearId?.ToString() ?? ScopeKeys.Global);
        var semester = request.TemporalScope.AlwaysActive ? ScopeKeys.Global : (request.TemporalScope.SemesterId?.ToString() ?? ScopeKeys.Global);

        string? nodePath = null;
        if (request.StructuralScope.StructureNodeId.HasValue)
        {
            var node = await _dbContext.StructureNodes.FindAsync(new object[] { request.StructuralScope.StructureNodeId.Value }, cancellationToken);
            nodePath = node?.Path;
        }

        // 1. Manage Roles
        var currentRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId && 
                         sr.StructureNodeId == request.StructuralScope.StructureNodeId && 
                         sr.Year == year && sr.Semester == semester)
            .ToListAsync(cancellationToken);

        var roleIdsToRemove = request.RolesToRemove.Where(r => !request.RolesToAdd.Contains(r)).ToList();
        var rolesToRemoveEntities = currentRoles.Where(cr => roleIdsToRemove.Contains(cr.RoleId)).ToList();
        _dbContext.StaffRoles.RemoveRange(rolesToRemoveEntities);

        foreach (var roleId in request.RolesToAdd)
        {
            if (!currentRoles.Any(cr => cr.RoleId == roleId))
            {
                var roleAssignment = new StaffRoleAssignment(request.UserId, roleId, year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };
                _dbContext.StaffRoles.Add(roleAssignment);
            }
        }

        // 2. Manage Overrides
        var currentOverrides = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == request.UserId && 
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId && 
                         sp.Year == year && sp.Semester == semester)
            .ToListAsync(cancellationToken);

        foreach (var permToRemove in request.PermissionsToRemove)
        {
            var entityToRemove = currentOverrides.FirstOrDefault(eo =>
                eo.ServiceId == permToRemove.ServiceId &&
                eo.Resource == permToRemove.Resource &&
                eo.Type == permToRemove.Type);

            if (entityToRemove != null)
            {
                _dbContext.StaffPermissions.Remove(entityToRemove);
            }
        }

        foreach (var permToAdd in request.PermissionsToAdd)
        {
            var existing = currentOverrides.FirstOrDefault(eo =>
                eo.ServiceId == permToAdd.ServiceId &&
                eo.Resource == permToAdd.Resource &&
                eo.Type == permToAdd.Type);

            if (existing != null)
            {
                existing.UpdateLevel(permToAdd.Level);
            }
            else
            {
                var spOverride = new StaffPermissionOverride(request.UserId, permToAdd.ServiceId, permToAdd.Resource, permToAdd.Level, permToAdd.Type, ScopeKeys.Global, year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };

                _dbContext.StaffPermissions.Add(spOverride);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        await InvalidateUserCacheAsync(request.UserId, cancellationToken);

        var finalRoles = await _dbContext.StaffRoles
            .Where(sr => sr.StaffId == request.UserId && 
                         sr.StructureNodeId == request.StructuralScope.StructureNodeId && 
                         sr.Year == year && sr.Semester == semester)
            .Select(sr => sr.RoleId)
            .ToListAsync(cancellationToken);

        var finalOverrides = await _dbContext.StaffPermissions
            .Where(sp => sp.StaffId == request.UserId && 
                         sp.StructureNodeId == request.StructuralScope.StructureNodeId && 
                         sp.Year == year && sp.Semester == semester)
            .Select(o => new PermissionOverrideModel
            {
                ServiceId = o.ServiceId,
                Resource = o.Resource,
                Level = o.Level,
                Type = o.Type
            }).ToListAsync(cancellationToken);

        return new PermissionAssignmentResponse
        {
            UserId = request.UserId,
            RoleIds = finalRoles,
            PermissionOverrides = finalOverrides,
            StructuralScope = request.StructuralScope,
            TemporalScope = request.TemporalScope
        };
    }
}

