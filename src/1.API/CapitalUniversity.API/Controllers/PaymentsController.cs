using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Payments;
using CapitalUniversity.Core.Abstractions.Payments.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentVerificationService _service;

    public PaymentsController(IPaymentVerificationService service)
    {
        _service = service;
    }

    [HttpPost("transactions")]
    [HasPermission(PermissionNames.PaymentTransactions.Insert)]
    public async Task<IActionResult> Record([FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.RecordAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("invoices/{invoiceId:guid}/transactions")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetForInvoice(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await _service.GetForInvoiceAsync(invoiceId, cancellationToken);
        return Ok(result);
    }
}
