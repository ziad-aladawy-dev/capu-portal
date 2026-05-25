using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
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
    [HasPermission(PermissionNames.Permissions.View)]
    public async Task<ActionResult<List<PermissionDto>>> GetEffectivePermissions(CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetEffectivePermissionsAsync(_currentUser.Id, cancellationToken);
        return Ok(permissions);
    }

    [HttpGet("assignment")]
    [HasPermission(PermissionNames.Permissions.View)]
    public async Task<ActionResult<PermissionAssignmentResponse>> GetAssignment([FromQuery] GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.GetAssignmentAsync(query, cancellationToken);
        if (assignment == null)
            return NotFound();

        return Ok(assignment);
    }

    [HttpPost]
    [HasPermission(PermissionNames.Permissions.Insert)]
    public async Task<ActionResult<PermissionAssignmentResponse>> CreateAssignment([FromBody] CreatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.CreateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }

    [HttpPut("assignment")]
    [HasPermission(PermissionNames.Permissions.EditClose)]
    public async Task<ActionResult<PermissionAssignmentResponse>> UpdateAssignment([FromBody] UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.UpdateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }

    /// <summary>
    /// 3.3 — bulk-create assignments. Body: <c>{ assignments: [...] }</c>.
    /// All-or-nothing — wraps every per-row call in one transaction.
    /// </summary>
    [HttpPost("assignment/batch")]
    [HasPermission(PermissionNames.Permissions.Insert)]
    public async Task<IActionResult> BatchCreate([FromBody] BatchCreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        BulkRequestGuard.EnsureRecords(request?.Assignments, propertyName: "assignments");

        var results = await _permissionService.BatchCreateAssignmentsAsync(request!.Assignments, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// 3.4 — bulk-update / revoke assignments. Body: <c>{ updates: [...] }</c>.
    /// All-or-nothing — wraps every per-row call in one transaction. Each
    /// update may carry <c>RolesToRemove</c> and <c>PermissionsToRemove</c>
    /// so this single endpoint covers both bulk add and bulk revoke.
    /// </summary>
    [HttpPut("assignment/batch")]
    [HasPermission(PermissionNames.Permissions.EditClose)]
    public async Task<IActionResult> BatchUpdate([FromBody] BatchUpdateAssignmentRequest request, CancellationToken cancellationToken)
    {
        BulkRequestGuard.EnsureRecords(request?.Updates, propertyName: "updates");

        var results = await _permissionService.BatchUpdateAssignmentsAsync(request!.Updates, cancellationToken);
        return Ok(results);
    }

    public sealed class BatchCreateAssignmentRequest
    {
        public IReadOnlyList<CreatePermissionAssignmentRequest> Assignments { get; init; } = Array.Empty<CreatePermissionAssignmentRequest>();
    }

    public sealed class BatchUpdateAssignmentRequest
    {
        public IReadOnlyList<UpdatePermissionAssignmentRequest> Updates { get; init; } = Array.Empty<UpdatePermissionAssignmentRequest>();
    }
}
