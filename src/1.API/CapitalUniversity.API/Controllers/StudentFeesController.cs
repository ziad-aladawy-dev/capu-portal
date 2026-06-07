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
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetUnpaidForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _fees.GetUnpaidForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _fees.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
