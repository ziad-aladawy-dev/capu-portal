using System.Security.Claims;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers.Admin;

/// <summary>
/// Staff-facing request endpoints. Staff with View see the queue; higher
/// verbs gate approve / reject / advance. The service layer enforces the
/// workflow constraints — invalid transitions surface as 409.
/// </summary>
[ApiController]
[Route("api/admin/student-service-requests")]
public class StudentServiceRequestsAdminController : ControllerBase
{
    private readonly IStudentServiceRequestService _service;

    public StudentServiceRequestsAdminController(IStudentServiceRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.StudentServiceRequests.View)]
    public async Task<IActionResult> List([FromQuery] StudentServiceRequestListQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("pending")]
    [HasPermission(PermissionNames.StudentServiceRequests.View)]
    public async Task<IActionResult> Pending([FromQuery] StudentServiceRequestListQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.ListPendingAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("assigned-to-me")]
    [HasPermission(PermissionNames.StudentServiceRequests.View)]
    public async Task<IActionResult> AssignedToMe([FromQuery] StudentServiceRequestListQuery query, CancellationToken cancellationToken)
    {
        var staffId = ResolveStaffId();
        var result = await _service.ListAssignedToStaffAsync(staffId, query, cancellationToken);
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

    [HttpPost("{id:guid}/approve")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStudentServiceRequestRequest request, CancellationToken cancellationToken)
    {
        var staffId = ResolveStaffId();
        await _service.ApproveAsync(id, staffId, request, cancellationToken);
        return Ok(new { Message = "Request approved" });
    }

    [HttpPost("{id:guid}/reject")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStudentServiceRequestRequest request, CancellationToken cancellationToken)
    {
        var staffId = ResolveStaffId();
        await _service.RejectAsync(id, staffId, request, cancellationToken);
        return Ok(new { Message = "Request rejected" });
    }

    [HttpPost("{id:guid}/transition")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> Transition(Guid id, [FromBody] MoveRequestWorkflowStateRequest request, CancellationToken cancellationToken)
    {
        var staffId = ResolveStaffId();
        await _service.MoveStateAsync(id, staffId, request, cancellationToken);
        return Ok(new { Message = "Request transitioned" });
    }

    [HttpPost("{id:guid}/confirm-payment")]
    [HasPermission(PermissionNames.StudentServiceRequests.EditClose)]
    public async Task<IActionResult> ConfirmPayment(Guid id, CancellationToken cancellationToken)
    {
        // Break-glass endpoint. The happy path runs through the
        // <c>payments.invoice.paid</c> outbox handler — Payments publishes
        // the fact and InvoicePaidEventHandler advances the request. This
        // endpoint stays mounted for the rare cases where ops needs to push
        // a stuck request through manually (e.g. a gateway reconciliation
        // landed bytes-on-disk but the outbox row was lost).
        //
        // ConfirmPaymentAsync is idempotent — a second call after the handler
        // has already advanced the request is a logged no-op, not a 409.
        await _service.ConfirmPaymentAsync(id, cancellationToken);
        return Ok(new { Message = "Payment confirmed" });
    }

    private Guid ResolveStaffId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }
}
