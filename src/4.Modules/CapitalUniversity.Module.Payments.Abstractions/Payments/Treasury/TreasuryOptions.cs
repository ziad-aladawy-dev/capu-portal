namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Bound from the <c>Treasury</c> configuration section. Holds the HU Treasury
/// base URL, credentials, and the relevant connection-type discriminator.
/// </summary>
public sealed class TreasuryOptions
{
    public const string SectionName = "Treasury";

    /// <summary>Treasury API base URL. Empty disables outbound calls (dev default).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Optional API key sent as the <c>X-Api-Key</c> header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Only receipts with this connection type are relevant. Default 6.</summary>
    public int ConnectionTypeId { get; set; } = 6;

    /// <summary>Per-request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Relative path for the receipts list endpoint.</summary>
    public string ReceiptsPath { get; set; } = "api/payments/receipts";

    /// <summary>Cron for the recurring receipt-pull job (Hangfire, Sync host). Default every 6h.</summary>
    public string ReceiptPullCron { get; set; } = "0 */6 * * *";

    /// <summary>Return URL handed to Treasury at initiation when the caller supplies none.</summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>Minutes until a PendingPayment order is considered expired (reconciliation TTL).</summary>
    public int OrderTtlMinutes { get; set; } = 30;
}
