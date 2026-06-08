using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Application.Treasury;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Inbound HU Treasury payment webhook. Anonymous (Treasury has no JWT) but
/// guarded by a shared-secret header when configured. Settlement is idempotent,
/// so duplicate deliveries never create duplicate payments. Additive endpoint.
/// </summary>
[ApiController]
[Route("api/payments/webhook")]
[AllowAnonymous]
public class PaymentsWebhookController : ControllerBase
{
    private const string SignatureHeader = "X-Treasury-Signature";

    private readonly ISettlementService _settlement;
    private readonly TreasuryOptions _options;

    public PaymentsWebhookController(ISettlementService settlement, IOptions<TreasuryOptions> options)
    {
        _settlement = settlement;
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TreasuryWebhookNotification notification, CancellationToken cancellationToken)
    {
        // Shared-secret check (skipped when no secret configured — dev).
        if (!string.IsNullOrEmpty(_options.WebhookSecret))
        {
            var provided = Request.Headers[SignatureHeader].ToString();
            if (!string.Equals(provided, _options.WebhookSecret, StringComparison.Ordinal))
            {
                return Unauthorized();
            }
        }

        if (notification is null || string.IsNullOrWhiteSpace(notification.MerchantOrderId))
        {
            return BadRequest(new { error = "MerchantOrderId is required." });
        }

        var outcome = TreasuryStatusMapper.Map(notification.Status);
        var raw = JsonSerializer.Serialize(notification);

        await _settlement.SettleAsync(
            notification.MerchantOrderId,
            outcome,
            TransactionType.Webhook,
            raw,
            cancellationToken);

        // Always 200 once accepted+audited so Treasury does not hammer retries.
        return Ok(new { received = true });
    }
}
