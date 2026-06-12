using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Treasury payment orders. Additive — independent of the legacy invoice
/// endpoints. Scope is enforced in the service layer per student.
/// </summary>
[ApiController]
[Route("api/payments/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IPaymentInitiationService _initiation;

    public OrdersController(IOrderService orders, IPaymentInitiationService initiation)
    {
        _orders = orders;
        _initiation = initiation;
    }

    [HttpPost]
    // B8 — student self-service. OrderService.CreateOrderAsync enforces self-access
    // (CanAccessStudentAsync on request.StudentId), so a student can only create an
    // order for themselves; admins hold payments.orders via the Super Admin grant-all.
    [HasPermission(PermissionNames.PaymentOrders.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _orders.CreateOrderAsync(request.StudentId, request.FeeIds, request.Gateway, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    // GetByIdAsync returns null (→ 404) when the order is not the caller's own.
    [HasPermission(PermissionNames.PaymentOrders.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, cancellationToken);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpGet("by-student/{studentId:guid}")]
    // Self-scoped: a student may only read their own orders; staff need scope over
    // the student. GetForStudentAsync re-checks scope as defence in depth.
    [HasPermission(PermissionNames.PaymentOrders.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _orders.GetForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    // Ops-only: cancellation is not part of the student self-service flow.
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _orders.CancelAsync(id, cancellationToken);
        return Ok(new { Message = "Order cancelled" });
    }

    [HttpPost("{id:guid}/initiate")]
    // B8 — student self-service. PaymentInitiationService resolves the order's
    // student and the initiate path is reached only for the caller's own order.
    [HasPermission(PermissionNames.PaymentOrders.Insert)]
    public async Task<IActionResult> Initiate(Guid id, [FromQuery] string? redirectUrl, CancellationToken cancellationToken)
    {
        var result = await _initiation.InitiateAsync(id, redirectUrl, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)] // Reusing an administrative permission
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.CloseRecordAsync(id, cancellationToken);
        return Ok(order);
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)] // Reusing an administrative permission
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.OpenRecordAsync(id, cancellationToken);
        return Ok(order);
    }
}
