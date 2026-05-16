using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;

public class PermissionTreeQueryHandler
{
    private readonly CoreDbContext _dbContext;

    private static readonly List<(ActionLevel Level, string Action, string Name, string Description)> PermissionActions = new()
    {
        (ActionLevel.View, "Read", "read", "Can view records"),
        (ActionLevel.Insert, "Create", "create", "Can add new records"),
        (ActionLevel.EditClose, "Update", "update", "Can edit existing records"),
        (ActionLevel.Open, "Open", "open", "Can open/unlock records"),
        (ActionLevel.Delete, "Delete", "delete", "Can remove records")
    };

    public PermissionTreeQueryHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ModulePermissionTreeDto>> Handle(GetPermissionTreeRequest request, CancellationToken cancellationToken)
    {
        var modules = await _dbContext.Modules
            .AsNoTracking()
            .Include(m => m.Services)
            .OrderBy(m => m.DisplayName)
            .Select(m => new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                ModuleName = m.DisplayName,
                Resources = m.Services
                    .OrderBy(s => s.DisplayName)
                    .Select(s => new ResourcePermissionTreeDto
                    {
                        ResourceId = s.Id,
                        ResourceName = s.DisplayName,
                        Permissions = PermissionActions.Select(pa => new PermissionActionDto
                        {
                            PermissionId = $"{s.Id}_{pa.Level}",
                            PermissionName = $"{m.ModuleKey}.{pa.Name}",
                            Action = pa.Action,
                            Description = pa.Description
                        }).ToList()
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        return modules;
    }

    public async Task<List<ModulePermissionTreeDto>?> Handle(GetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var roleExists = await _dbContext.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);
        if (!roleExists) return null;

        var rolePermissions = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == request.RoleId)
            .ToDictionaryAsync(rp => rp.ServiceId, rp => rp.Level, cancellationToken);

        var modules = await _dbContext.Modules
            .AsNoTracking()
            .Include(m => m.Services)
            .OrderBy(m => m.DisplayName)
            .Select(m => new ModulePermissionTreeDto
            {
                ModuleId = m.Id,
                ModuleName = m.DisplayName,
                Resources = m.Services
                    .OrderBy(s => s.DisplayName)
                    .Select(s => new ResourcePermissionTreeDto
                    {
                        ResourceId = s.Id,
                        ResourceName = s.DisplayName,
                        Permissions = PermissionActions.Select(pa => new PermissionActionDto
                        {
                            PermissionId = $"{s.Id}_{pa.Level}",
                            PermissionName = $"{m.ModuleKey}.{pa.Name}",
                            Action = pa.Action,
                            Description = pa.Description,
                            IsAssigned = rolePermissions.ContainsKey(s.Id) && rolePermissions[s.Id] >= pa.Level
                        }).ToList()
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        return modules;
    }
}
