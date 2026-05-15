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

    public PermissionManagementService(
        IPermissionService permissionService,
        IRequestContext requestContext,
        IScopeResolver scopeResolver,
        CoreDbContext dbContext,
        ICacheService cache,
        ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _requestContext = requestContext;
        _scopeResolver = scopeResolver;
        _dbContext = dbContext;
        _cache = cache;
        _currentUser = currentUser;
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

        if (user.Role == "Student")
        {
            // Student Model: Contextual Scoping
            response.ActiveScope.Structural.NodeId = user.StructureNodeId;

            response.AuthorizedScopes.AllowedNodeIds = user.StructureNodeId.HasValue ? new List<Guid> { user.StructureNodeId.Value } : new List<Guid>();
            response.AuthorizedScopes.AllowedAcademicYearIds = currentYear != null ? new List<Guid> { currentYear.Id } : new List<Guid>();
            response.AuthorizedScopes.AllowedSemesterIds = currentSem != null ? new List<Guid> { currentSem.Id } : new List<Guid>();
            
            // Students have empty permissions (context-based)
            response.Permissions = new List<PermissionDto>();
        }
        else
        {
            // Admin Model: Permission-Based Scoping
            response.ActiveScope.Structural.NodeId = user.StructureNodeId; // Default to assigned node if any

            // Populate Permissions
            response.Permissions = await GetEffectivePermissionsAsync(user.Id, cancellationToken);

            // Populate Authorized Scopes from Assignments
            var assignments = await _dbContext.StaffRoles
                .Where(sr => sr.StaffId == user.Id)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            response.AuthorizedScopes.IsGlobalStructural = assignments.Any(a => a.StructureNodeId == null);
            response.AuthorizedScopes.AllowedNodeIds = assignments
                .Where(a => a.StructureNodeId.HasValue)
                .Select(a => a.StructureNodeId!.Value)
                .Distinct()
                .ToList();

            response.AuthorizedScopes.IsGlobalYear = assignments.Any(a => a.Year == "Global");
            response.AuthorizedScopes.AllowedAcademicYearIds = assignments
                .Where(a => a.Year != "Global")
                .Select(a => Guid.TryParse(a.Year, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            response.AuthorizedScopes.IsGlobalSemester = assignments.Any(a => a.Semester == "Global");
            response.AuthorizedScopes.AllowedSemesterIds = assignments
                .Where(a => a.Semester != "Global")
                .Select(a => Guid.TryParse(a.Semester, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
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
        var lookup = await GetPermissionLookupAsync(userId, cancellationToken);
        
        var dtos = new List<PermissionDto>();
        foreach (var key in lookup)
        {
            if (PermissionIdentity.TryParse(key, out var module, out var resource, out var action))
            {
                dtos.Add(new PermissionDto
                {
                    Module = module,
                    Resource = resource,
                    Action = action
                });
            }
        }
        return dtos;
    }

    public async Task<HashSet<string>> GetPermissionLookupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var year = _requestContext.ActiveAcademicYearId?.ToString() ?? "Global";
        var semester = _requestContext.ActiveSemesterId?.ToString() ?? "Global";
        var cacheKey = $"perm_lookup_{userId}_{year}_{semester}";

        var cachedLookup = await _cache.GetAsync<HashSet<string>>(cacheKey, cancellationToken);
        if (cachedLookup != null)
        {
            return new HashSet<string>(cachedLookup);
        }

        var scope = await _scopeResolver.ResolveAsync(userId, year, semester, cancellationToken);

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

