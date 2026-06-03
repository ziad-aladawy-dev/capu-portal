namespace CapitalUniversity.Sync.Schedules.Configuration;

public sealed class SchedulesSyncOptions
{
    public const string SectionName = "Sync:Schedules";

    public string ConnectionString { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 25;
    public int PushBatchSize { get; set; } = 25;

    /// <summary>
    /// Pull-extractor clawback in seconds. See
    /// <c>StudentSyncOptions.ExtractorSafetyBufferSeconds</c> for the rationale.
    /// </summary>
    public int ExtractorSafetyBufferSeconds { get; set; } = 1;
}
