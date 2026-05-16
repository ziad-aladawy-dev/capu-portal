using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;

public interface IPermissionTreeQueryHandler
{
    Task<List<ModulePermissionTreeDto>> Handle(GetPermissionTreeRequest request, CancellationToken cancellationToken);

    Task<List<ModulePermissionTreeDto>?> Handle(GetRolePermissionsRequest request, CancellationToken cancellationToken);
}
