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

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpPost]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _orders.CreateOrderAsync(request.StudentId, request.FeeIds, request.Gateway, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, cancellationToken);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpGet("by-student/{studentId:guid}")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _orders.GetForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _orders.CancelAsync(id, cancellationToken);
        return Ok(new { Message = "Order cancelled" });
    }
}
