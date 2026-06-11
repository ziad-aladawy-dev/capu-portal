using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Read-side for student fees (selection UI / order assembly). Additive.
/// </summary>
[ApiController]
[Route("api/payments/fees")]
public class StudentFeesController : ControllerBase
{
    private readonly IStudentFeeQueryService _fees;

    public StudentFeesController(IStudentFeeQueryService fees)
    {
        _fees = fees;
    }

    [HttpGet("by-student/{studentId:guid}")]
    // B8 — students view their OWN unpaid fees to assemble a payment order.
    // Self-scoped at the handler; the service re-checks scope as defence in depth.
    [HasPermission(PermissionNames.PaymentOrders.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetUnpaidForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _fees.GetUnpaidForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    // GetByIdAsync returns null (→ 404) when the fee is not the caller's own.
    [HasPermission(PermissionNames.PaymentOrders.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _fees.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
