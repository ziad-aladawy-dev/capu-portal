using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;

public class GetPermissionTreeRequest
{
}

public class GetRolePermissionsRequest
{
    public Guid RoleId { get; set; }
}
