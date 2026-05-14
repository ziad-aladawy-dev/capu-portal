using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

public class PermissionManagementService : IPermissionManagementService
{
    private readonly IPermissionService _permissionService;
    private readonly IRequestContext _requestContext;
    private readonly IScopeResolver _scopeResolver;
    private readonly CoreDbContext _dbContext;
    private readonly ICacheService _cache;

    public PermissionManagementService(
        IPermissionService permissionService,
        IRequestContext requestContext,
        IScopeResolver scopeResolver,
        CoreDbContext dbContext,
        ICacheService cache)
    {
        _permissionService = permissionService;
        _requestContext = requestContext;
        _scopeResolver = scopeResolver;
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<List<PermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var lookup = await GetPermissionLookupAsync(userId, cancellationToken);
        
        return lookup.Select(key => 
        {
            var parts = key.Split(':');
            return new PermissionDto
            {
                Module = parts[0],
                Resource = parts[1],
                Action = parts[2]
            };
        }).ToList();
    }

    public async Task<HashSet<string>> GetPermissionLookupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var year = _requestContext.ActiveAcademicYearId?.ToString() ?? "Global";
        var semester = _requestContext.ActiveSemesterId?.ToString() ?? "Global";
        var cacheKey = $"perm_lookup_{userId}_{year}_{semester}";

        var cachedLookup = await _cache.GetAsync<HashSet<string>>(cacheKey, cancellationToken);
        if (cachedLookup != null)
        {
            return cachedLookup;
        }

        var domain = "Global";
        var scope = await _scopeResolver.ResolveAsync(domain, year, semester, cancellationToken);
        var rawPermissions = await _permissionService.GetPermissionsAsync(userId, "*", scope, cancellationToken);

        var roleIds = rawPermissions.Assignments.Select(a => a.RoleId).Distinct().ToList();
        var rolePermsDb = await _dbContext.RolePermissions
            .Include(rp => rp.Service)
                .ThenInclude(s => s.Module)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var maxGrantedLevelPerResource = new Dictionary<string, (ActionLevel Level, string ModuleKey)>();

        foreach (var rp in rolePermsDb)
        {
            if (!maxGrantedLevelPerResource.ContainsKey(rp.Resource) || maxGrantedLevelPerResource[rp.Resource].Level < rp.Level)
            {
                maxGrantedLevelPerResource[rp.Resource] = (rp.Level, rp.Service.Module.ModuleKey);
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
            if (!maxGrantedLevelPerResource.ContainsKey(ov.Resource) || maxGrantedLevelPerResource[ov.Resource].Level < ov.Level)
            {
                maxGrantedLevelPerResource[ov.Resource] = (ov.Level, ov.Service.Module.ModuleKey);
            }
        }

        var denyOverrides = overridesDb.Where(o => o.Type == OverrideType.Deny);
        foreach (var deny in denyOverrides)
        {
            if (maxGrantedLevelPerResource.ContainsKey(deny.Resource) && maxGrantedLevelPerResource[deny.Resource].Level >= deny.Level)
            {
                var newMaxLevel = (ActionLevel)((int)deny.Level - 1);
                maxGrantedLevelPerResource[deny.Resource] = (newMaxLevel, maxGrantedLevelPerResource[deny.Resource].ModuleKey);
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
                    // Composite key: Module:Resource:Action
                    lookup.Add($"{kvp.Value.ModuleKey}:{kvp.Key}:{action}");
                }
            }
        }

        await _cache.SetAsync(cacheKey, lookup, TimeSpan.FromMinutes(20), cancellationToken);

        return lookup;
    }

    private async Task InvalidateUserCacheAsync(Guid userId, string year, string semester)
    {
        var cacheKey = $"perm_lookup_{userId}_{year}_{semester}";
        await _cache.RemoveAsync(cacheKey);
    }

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
        var year = query.AlwaysActive ? "Global" : (query.AcademicYearId?.ToString() ?? "Global");
        var semester = query.AlwaysActive ? "Global" : (query.SemesterId?.ToString() ?? "Global");

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

        var year = request.TemporalScope.AlwaysActive ? "Global" : (request.TemporalScope.AcademicYearId?.ToString() ?? "Global");
        var semester = request.TemporalScope.AlwaysActive ? "Global" : (request.TemporalScope.SemesterId?.ToString() ?? "Global");

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
                var spOverride = new StaffPermissionOverride(request.UserId, perm.ServiceId, perm.Resource, perm.Level, perm.Type, "Global", year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };

                _dbContext.StaffPermissions.Add(spOverride);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        await InvalidateUserCacheAsync(request.UserId, year, semester);

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

        var year = request.TemporalScope.AlwaysActive ? "Global" : (request.TemporalScope.AcademicYearId?.ToString() ?? "Global");
        var semester = request.TemporalScope.AlwaysActive ? "Global" : (request.TemporalScope.SemesterId?.ToString() ?? "Global");

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
                var spOverride = new StaffPermissionOverride(request.UserId, permToAdd.ServiceId, permToAdd.Resource, permToAdd.Level, permToAdd.Type, "Global", year, semester)
                {
                    StructureNodeId = request.StructuralScope.StructureNodeId,
                    StructureNodePath = nodePath
                };

                _dbContext.StaffPermissions.Add(spOverride);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        await InvalidateUserCacheAsync(request.UserId, year, semester);

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

