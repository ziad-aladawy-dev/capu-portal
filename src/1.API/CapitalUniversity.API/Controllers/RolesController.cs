using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize] // Added Authorize attribute to ensure security
public class RolesController : ControllerBase
{
    private readonly CreateRoleCommandHandler _createRoleHandler;
    private readonly UpdateRoleCommandHandler _updateRoleHandler;
    private readonly DeleteRoleCommandHandler _deleteRoleHandler;
    private readonly GetRoleByIdQueryHandler _getRoleByIdHandler;
    private readonly GetRolesQueryHandler _getRolesHandler;

    public RolesController(
        CreateRoleCommandHandler createRoleHandler,
        UpdateRoleCommandHandler updateRoleHandler,
        DeleteRoleCommandHandler deleteRoleHandler,
        GetRoleByIdQueryHandler getRoleByIdHandler,
        GetRolesQueryHandler getRolesHandler)
    {
        _createRoleHandler = createRoleHandler;
        _updateRoleHandler = updateRoleHandler;
        _deleteRoleHandler = deleteRoleHandler;
        _getRoleByIdHandler = getRoleByIdHandler;
        _getRolesHandler = getRolesHandler;
    }

    [HttpGet]
    public async Task<ActionResult<PagedRoleResponse>> GetRoles([FromQuery] GetRolesRequest request, CancellationToken cancellationToken)
    {
        var response = await _getRolesHandler.Handle(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleResponse>> GetRole(Guid id, CancellationToken cancellationToken)
    {
        var response = await _getRoleByIdHandler.Handle(new GetRoleByIdRequest { Id = id }, cancellationToken);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CreateRoleResponse>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await _createRoleHandler.Handle(request, cancellationToken);
        return CreatedAtAction(nameof(GetRole), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateRoleResponse>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest();
        var response = await _updateRoleHandler.Handle(request, cancellationToken);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteRoleHandler.Handle(new DeleteRoleRequest { Id = id }, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }
}
