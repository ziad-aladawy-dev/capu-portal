using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Admin CRUD over service → Treasury-receipt mappings. Additive — independent
/// of the legacy invoice endpoints. Reuses the payment-transactions permission
/// surface until a dedicated mappings permission is introduced.
/// </summary>
[ApiController]
[Route("api/payments/service-receipt-mappings")]
public class ServiceReceiptMappingsController : ControllerBase
{
    private readonly IServiceReceiptMappingService _service;

    public ServiceReceiptMappingsController(IServiceReceiptMappingService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateServiceReceiptMappingRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeactivateAsync(id, cancellationToken);
        return Ok(new { Message = "Mapping deactivated" });
    }
}
