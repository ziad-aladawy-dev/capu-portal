namespace CapitalUniversity.Sync.Finance.Configuration;

public sealed class FinanceSyncOptions
{
    public const string SectionName = "Sync:Finance";

    public string ConnectionString { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 50;
    public int PushBatchSize { get; set; } = 50;

    /// <summary>
    /// Pull-extractor clawback in seconds. See
    /// <c>StudentSyncOptions.ExtractorSafetyBufferSeconds</c> for the rationale.
    /// Finance defaults to a wider window than other modules because bursar
    /// systems frequently back-date adjustments after end-of-day reconciliation.
    /// </summary>
    public int ExtractorSafetyBufferSeconds { get; set; } = 60;
}
