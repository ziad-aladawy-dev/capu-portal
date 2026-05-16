using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionManagementService _permissionService;
    private readonly ICurrentUser _currentUser;

    public PermissionsController(IPermissionManagementService permissionService, ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("permissions.permissions.View")]
    public async Task<ActionResult<List<PermissionDto>>> GetEffectivePermissions(CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetEffectivePermissionsAsync(_currentUser.Id, cancellationToken);
        return Ok(permissions);
    }

    [HttpGet("assignment")]
    [HasPermission("permissions.permissions.View")]
    public async Task<ActionResult<PermissionAssignmentResponse>> GetAssignment([FromQuery] GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.GetAssignmentAsync(query, cancellationToken);
        if (assignment == null)
            return NotFound();

        return Ok(assignment);
    }

    [HttpPost]
    [HasPermission("permissions.permissions.Insert")]
    public async Task<ActionResult<PermissionAssignmentResponse>> CreateAssignment([FromBody] CreatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.CreateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }

    [HttpPut("assignment")]
    [HasPermission("permissions.permissions.EditClose")]
    public async Task<ActionResult<PermissionAssignmentResponse>> UpdateAssignment([FromBody] UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.UpdateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }
}
