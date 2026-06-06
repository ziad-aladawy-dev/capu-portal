namespace CapitalUniversity.Sync.Staff.Configuration;

public sealed class StaffSyncOptions
{
    public const string SectionName = "Sync:Staff";

    public string ConnectionString { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 10;
    public int PushBatchSize { get; set; } = 10;

    /// <summary>
    /// Pull-extractor clawback in seconds. See
    /// <c>StudentSyncOptions.ExtractorSafetyBufferSeconds</c> for the rationale.
    /// </summary>
    public int ExtractorSafetyBufferSeconds { get; set; } = 1;
}