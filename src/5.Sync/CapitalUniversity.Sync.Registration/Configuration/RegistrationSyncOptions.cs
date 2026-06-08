namespace CapitalUniversity.Sync.Registration.Configuration;

/// <summary>
/// Options for the pull-only Registration sync module. There is no
/// <c>ConnectionString</c> / <c>PushBatchSize</c> here (unlike the other sync
/// modules) because this module owns no DbContext and no push outbox — it pulls
/// registration snapshots and writes them to Core through
/// <c>ICoreWriteGateway</c>.
/// </summary>
public sealed class RegistrationSyncOptions
{
    public const string SectionName = "Sync:Registration";

    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Pull-extractor clawback in seconds. See
    /// <c>CoursesSyncOptions.ExtractorSafetyBufferSeconds</c> for the rationale.
    /// </summary>
    public int ExtractorSafetyBufferSeconds { get; set; } = 1;
}
