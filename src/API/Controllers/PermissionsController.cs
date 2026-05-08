using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Auth.Authorization.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/permissions")]
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
    public async Task<ActionResult<List<PermissionDto>>> GetEffectivePermissions(CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetEffectivePermissionsAsync(_currentUser.Id, cancellationToken);
        return Ok(permissions);
    }

    [HttpGet("assignment")]
    public async Task<ActionResult<PermissionAssignmentResponse>> GetAssignment([FromQuery] GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.GetAssignmentAsync(query, cancellationToken);
        if (assignment == null)
            return NotFound();

        return Ok(assignment);
    }

    [HttpPost]
    public async Task<ActionResult<PermissionAssignmentResponse>> CreateAssignment([FromBody] CreatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.CreateAssignmentAsync(request, cancellationToken);
        // We return the assignment itself. The user can fetch it via GetAssignment using scoped query params.
        return Ok(assignment);
    }

    [HttpPut("assignment")]
    public async Task<ActionResult<PermissionAssignmentResponse>> UpdateAssignment([FromBody] UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.UpdateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }
}
