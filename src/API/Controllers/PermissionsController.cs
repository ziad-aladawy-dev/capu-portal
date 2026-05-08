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

    /// For data models related to permission assignments, we use the DTOs defined in the Core.Abstractions.Auth.Authorization.DTOs namespace.



    /// <summary>
    /// IPermissionManagementService is the main service for managing permissions and assignments.
    /// ICurrentUser is used to get the current user's ID for fetching effective permissions.
    /// </summary>
    private readonly IPermissionManagementService _permissionService;
    private readonly ICurrentUser _currentUser;


    public PermissionsController(IPermissionManagementService permissionService, ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets the effective permissions for the current user. This includes all permissions granted directly or via roles.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PermissionDto>>> GetEffectivePermissions(CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetEffectivePermissionsAsync(_currentUser.Id, cancellationToken);
        return Ok(permissions);
    }

    /// </summary>
    /// <param name="query">The query parameters for fetching a specific permission assignment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The permission assignment matching the query parameters.</returns>
    /// </summary>
    [HttpGet("assignment")]
    public async Task<ActionResult<PermissionAssignmentResponse>> GetAssignment([FromQuery] GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.GetAssignmentAsync(query, cancellationToken);
        if (assignment == null)
            return NotFound();

        return Ok(assignment);
    }

    /// <summary>
    /// <param name="request">The request object containing the details for creating a permission assignment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created permission assignment.</returns>
    /// </summary>

    [HttpPost]
    public async Task<ActionResult<PermissionAssignmentResponse>> CreateAssignment([FromBody] CreatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.CreateAssignmentAsync(request, cancellationToken);
        // We return the assignment itself. The user can fetch it via GetAssignment using scoped query params.
        return Ok(assignment);
    }

    /// <summary>
    /// Updates an existing permission assignment.
    /// <param name="request">The request object containing the details for updating a permission assignment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated permission assignment.</returns>
    /// </summary>
        
    [HttpPut("assignment")]
    public async Task<ActionResult<PermissionAssignmentResponse>> UpdateAssignment([FromBody] UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await _permissionService.UpdateAssignmentAsync(request, cancellationToken);
        return Ok(assignment);
    }
}
