using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
[SuppressMessage("Major Code Smell", "S6960:Controllers should not have mixed responsibilities",
    Justification = "Single CRUD aggregate for the Role resource: 5 thin pass-through endpoints (list/get/create/update/delete) over one DDD entity. Splitting into 4 controllers would add ceremony with no separation-of-concerns gain.")]
public class RolesController : ControllerBase
{
    private readonly CreateRoleCommandHandler _createRoleHandler;
    private readonly UpdateRoleCommandHandler _updateRoleHandler;
    private readonly DeleteRoleCommandHandler _deleteRoleHandler;
    private readonly GetRoleByIdQueryHandler _getRoleByIdHandler;
    private readonly GetRolesQueryHandler _getRolesHandler;
    private readonly GetRoleMembersQueryHandler _getRoleMembersHandler;
    private readonly AddRoleMemberCommandHandler _addRoleMemberHandler;
    private readonly RemoveRoleMemberCommandHandler _removeRoleMemberHandler;

    public RolesController(
        CreateRoleCommandHandler createRoleHandler,
        UpdateRoleCommandHandler updateRoleHandler,
        DeleteRoleCommandHandler deleteRoleHandler,
        GetRoleByIdQueryHandler getRoleByIdHandler,
        GetRolesQueryHandler getRolesHandler,
        GetRoleMembersQueryHandler getRoleMembersHandler,
        AddRoleMemberCommandHandler addRoleMemberHandler,
        RemoveRoleMemberCommandHandler removeRoleMemberHandler)
    {
        _createRoleHandler = createRoleHandler;
        _updateRoleHandler = updateRoleHandler;
        _deleteRoleHandler = deleteRoleHandler;
        _getRoleByIdHandler = getRoleByIdHandler;
        _getRolesHandler = getRolesHandler;
        _getRoleMembersHandler = getRoleMembersHandler;
        _addRoleMemberHandler = addRoleMemberHandler;
        _removeRoleMemberHandler = removeRoleMemberHandler;
    }

    [HttpGet]
    [HasPermission(PermissionNames.Roles.View)]
    public async Task<ActionResult<PagedRoleResponse>> GetRoles([FromQuery] GetRolesRequest request, CancellationToken cancellationToken)
    {
        var response = await _getRolesHandler.Handle(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.Roles.View)]
    public async Task<ActionResult<RoleResponse>> GetRole(Guid id, CancellationToken cancellationToken)
    {
        var response = await _getRoleByIdHandler.Handle(new GetRoleByIdRequest { Id = id }, cancellationToken);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpPost]
    [HasPermission(PermissionNames.Roles.Insert)]
    public async Task<ActionResult<CreateRoleResponse>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await _createRoleHandler.Handle(request, cancellationToken);
        return CreatedAtAction(nameof(GetRole), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionNames.Roles.EditClose)]
    public async Task<ActionResult<UpdateRoleResponse>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest();
        var response = await _updateRoleHandler.Handle(request, cancellationToken);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpGet("{id:guid}/members")]
    [HasPermission(PermissionNames.Roles.View)]
    public async Task<ActionResult<List<RoleMemberResponse>>> GetRoleMembers(Guid id, CancellationToken cancellationToken)
    {
        var members = await _getRoleMembersHandler.Handle(new GetRoleMembersRequest { RoleId = id }, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    [HasPermission(PermissionNames.Roles.EditClose)]
    public async Task<ActionResult> AddRoleMember(Guid id, [FromBody] AddRoleMemberRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RoleId) return BadRequest();
        var result = await _addRoleMemberHandler.Handle(request, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{staffId:guid}")]
    [HasPermission(PermissionNames.Roles.EditClose)]
    public async Task<ActionResult> RemoveRoleMember(Guid id, Guid staffId, CancellationToken cancellationToken)
    {
        var result = await _removeRoleMemberHandler.Handle(new RemoveRoleMemberRequest { RoleId = id, StaffId = staffId }, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.Roles.Delete)]
    public async Task<ActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteRoleHandler.Handle(new DeleteRoleRequest { Id = id }, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }
}
