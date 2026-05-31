namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Module-supplied checkpoint snapshot. <see cref="Cursor"/> is an opaque string
/// chosen by the module's extractor (typically an ISO-8601 <c>UpdatedAt</c>
/// stamp or a sequence number) and round-tripped via the checkpoint store.
/// </summary>
public sealed class SyncCheckpoint
{
    public required string ModuleName { get; init; }

    public DateTimeOffset? LastSyncedAt { get; init; }

    public string? Cursor { get; init; }
}