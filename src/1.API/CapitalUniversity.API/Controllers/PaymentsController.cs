using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// Paged transactions search. Filters: <c>invoiceId</c>, <c>studentId</c>,
    /// <c>status</c>, <c>provider</c>, <c>from/to</c>, amount range, free-text.
    /// </summary>
    [HttpGet("transactions")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> SearchTransactions([FromQuery] PaymentTransactionSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Webhook to receive payment updates from the university vault / bank.
    /// In a real scenario, this would verify a signature (e.g. HMAC) from the bank.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> PaymentWebhook([FromBody] PaymentWebhookPayload payload, CancellationToken cancellationToken)
    {
        // 1. Verify webhook signature (stubbed for demonstration)
        if (string.IsNullOrEmpty(payload.TransactionId) || payload.InvoiceId == Guid.Empty)
        {
            return BadRequest(new { Message = "Invalid webhook payload" });
        }

        // Map status string to enum (basic mapping)
        var status = payload.Status.ToLower() == "succeeded" 
            ? PaymentTransactionStatus.Succeeded 
            : PaymentTransactionStatus.Pending;

        // 2. Record the payment to update the invoice and transaction history
        var request = new RecordPaymentRequest
        {
            InvoiceId = payload.InvoiceId,
            Amount = payload.Amount,
            Provider = payload.Provider ?? "BankVault",
            ProviderTransactionId = payload.TransactionId,
            Status = status,
            IdempotencyKey = payload.TransactionId,
            RawPayloadJson = "{\"source\": \"webhook\"}"
        };

        try
        {
            await _service.RecordAsync(request, cancellationToken);
            return Ok(new { Message = "Webhook processed successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}

public class PaymentWebhookPayload
{
    public Guid InvoiceId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}
