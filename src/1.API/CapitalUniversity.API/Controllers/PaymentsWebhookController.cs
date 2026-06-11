using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Application.Treasury;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
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
    private const string SignatureHeader = "X-Webhook-Signature";

    private readonly ISettlementService _settlement;
    private readonly TreasuryOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PaymentsWebhookController> _logger;

    public PaymentsWebhookController(
        ISettlementService settlement,
        IOptions<TreasuryOptions> options,
        IHostEnvironment environment,
        ILogger<PaymentsWebhookController> logger)
    {
        _settlement = settlement;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TreasuryWebhookNotification notification, CancellationToken cancellationToken)
    {
        // Fail closed. With no secret configured we reject everything outside
        // Development — an unauthenticated settlement trigger is never acceptable
        // in a deployed environment.
        if (string.IsNullOrEmpty(_options.WebhookSecret))
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "Treasury webhook rejected: no Treasury:WebhookSecret configured (fail-closed in {Environment}).",
                    _environment.EnvironmentName);
                return Unauthorized();
            }

            _logger.LogWarning(
                "Treasury webhook accepted WITHOUT a secret — Development only. Configure Treasury:WebhookSecret before deploying.");
        }
        else
        {
            var provided = Request.Headers[SignatureHeader].ToString();
            if (!FixedTimeSecretEquals(provided, _options.WebhookSecret))
            {
                _logger.LogWarning("Treasury webhook rejected: missing or invalid {Header}.", SignatureHeader);
                return Unauthorized();
            }
        }

        var tx = notification?.Transaction;
        if (tx is null || string.IsNullOrWhiteSpace(tx.MerchantOrderId))
        {
            return BadRequest(new { error = "transaction.merchantOrderId is required." });
        }

        var outcome = TreasuryStatusMapper.Map(tx.Status);
        var raw = JsonSerializer.Serialize(notification);

        await _settlement.SettleAsync(
            tx.MerchantOrderId,
            outcome,
            TransactionType.Webhook,
            raw,
            tx.GrossAmount,
            cancellationToken);

        // Always 200 once accepted+audited so Treasury does not hammer retries.
        return Ok(new { received = true });
    }

    /// <summary>
    /// Constant-time comparison of the presented secret against the configured
    /// one (H2). Both are hashed to fixed-length SHA-256 digests first so neither
    /// the secret's length nor its content leaks through response timing — the
    /// previous Ordinal string compare short-circuited on the first mismatched
    /// character.
    ///
    /// Note: this still authenticates a STATIC shared secret, not an HMAC over the
    /// raw request body. A true body-bound signature (and timestamp/nonce replay
    /// rejection) requires the HU Treasury signing contract, which is not defined
    /// in this repository. Replay of an identical notification is already a no-op:
    /// SettlementService dedups on (MerchantOrderId, "{source}:{outcome}").
    /// </summary>
    private static bool FixedTimeSecretEquals(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        Span<byte> providedHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(provided), providedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
