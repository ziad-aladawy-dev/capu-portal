using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/authorization")]
public class AuthorizationController : ControllerBase
{
    private readonly IPermissionTreeQueryHandler _permissionTreeHandler;

    public AuthorizationController(IPermissionTreeQueryHandler permissionTreeHandler)
    {
        _permissionTreeHandler = permissionTreeHandler;
    }

    /// <summary>
    /// Gets all permissions grouped by module and resource in a hierarchical tree structure.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of modules with their resources and permissions.</returns>
    [HttpGet("permissions/tree")]
    [HasPermission(PermissionNames.Permissions.View)]
    public async Task<ActionResult<List<ModulePermissionTreeDto>>> GetPermissionTree(CancellationToken cancellationToken)
    {
        var result = await _permissionTreeHandler.Handle(new GetPermissionTreeRequest(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets all permissions grouped by module and resource, marking those assigned to a specific role.
    /// </summary>
    /// <param name="roleId">The ID of the role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of modules with their resources and permissions, including assignment status.</returns>
    [HttpGet("roles/{roleId:guid}/permissions")]
    [HasPermission(PermissionNames.Roles.View)]
    public async Task<ActionResult<List<ModulePermissionTreeDto>>> GetRolePermissions(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await _permissionTreeHandler.Handle(new GetRolePermissionsRequest { RoleId = roleId }, cancellationToken);

        if (result == null)
        {
            return NotFound(new { Message = $"Role with ID {roleId} not found." });
        }

        return Ok(result);
    }
}
