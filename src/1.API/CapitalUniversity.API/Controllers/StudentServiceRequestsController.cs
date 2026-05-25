using System.Security.Claims;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Student-facing request endpoints. The caller's <c>studentId</c> is bound
/// from the JWT, so a student cannot submit/cancel on someone else's behalf
/// even if they fabricate a body. Out-of-scope reads surface as 404.
/// </summary>
[ApiController]
[Route("api/student-service-requests")]
public class StudentServiceRequestsController : ControllerBase
{
    private readonly IStudentServiceRequestService _service;

    public StudentServiceRequestsController(IStudentServiceRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    [HasPermission(PermissionNames.StudentServiceRequests.Insert)]
    public async Task<IActionResult> Submit([FromBody] SubmitStudentServiceRequestRequest request, CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId();
        var id = await _service.SubmitAsync(studentId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Request submitted successfully" });
    }

    [HttpGet]
    [HasPermission(PermissionNames.StudentServiceRequests.View)]
    public async Task<IActionResult> List([FromQuery] StudentServiceRequestListQuery query, CancellationToken cancellationToken)
    {
        // The service-layer scope check restricts students to their own rows;
        // we still pin the filter so the underlying query is narrow.
        query.StudentId = ResolveStudentId();
        var result = await _service.ListAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.StudentServiceRequests.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelStudentServiceRequestRequest request, CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId();
        await _service.CancelAsync(studentId, id, request, cancellationToken);
        return Ok(new { Message = "Request cancelled" });
    }

    /// <summary>
    /// Staff bulk-transition of a slate of requests. Each row routes through
    /// the workflow validator + scope check + audit log, so a per-row failure
    /// (NotFound / InvalidTransition / Validation) lands in <c>failures</c>
    /// without aborting peers. Use case: clearing the staff queue in one
    /// approve / reject sweep.
    /// </summary>
    [HttpPost("transition")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> BulkTransition([FromBody] BulkActionRequest<MoveRequestWorkflowStateRequest> request, CancellationToken cancellationToken)
    {
        if (request is null || request.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new { Message = "At least one request id is required." });
        }
        if (request.Ids.Count > BulkConstants.MaxBulkSize)
        {
            return BadRequest(new { Message = $"Cannot transition more than {BulkConstants.MaxBulkSize} requests in one request." });
        }
        if (request.Payload is null)
        {
            return BadRequest(new { Message = "A target status payload is required." });
        }

        var staffId = ResolveCallerId();
        var result = await _service.BulkTransitionAsync(request.Ids, staffId, request.Payload, cancellationToken);
        return Ok(result);
    }

    private Guid ResolveStudentId() => ResolveCallerId();

    private Guid ResolveCallerId()
    {
        // The Authentication module mints a JWT carrying the user id as the
        // standard <c>NameIdentifier</c> claim. For student-facing endpoints
        // this is the student id; for staff-facing bulk transition it is the
        // staff id — the bound user is the actor either way.
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }
}
