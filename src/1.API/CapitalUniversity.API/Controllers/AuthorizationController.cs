using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/authorization")]
[Authorize]
public class AuthorizationController : ControllerBase
{
    private readonly PermissionTreeQueryHandler _permissionTreeHandler;

    public AuthorizationController(PermissionTreeQueryHandler permissionTreeHandler)
    {
        _permissionTreeHandler = permissionTreeHandler;
    }

    /// <summary>
    /// Gets all permissions grouped by module and resource in a hierarchical tree structure.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of modules with their resources and permissions.</returns>
    [HttpGet("permissions/tree")]
    // [Authorize(Permissions.Authorization.ViewPermissions)] // Example of policy-based auth if constants exist
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
