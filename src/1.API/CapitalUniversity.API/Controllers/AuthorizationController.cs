using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/authorization")]
public class AuthorizationController : ControllerBase
{
    private readonly IPermissionTreeQueryHandler _permissionTreeHandler;
    private readonly SetRolePermissionsCommandHandler _setRolePermissionsHandler;

    public AuthorizationController(
        IPermissionTreeQueryHandler permissionTreeHandler,
        SetRolePermissionsCommandHandler setRolePermissionsHandler)
    {
        _permissionTreeHandler = permissionTreeHandler;
        _setRolePermissionsHandler = setRolePermissionsHandler;
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
        return Ok(result);
    }

    /// <summary>
    /// Replaces a role's permission set (full-replace) with the supplied per-action
    /// grants. Each action is expanded through the resource manifest's implies graph.
    /// </summary>
    [HttpPut("roles/{roleId:guid}/permissions")]
    [HasPermission(PermissionNames.Roles.EditClose)]
    public async Task<ActionResult<SetRolePermissionsResponse>> SetRolePermissions(
        Guid roleId, [FromBody] SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest();
        request.RoleId = roleId;
        var result = await _setRolePermissionsHandler.Handle(request, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Gets all permissions grouped by module and resource, marking those assigned to a specific user along with their scopes.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of modules with their resources, permissions, assignment status, and scope metadata.</returns>
    [HttpGet("users/{userId:guid}/permission-tree")]
    [HasPermission(PermissionNames.Permissions.EditClose)]
    public async Task<ActionResult<List<ModulePermissionTreeDto>>> GetPermissionTreeForUser(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _permissionTreeHandler.Handle(new GetUserPermissionTreeRequest { UserId = userId }, cancellationToken);
        return Ok(result);
    }
}
