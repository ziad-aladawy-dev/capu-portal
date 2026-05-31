namespace CapitalUniversity.Sync.Persistence.Entities;

public sealed class SyncCheckpointEntity
{
    public string ModuleName { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? Cursor { get; set; }
    public long? LastRowVersion { get; set; }
    public string? LastExternalVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}